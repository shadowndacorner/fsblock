# FSBlock

Simple .NET Core command line utility for performing actions on filesystem changes.  It can either call commands (batch file, shell script, etc) on file change or simply block the command line from executing until a file changes.

## Building
Use `dotnet publish -c release -r <platform-name>` to build a self-contained deployment for your platform.  Alternatively, simply build with `dotnet build` and run the resulting binary with `dotnet fsblock.dll`.  Alternatively, use [dotnet-warp](https://www.nuget.org/packages/dotnet-warp/) to build a completely self-contained executable.

## Usage Examples
```bat
fsblock.exe -p "src" -w -C "rebuild.bat" -F
```
The above usage sample calls `rebuild.bat` (`-C "rebuild.bat"`) any time a file changes within the `src/` folder (`-p "src"`).  It passes the filename as the first argument to the batch file (`-F`).  The `-w` flag causes it to run continuously rather than simply blocking.


```bat
@echo off
echo Press Ctrl+C to exit.

:loop
fsblock.exe -p "src"
pushd build/Ninja
ninja

IF %ERRORLEVEL% NEQ 0 (
    call resulting_executable.exe
) else (
    echo Ninja build failed, error is above
)

popd
goto loop
```

The above is a batch file that watches for changes and, if a change occurs, rebuilds with Ninja.  It is a very basic example for live coding, which was the original intent of this project.

## Command Line Options
Call fsblock.exe with the following options
```
-n, --norecurse     Specifies that the watch should be non-recursive

-v, --verbose       Verbose mode

-w, --watch           If feedback is disabled and no commands are set to run, this will appear to do nothing.

-f, --nofeedback    Specifies whether or not to print changes to stdout

-p, --path          Required. Specifies the directory to watch

-C, --command       Specifies a command to run from the current shell environment when a file changes.

-F, --forward       Specifies whether or not to forward modified file name to command line on changes.  Only has an effect if --command is set.

-N, --nocmdwait     Do not wait for command to finish.  Only has an effect if --command is set.

-i, --ignore        Set of paths to ignore
```