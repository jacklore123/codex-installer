@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoLogo -NoProfile -STA -ExecutionPolicy Bypass -File "%~dp0install-codex.ps1"
if errorlevel 1 (
    echo.
    echo 安装没有成功完成，请查看上方错误信息。
    pause
)
endlocal
