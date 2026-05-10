@echo off
chcp 65001 >nul
setlocal

REM ============================================================
REM  PDFPuzzle Release Build and Setup Generator
REM  ----------------------------------------------------------
REM  Usage : installer\build_release_setup.bat
REM  Output: installer\output\PDFPuzzle_Setup_v<AppVersion>.exe
REM
REM  Steps:
REM    1) Clean publish\
REM    2) MSBuild Restore (csproj direct, no .sln in repo)
REM    3) MSBuild Publish (PublishProfile=Win64Release / Self-contained / win-x64)
REM    4) ISCC to generate Inno Setup installer
REM
REM  Requirements:
REM    - VS 2022 Community (MSBuild) and Inno Setup 6 installed
REM    - Edit MSBUILD / ISCC variables below for different environments
REM ============================================================

set "REPO_ROOT=%~dp0.."
set "MSBUILD=C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
set "ISCC=C:\Users\zhang\AppData\Local\Programs\Inno Setup 6\ISCC.exe"
set "CSPROJ=%REPO_ROOT%\PDFPuzzle\PDFPuzzle.csproj"
set "ISS=%REPO_ROOT%\installer\PDFPuzzle.iss"
set "PUBLISH_DIR=%REPO_ROOT%\publish"

REM --- 1. Clean previous publish output ---
if exist "%PUBLISH_DIR%" (
    echo [build] Cleaning previous publish output...
    rmdir /s /q "%PUBLISH_DIR%"
    if errorlevel 1 goto :fail
)

REM --- 2. Restore ---
echo [build] Running MSBuild Restore...
"%MSBUILD%" "%CSPROJ%" /t:Restore /verbosity:minimal
if errorlevel 1 goto :fail

REM --- 3. Publish (Release / Self-contained / win-x64) ---
echo [build] Publishing Release (Self-contained, win-x64)...
"%MSBUILD%" "%CSPROJ%" /t:Publish /p:PublishProfile=Win64Release /p:Configuration=Release /verbosity:minimal
if errorlevel 1 goto :fail

REM --- 4. Inno Setup Compiler ---
echo [build] Running Inno Setup Compiler...
"%ISCC%" "%ISS%"
if errorlevel 1 goto :fail

echo.
echo [build] SUCCESS: Setup file generated in installer\output\
echo.
goto :end

:fail
echo.
echo [build] BUILD FAILED. See errors above.
echo.
endlocal
exit /b 1

:end
endlocal
exit /b 0
