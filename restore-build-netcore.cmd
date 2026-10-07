@echo off
setlocal
set CONFIGURATION=%1
if "%CONFIGURATION%"=="" set CONFIGURATION=Release

dotnet --info
if errorlevel 1 exit /b 1

dotnet restore BankSwitch.sln
if errorlevel 1 exit /b 1

dotnet build BankSwitch.sln -c %CONFIGURATION% --no-restore
if errorlevel 1 exit /b 1

dotnet test BankSwitch.sln -c %CONFIGURATION% --no-build
exit /b %ERRORLEVEL%
