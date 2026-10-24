@echo off
rem ============================================================
rem  MouseSim one-click packaging script
rem  Produces two exe files in the publish folder:
rem    MouseSim.exe      - full (self-contained, ~72MB, no .NET install needed)
rem    MouseSim-lite.exe - lite  (framework-dependent, ~1MB, needs .NET 8 Desktop Runtime)
rem ============================================================
setlocal
set DOTNET=C:\Develop\.Net\dotnet.exe
set PROJ=%~dp0src\MouseSim.App\MouseSim.App.csproj
set OUT=%~dp0publish

if not exist "%DOTNET%" (
    echo [ERROR] dotnet not found: %DOTNET%
    pause
    exit /b 1
)

echo.
echo [1/2] Building FULL version (self-contained)...
"%DOTNET%" publish "%PROJ%" -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "%OUT%"
if errorlevel 1 goto :fail

echo.
echo [2/2] Building LITE version (framework-dependent)...
"%DOTNET%" publish "%PROJ%" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o "%OUT%\lite_tmp"
if errorlevel 1 goto :fail
move /y "%OUT%\lite_tmp\MouseSim.exe" "%OUT%\MouseSim-lite.exe" >nul
rd /s /q "%OUT%\lite_tmp"

del /q "%OUT%\*.pdb" 2>nul

echo.
echo ============================================================
echo  Done. Output:
echo ============================================================
dir /b "%OUT%"
echo.
echo  Full version : MouseSim.exe      - run anywhere, no prerequisites
echo  Lite version : MouseSim-lite.exe - target PC needs .NET 8 Desktop Runtime
echo ============================================================
pause
exit /b 0

:fail
echo.
echo [ERROR] Build FAILED - see messages above.
pause
exit /b 1
