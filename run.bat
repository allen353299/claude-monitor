@echo off
cd /d "%~dp0"

set EXE=App\bin\Debug\net8.0\ClaudeMonitor.App.exe

if not exist "%EXE%" (
    echo Building...
    dotnet build --nologo -v q
)

start "" "%EXE%"
