@echo off
setlocal
set "SCRIPT_DIR=%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%build_release_update_zip.ps1"
set "ERR=%ERRORLEVEL%"
echo.
if not "%ERR%"=="0" echo HATA: Build veya guncelleme paketi olusturma islemi basarisiz oldu. Kod=%ERR%
pause
exit /b %ERR%
