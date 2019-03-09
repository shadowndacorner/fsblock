using System;
using System.IO;
using CommandLine;
using System.Threading;
using System.Collections.Generic;
using System.Diagnostics;

namespace fsblock
{
    class Program
    {
        private static ManualResetEvent ExitWaiter = new ManualResetEvent(false);

        class Options
        {
            [Option('n', "norecurse", HelpText = "Specifies that the watch should be non-recursive")]
            public bool NoRecurse { get; set; }

            [Option('v', "verbose", HelpText = "Verbose mode")]
            public bool Verbose { get; set; }

            [Option('w', "watch", HelpText = "Continuously watch for changes.  Be sure feedback is enabled.")]
            public bool Watch { get; set; }

            [Option('f', "nofeedback", HelpText = "Specifies whether or not to print changes to stdout")]
            public bool NoFeedback { get; set; }

            [Option('p', "path", HelpText = "Specifies the directory to watch", Required = true)]
            public string Path { get; set; }

            [Option('C', "command", HelpText = "Specifies a command to run from the current shell environment when a file changes.", Default = null)]
            public string Command { get; set; }

            [Option('F', "forward", HelpText = "Specifies whether or not to forward modified file name to command line on changes.  Only has an effect if --command is set.")]
            public bool ForwardFileName { get; set; }

            [Option('N', "nocmdwait", HelpText = "Do not wait for command to finish.  Only has an effect if --command is set.")]
            public bool NoWaitForCommand { get; set; }

            [Option('i', "ignore", Separator = ' ', HelpText = "Set of paths to ignore")]
            public List<string> IgnorePaths { get; set; } = new List<string>();
        }

        static Options opt = null;
        static int Main(string[] args)
        {
            int returnCode = 0;
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed((o) =>
                {
                    opt = o;
                });

            if (opt == null)
            {
                //Console.Error.WriteLine("Failed to parse command line");
                return 0;
            }

            /*
			if (opt.Continue)
			{
				Console.Error.WriteLine("Non-blocking mode is not currently supported");
				return;
			}
			*/

            if (!Directory.Exists(opt.Path))
            {
                Console.Error.WriteLine($"Path {opt.Path} does not exist...");
                return 1;
            }

            if (opt.Command != null)
            {
                opt.Command = Path.GetFullPath(opt.Command);
                if (!File.Exists(opt.Command))
                {
                    Console.Error.WriteLine($"Command {opt.Path} does not exist...");
                    return 1;
                }
            }

            opt.Path = Path.GetFullPath(opt.Path);

            foreach(var v in opt.IgnorePaths)
            {
                v = Path.GetFullPath(v);
            }

            var watch = new FileSystemWatcher();
            watch.Path = opt.Path;
            watch.IncludeSubdirectories = !opt.NoRecurse;

            if (opt.Verbose)
            {
                Console.WriteLine($"Watching for changes in directory \"{opt.Path}\"");
            }

            if (opt.Watch)
            {
                Console.CancelKeyPress += (sender, e) =>
                {
                    if (opt.Verbose)
                    {
                        Console.WriteLine($"Exiting from interrupt signal...");
                    }

                    returnCode = -1;
                    e.Cancel = true;
                    ExitWaiter.Set();
                };

                watch.Created += Watch_Changed;
                watch.Changed += Watch_Changed;
                watch.Deleted += Watch_Changed;
                watch.Renamed += Watch_Changed;

                watch.Error += (s, a) =>
                {
                    Console.Error.WriteLine($"FSBlock error: {a.GetException()}");
                    returnCode = -1;
                    watch.EnableRaisingEvents = false;
                    ExitWaiter.WaitOne();
                };

                watch.EnableRaisingEvents = true;

                ExitWaiter.WaitOne();
            }
            else
            {
                bool repeat = true;
                while(repeat)
                {
                    repeat = false;
                    var change = watch.WaitForChanged(WatcherChangeTypes.All);
                    foreach (var v in opt.IgnorePaths)
                    {
                        if (Path.Combine(opt.Path, change.Name).Contains(v))
                        {
                            repeat = true;
                            continue;
                        }
                    }
                    if (opt.Verbose)
                    {
                        Console.WriteLine($"File \"{Path.Combine(opt.Path, change.Name)}\" changed");
                    }

                    if (!opt.NoFeedback)
                    {
                        if (change.ChangeType == WatcherChangeTypes.Renamed)
                        {
                            Console.WriteLine($"{change.ChangeType.ToString()}:\"{Path.Combine(opt.Path, change.Name)}\"<-\"{Path.Combine(opt.Path, change.OldName)}\"");
                        }
                        else
                        {
                            Console.WriteLine($"{change.ChangeType.ToString()}:\"{Path.Combine(opt.Path, change.Name)}\"");
                        }
                    }
                }
            }
            return returnCode;
        }

        public static FileStream WaitForFile(string fullPath, FileMode mode, FileAccess access, FileShare share)
        {
            for (int numTries = 0; numTries < 100; ++numTries)
            {
                FileStream fs = null;
                try
                {
                    fs = new FileStream(fullPath, mode, access, share);
                    return fs;
                }
                catch (IOException)
                {
                    if (fs != null)
                    {
                        fs.Dispose();
                    }
                    Thread.Sleep(50);
                }
            }

            return null;
        }

        private static Dictionary<string, DateTime> m_LastUpdateTimes = new Dictionary<string, DateTime>();
        private static void Watch_Changed(object sender, FileSystemEventArgs change)
        {
            foreach(var v in opt.IgnorePaths)
            {
                if (Path.Combine(opt.Path, change.Name).Contains(v))
                    return;
            }

            if (change.ChangeType == WatcherChangeTypes.Deleted)
            {
                m_LastUpdateTimes.Remove(change.FullPath);
            }
            else
            {
                using (var f = WaitForFile(change.FullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) { }

                if (m_LastUpdateTimes.ContainsKey(change.FullPath) &&
                    (DateTime.Now - m_LastUpdateTimes[change.FullPath]).TotalSeconds < 0.1)
                {
                    return;
                }

                m_LastUpdateTimes[change.FullPath] = DateTime.Now;
            }

            if (opt.Verbose)
            {
                Console.WriteLine($"File \"{Path.Combine(opt.Path, change.Name)}\" changed");
            }

            if (!opt.NoFeedback)
            {
                if (change.ChangeType == WatcherChangeTypes.Renamed)
                {
                    Console.WriteLine($"{change.ChangeType.ToString()}:\"{Path.Combine(opt.Path, change.Name)}\"<-\"{Path.Combine(opt.Path, (change as RenamedEventArgs).OldName)}\"");
                }
                else
                {
                    Console.WriteLine($"{change.ChangeType.ToString()}:\"{Path.Combine(opt.Path, change.Name)}\"");
                }
            }

            if (opt.Command != null)
            {
                var info = new ProcessStartInfo();
                using (var f = WaitForFile(opt.Command, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    info.FileName = opt.Command;
                    if (opt.ForwardFileName)
                    {
                        info.Arguments = change.Name;
                    }
                    info.UseShellExecute = true;

                    var proc = Process.Start(info);
                    if (!opt.NoWaitForCommand)
                        proc.WaitForExit();
                }
            }
        }
    }
}
