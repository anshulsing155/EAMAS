@echo off
setlocal

set "APP_NAME=EAMAS"
set "INSTALL_DIR=%LOCALAPPDATA%\Programs\EAMAS"
set "START_MENU_DIR=%APPDATA%\Microsoft\Windows\Start Menu\Programs\EAMAS"
set "DESKTOP_SHORTCUT=%USERPROFILE%\Desktop\EAMAS.lnk"
set "PAYLOAD=%~dp0EAMAS-win-x64.zip"
set "UNINSTALL_KEY=HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\EAMAS"

if not exist "%PAYLOAD%" (
    echo Installer payload not found.
    exit /b 1
)

if exist "%INSTALL_DIR%" (
    rmdir /S /Q "%INSTALL_DIR%"
)

mkdir "%INSTALL_DIR%" >nul 2>&1
mkdir "%START_MENU_DIR%" >nul 2>&1

powershell -NoProfile -ExecutionPolicy Bypass -Command "Expand-Archive -LiteralPath '%PAYLOAD%' -DestinationPath '%INSTALL_DIR%' -Force"
if errorlevel 1 (
    echo Failed to extract application files.
    exit /b 1
)

copy /Y "%~dp0uninstall.cmd" "%INSTALL_DIR%\uninstall.cmd" >nul

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$ws = New-Object -ComObject WScript.Shell; " ^
  "$shortcut = $ws.CreateShortcut('%START_MENU_DIR%\EAMAS.lnk'); " ^
  "$shortcut.TargetPath = '%INSTALL_DIR%\EAMAS.exe'; " ^
  "$shortcut.WorkingDirectory = '%INSTALL_DIR%'; " ^
  "$shortcut.Save(); " ^
  "$desktop = $ws.CreateShortcut('%DESKTOP_SHORTCUT%'); " ^
  "$desktop.TargetPath = '%INSTALL_DIR%\EAMAS.exe'; " ^
  "$desktop.WorkingDirectory = '%INSTALL_DIR%'; " ^
  "$desktop.Save()"
if errorlevel 1 (
    echo Installed app, but failed to create shortcuts.
)

reg add "%UNINSTALL_KEY%" /v "DisplayName" /t REG_SZ /d "EAMAS" /f >nul
reg add "%UNINSTALL_KEY%" /v "DisplayVersion" /t REG_SZ /d "1.0.0" /f >nul
reg add "%UNINSTALL_KEY%" /v "Publisher" /t REG_SZ /d "EAMAS Project" /f >nul
reg add "%UNINSTALL_KEY%" /v "InstallLocation" /t REG_SZ /d "%INSTALL_DIR%" /f >nul
reg add "%UNINSTALL_KEY%" /v "DisplayIcon" /t REG_SZ /d "%INSTALL_DIR%\EAMAS.exe" /f >nul
reg add "%UNINSTALL_KEY%" /v "UninstallString" /t REG_SZ /d "cmd.exe /c \"%INSTALL_DIR%\uninstall.cmd\"" /f >nul
reg add "%UNINSTALL_KEY%" /v "QuietUninstallString" /t REG_SZ /d "cmd.exe /c \"%INSTALL_DIR%\uninstall.cmd\"" /f >nul
reg add "%UNINSTALL_KEY%" /v "NoModify" /t REG_DWORD /d 1 /f >nul
reg add "%UNINSTALL_KEY%" /v "NoRepair" /t REG_DWORD /d 1 /f >nul

start "" "%INSTALL_DIR%\EAMAS.exe"
exit /b 0
