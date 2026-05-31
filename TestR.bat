@echo off
powershell -ExecutionPolicy Bypass -NoProfile -File "%~dp0TestR/Invoke-TestR.ps1" %*
