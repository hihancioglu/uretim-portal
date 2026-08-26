@echo off
setlocal
set "SCRIPT_DIR=%~dp0"
set "PROJECT=%SCRIPT_DIR%TeknikResimOlcum.vbproj"
set "OUTPUT=%SCRIPT_DIR%bin\Release\net8.0-windows\win-x64\publish"

echo.
echo ============================================================
echo  TeknikResimOlcum Release EXE Olusturma
echo ============================================================
echo.

dotnet publish "%PROJECT%" -c Release -r win-x64 --self-contained false /p:PublishSingleFile=true
set "ERR=%ERRORLEVEL%"

echo.
if not "%ERR%"=="0" (
    echo HATA: Release EXE olusturulamadi. Kod=%ERR%
    pause
    exit /b %ERR%
)

echo Release EXE basariyla olusturuldu:
echo %OUTPUT%\TeknikResimOlcum.exe
echo.
echo Not: Calistirilacak bilgisayarda .NET 8 Desktop Runtime bulunmalidir.
pause
exit /b 0
