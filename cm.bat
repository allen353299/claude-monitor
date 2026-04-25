@echo off
cd /d "%~dp0"

set EXE=App\bin\Debug\net10.0\ClaudeMonitor.App.exe

dotnet build App\App.csproj --nologo -v q
if errorlevel 1 (
    echo Build failed.
    exit /b 1
)

start "" "%EXE%"
