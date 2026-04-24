@echo off
cd /d "%~dp0"
start "" /b dotnet run --project App --no-build
