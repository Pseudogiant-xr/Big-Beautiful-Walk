@echo off
setlocal
rem A PowerShell 7 console passes its own module path down; Windows PowerShell rebuilds its default when it is unset.
set "PSModulePath="
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoLogo -NoProfile -ExecutionPolicy Bypass -STA -File "%~dp0scripts\Setup.ps1" %*
set "BBW_EXIT=%ERRORLEVEL%"
if not "%BBW_EXIT%"=="0" pause
exit /b %BBW_EXIT%
