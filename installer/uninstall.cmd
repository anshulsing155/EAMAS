@echo off
setlocal

set "INSTALL_DIR=%LOCALAPPDATA%\Programs\EAMAS"
set "START_MENU_DIR=%APPDATA%\Microsoft\Windows\Start Menu\Programs\EAMAS"
set "DESKTOP_SHORTCUT=%USERPROFILE%\Desktop\EAMAS.lnk"
set "UNINSTALL_KEY=HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\EAMAS"

taskkill /F /IM EAMAS.exe >nul 2>&1

if exist "%DESKTOP_SHORTCUT%" del /Q "%DESKTOP_SHORTCUT%"
if exist "%START_MENU_DIR%" rmdir /S /Q "%START_MENU_DIR%"
reg delete "%UNINSTALL_KEY%" /f >nul 2>&1
if exist "%INSTALL_DIR%" rmdir /S /Q "%INSTALL_DIR%"

exit /b 0
