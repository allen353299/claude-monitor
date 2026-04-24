@echo off
cd /d "%~dp0"
start "" /b dotnet run --project src\ClaudeMonitor.App --no-build
