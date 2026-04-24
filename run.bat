@echo off
cd /d "%~dp0"
start "" /b dotnet run --project src\C9Watch.App --no-build
