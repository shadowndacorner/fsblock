@echo off

:loop
REM Block until something changes in the folder "test"
fsblock.exe -p "test" -f -w -C "example-rec.bat" -F
echo Something changed, run a build command or something
goto loop