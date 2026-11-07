@echo off
chcp 65001 >nul
setlocal EnableExtensions EnableDelayedExpansion
title WxChatServer 自动化部署

rem ============================================================
rem  WxChatServer 自动化部署脚本
rem  用法：
rem    deploy-server.bat                   发布并重启服务（默认端口 5210）
rem    deploy-server.bat 5210              指定端口
rem    deploy-server.bat 5210 install-service  注册为 Windows 服务（需管理员）
rem    deploy-server.bat stop              停止服务
rem    deploy-server.bat status            查看服务状态
rem ============================================================

rem 目录结构（多工程模块）：
rem   <模块根>\WxChatClientApp\WxChatApp.csproj   客户端工程
rem   <模块根>\WxChatServer\WxChatServer.csproj   服务端工程
rem   <模块根>\scripts\deploy-server.bat          本脚本
rem   <模块根>\publish\server\WxChatServer.exe    发布产物
rem 以 %~dp0 推导模块根，不依赖当前工作目录（双击/任意目录调用均可）。
for %%I in ("%~dp0..") do set "ROOT=%%~fI"
set "PROJ=%ROOT%\WxChatServer\WxChatServer.csproj"
set "PUBLISH=%ROOT%\publish\server"
rem 兜底：若脚本被移到模块根目录，则按 %~dp0 重新定位
if not exist "%PROJ%" if exist "%~dp0WxChatServer\WxChatServer.csproj" (
    for %%I in ("%~dp0.") do set "ROOT=%%~fI"
    set "PROJ=!ROOT!\WxChatServer\WxChatServer.csproj"
    set "PUBLISH=!ROOT!\publish\server"
)
set "PORT=5210"
set "ACTION=deploy"
if /i "%WXCHAT_NOPAUSE%"=="1" set "NOPAUSE=1"

if /i "%~1"=="help" goto :usage
if /i "%~1"=="-h" goto :usage
if /i "%~1"=="stop" set "ACTION=stop"
if /i "%~1"=="status" set "ACTION=status"
if not "%~1"=="" (
    if /i not "%~1"=="stop" if /i not "%~1"=="status" set "PORT=%~1"
)
if /i "%~2"=="install-service" set "ACTION=service"

echo ============================================================
echo   WxChatServer 自动化部署
echo   项目目录 : %ROOT%
echo   发布目录 : %PUBLISH%
echo   监听端口 : %PORT%
echo   执行动作 : %ACTION%
echo ============================================================

if /i "%ACTION%"=="status" goto :status
if /i "%ACTION%"=="stop" goto :stop

rem ---------- 0. 环境检查 ----------
where dotnet >nul 2>nul
if errorlevel 1 (
    echo [错误] 未检测到 dotnet 命令，请先安装 .NET 8 SDK：
    echo        https://dotnet.microsoft.com/download/dotnet/8.0
    goto :fail
)
if not exist "%PROJ%" (
    echo [错误] 找不到服务端项目文件：%PROJ%
    goto :fail
)

rem ---------- 1. 停止旧进程 ----------
echo.
echo [1/5] 停止已运行的服务端...
call :killserver
rem 等待文件句柄释放，否则发布时会因 exe 被占用而卡住
ping -n 4 127.0.0.1 >nul 2>nul

rem ---------- 2. 发布 ----------
echo.
echo [2/5] 编译并发布服务端（Release，自包含单文件）...
rem 清理上一次的多文件产物，保留 App_Data（数据库与上传目录）
if exist "%PUBLISH%" (
    for %%F in ("%PUBLISH%\*") do (
        if /I not "%%~nxF"=="App_Data" if exist "%%~fF" del /q "%%~fF" >nul 2>nul
    )
    if exist "%PUBLISH%\runtimes" rd /s /q "%PUBLISH%\runtimes" >nul 2>nul
)
dotnet publish "%PROJ%" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o "%PUBLISH%" --nologo -v minimal
if errorlevel 1 (
    echo [失败] 编译发布失败，请检查上方错误信息。
    goto :fail
)
echo [完成] 发布产物：%PUBLISH%\WxChatServer.exe（单文件，可单独拷贝/双击运行）

rem ---------- 3. 防火墙 ----------
echo.
echo [3/5] 配置防火墙入站规则（非管理员时会跳过）...
netsh advfirewall firewall add rule name="WxChatServer %PORT%" dir=in action=allow protocol=TCP localport=%PORT% >nul 2>nul
if errorlevel 1 (
    echo [提示] 防火墙规则添加失败，如为跨机访问请手动放行 TCP %PORT%
) else (
    echo [完成] 已放行 TCP %PORT%
)

rem ---------- 4. 启动 ----------
if /i "%ACTION%"=="service" goto :installservice

echo.
echo [4/5] 启动服务端...
set "WXCHAT_PORT=%PORT%"
if not defined WXCHAT_DATA set "WXCHAT_DATA=%PUBLISH%\App_Data"
if not exist "%WXCHAT_DATA%" mkdir "%WXCHAT_DATA%"
start "WxChatServer" /min "%PUBLISH%\WxChatServer.exe"
echo [完成] 已启动进程，数据目录：%WXCHAT_DATA%
goto :health

:installservice
echo.
echo [4/5] 注册为 Windows 服务...
net session >nul 2>&1
if errorlevel 1 (
    echo [错误] 注册服务需要管理员权限，请以管理员身份重新运行本脚本。
    goto :fail
)
sc query WxChatServer >nul 2>nul
if not errorlevel 1 (
    echo [信息] 清理旧服务...
    sc stop WxChatServer >nul 2>nul
    ping -n 4 127.0.0.1 >nul 2>nul
    sc delete WxChatServer >nul 2>nul
)
setx /M WXCHAT_PORT "%PORT%" >nul
setx /M WXCHAT_DATA "%PUBLISH%\App_Data" >nul
sc create WxChatServer binPath= "\"%PUBLISH%\WxChatServer.exe\"" start= auto DisplayName= "WxChatServer"
if errorlevel 1 (
    echo [失败] 服务创建失败。
    goto :fail
)
sc description WxChatServer "WxChat 即时通讯服务端（SignalR + SQLite）" >nul
sc start WxChatServer >nul
echo [完成] 服务已注册并启动（服务使用系统环境变量 WXCHAT_PORT/WXCHAT_DATA）

rem ---------- 5. 健康检查 ----------
:health
echo.
echo [5/5] 健康检查 http://127.0.0.1:%PORT%/api/health ...
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$deadline=(Get-Date).AddSeconds(25);" ^
  "do { try { $r=Invoke-WebRequest -Uri 'http://127.0.0.1:%PORT%/api/health' -UseBasicParsing -TimeoutSec 3;" ^
  "if($r.StatusCode -eq 200){ exit 0 } } catch {} ; Start-Sleep -Milliseconds 800 }" ^
  "while((Get-Date) -lt $deadline); exit 1"
if errorlevel 1 (
    echo [警告] 25 秒内未通过健康检查，请查看服务端控制台日志。
    goto :fail
)
echo [成功] 服务已就绪：http://127.0.0.1:%PORT%
echo         局域网访问：http://本机IP:%PORT%
echo         客户端可设置环境变量 WXCHAT_SERVER 指向该地址。
echo.
echo 部署完成。
if /i not "%ACTION%"=="service" (
    echo 提示：服务端进程以最小化窗口运行，关闭该窗口即停止服务。
)
if not defined NOPAUSE pause
exit /b 0

rem ============================================================
rem  子流程
rem ============================================================

:stop
echo.
echo 停止 WxChatServer ...
call :killserver
echo [完成] 已停止。
if not defined NOPAUSE pause
exit /b 0

:status
echo.
echo 进程/端口状态：
netstat -ano | findstr /C:":%PORT% " | findstr "LISTENING"
sc query WxChatServer 2>nul | findstr /C:"STATE"
echo.
echo 健康检查：
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "try { $r=Invoke-WebRequest -Uri 'http://127.0.0.1:%PORT%/api/health' -UseBasicParsing -TimeoutSec 3;" ^
  "if($r.StatusCode -eq 200){ Write-Host '[正常] 服务可用' -ForegroundColor Green; exit 0 } }" ^
  "catch {}; Write-Host '[异常] 服务不可用' -ForegroundColor Red; exit 1"
echo.
if not defined NOPAUSE pause
exit /b 0

:killserver
for /f "tokens=5" %%P in ('netstat -ano ^| findstr /C:":%PORT% " ^| findstr "LISTENING"') do (
    taskkill /PID %%P /F >nul 2>nul
)
taskkill /IM WxChatServer.exe /F >nul 2>nul
sc query WxChatServer >nul 2>nul
if not errorlevel 1 (
    sc stop WxChatServer >nul 2>nul
    ping -n 4 127.0.0.1 >nul 2>nul
)
exit /b 0

:usage
echo 用法：
echo   deploy-server.bat                      发布并重启（默认 5210）
echo   deploy-server.bat ^<端口^>               指定端口发布并重启
echo   deploy-server.bat ^<端口^> install-service  注册为 Windows 服务（管理员）
echo   deploy-server.bat stop                 停止服务
echo   deploy-server.bat status               查看状态
exit /b 0

:fail
echo.
echo 部署未成功完成，请根据上方提示处理。
if not defined NOPAUSE pause
exit /b 1
