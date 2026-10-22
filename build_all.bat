@echo off
TITLE 软件版本平台发布中心
COLOR A0
setlocal EnableExtensions EnableDelayedExpansion

rem ============================================================
rem  WPF-Demo: build all project modules and package release
rem  Usage:   build_all.bat [Release^|Debug]     (default: Release)
rem  Output:  _release\<Module>\  per-module runnable output
rem           _release\WPF-Demo-All-<stamp>.zip   single zip package
rem           _release\logs\<Module>.log          build logs
rem ============================================================

set "ROOT=%~dp0"
set "CONFIG=%~1"
if "%CONFIG%"=="" set "CONFIG=Release"
set "RELDIR=%ROOT%Publish"
set "LOGDIR=%RELDIR%\logs"

rem ---------- clear env vars that collide with MSBuild reserved properties ----------
rem MSBuild inherits environment variables as global properties. Names like OutDir,
rem OutputPath, Configuration, Platform, TargetFramework etc. would silently
rem override every project's output path and scatter binaries into that folder.
set "OutDir="
set "OutputPath="
set "BaseOutputPath="
set "BaseIntermediateOutputPath="
set "IntermediateOutputPath="
set "PublishDir="
set "TargetDir="

echo.
echo [INFO] Cleaning output directory: %RELDIR%
powershell -NoProfile -Command "if (Test-Path '%RELDIR%') { Remove-Item -LiteralPath '%RELDIR%' -Recurse -Force -ErrorAction SilentlyContinue }"
mkdir "%LOGDIR%" >nul 2>nul
for /f %%i in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd_HHmmss"') do set "STAMP=%%i"

echo ================================================
echo  WPF-Demo build_all  ^|  config=%CONFIG%
echo ================================================

rem ---------- locate build tool: VS MSBuild first, dotnet fallback ----------
set "MSBUILD="
set "VSWHERE=C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe"
if exist "%VSWHERE%" (
    "%VSWHERE%" -latest -products * -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" > "%TEMP%\vs_msbuild.txt" 2>nul
    set /p MSBUILD=<"%TEMP%\vs_msbuild.txt"
    if not exist "!MSBUILD!" set "MSBUILD="
)
if not defined MSBUILD if exist "C:\Tang\IDE\Microsoft Visual Studio\18\Professional\MSBuild\Current\Bin\MSBuild.exe" set "MSBUILD=C:\Tang\IDE\Microsoft Visual Studio\18\Professional\MSBuild\Current\Bin\MSBuild.exe"

if defined MSBUILD (
    echo Build tool : !MSBUILD!
) else (
    where dotnet >nul 2>nul && (set "MSBUILD=dotnet" & echo Build tool : dotnet CLI)
)
if not defined MSBUILD (
    echo [ERROR] Neither VS MSBuild.exe nor dotnet CLI was found.
    exit /b 1
)

rem ---------- all modules (15) ----------
set "MODULES=BitTomrrorApp Cmp.UTools.GUI CoderControl CoordinateFunction Elastic.BMS GenealogyDemo IpanelWpf Radae3DDemo3 RadarApp RebootHMI Rxbit.Calibration Tang.RadarApplication Tang.VideoThrimalgoAI WpfDevDemo WxChatApp"

set /a PASS=0, FAIL=0, IDX=0

for %%M in (%MODULES%) do (
    set /a IDX+=1
    set "PROJ="
    for %%P in ("%ROOT%%%M\*.csproj") do set "PROJ=%%P"
    if not defined PROJ (
        echo [SKIP   ] %%M  - no .csproj found
        set /a FAIL+=1
    ) else (
        echo.
        echo [!IDX!/15] Building %%M ...
        if /i "!MSBUILD!"=="dotnet" (
            dotnet build "!PROJ!" -c %CONFIG% -v m --nologo > "%LOGDIR%\%%M.log" 2>&1
        ) else (
            "%MSBUILD%" "!PROJ!" /p:Configuration=%CONFIG% /v:m /nologo /m > "%LOGDIR%\%%M.log" 2>&1
        )
        if errorlevel 1 (
            echo [FAILED ] %%M  - see logs\%%M.log
            set /a FAIL+=1
        ) else (
            rem SDK-style output: bin\Release\netXX-windows\   old-style: bin\Release\
            set "SRC="
            for /d %%D in ("%ROOT%%%M\bin\%CONFIG%\net*") do set "SRC=%%D"
            if not defined SRC if exist "%ROOT%%%M\bin\%CONFIG%" set "SRC=%ROOT%%%M\bin\%CONFIG%"
            if not defined SRC (
                echo [FAILED ] %%M  - build output dir not found
                set /a FAIL+=1
            ) else (
            rem output goes straight into per-module folder, then zip & remove
            set "MODDIR=%RELDIR%\%%M"
            mkdir "!MODDIR!" >nul 2>nul
            xcopy "!SRC!\*" "!MODDIR!\" /e /i /y /q >nul
            powershell -NoProfile -Command "Compress-Archive -Path '!MODDIR!\*' -DestinationPath '%RELDIR%\%%M-%STAMP%.zip' -Force"
            if errorlevel 1 (
                echo [FAILED ] %%M  - zip failed
                set /a FAIL+=1
            ) else (
                rmdir /s /q "!MODDIR!" >nul 2>nul
                powershell -NoProfile -Command "if (Test-Path '!MODDIR!') { Remove-Item -LiteralPath '!MODDIR!' -Recurse -Force }"
                echo [OK     ] %%M  -^> %%M-%STAMP%.zip
                set /a PASS+=1
            )
            )
        )
    )
)

echo.
echo ================================================
echo  Build finished: !PASS! ok, !FAIL! failed
echo ================================================

rem ---------- final safety sweep: keep only zips and logs in release root ----------
powershell -NoProfile -Command "Get-ChildItem -LiteralPath '%RELDIR%' -Force | Where-Object { $_.Name -ne 'logs' -and -not ($_.PSIsContainer -eq $false -and $_.Extension -eq '.zip') } | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue"

if !FAIL! gtr 0 (
    echo One or more modules failed. Check %LOGDIR%
    exit /b 1
)

echo.
echo Release ready (per-module zips in %RELDIR%):
dir /b "%RELDIR%\*.zip"
echo.
echo Done.
endlocal
exit /b 0
