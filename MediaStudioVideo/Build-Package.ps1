[CmdletBinding()]
param(
    [ValidateSet("Debug","Release","Both")]
    [string]$Configuration = "Release",
    [string]$Platform = "Any CPU",
    [string]$FfmpegPath = "",
    [string]$MSBuildPath = "",
    [switch]$NoZip,
    [switch]$Clean
)
$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$Solution = Join-Path $ProjectRoot "MediaStudioWpf48.sln"
$PublishRoot = Join-Path $ProjectRoot "publish"
$Timestamp = Get-Date -Format "yyyyMMdd_HHmmss"

function Find-MSBuild {
    if ($MSBuildPath) {
        if (Test-Path $MSBuildPath -PathType Leaf) { return (Resolve-Path $MSBuildPath).Path }
        throw "MSBuildPath 指定的文件不存在：$MSBuildPath"
    }
    if ($env:MSBUILD_EXE -and (Test-Path $env:MSBUILD_EXE -PathType Leaf)) {
        return (Resolve-Path $env:MSBUILD_EXE).Path
    }
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $install = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
        if ($install) {
            $candidate = Join-Path $install "MSBuild\Current\Bin\MSBuild.exe"
            if (Test-Path $candidate) { return $candidate }
        }
    }
    $cmd = Get-Command MSBuild.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $fallbacks = @(
      "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
      "${env:ProgramFiles}\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
      "${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
      "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe"
    )
    foreach ($p in $fallbacks) { if (Test-Path $p) { return $p } }
    throw "未找到 MSBuild。请安装 Visual Studio（含 .NET 桌面开发工作负载），或用 -MSBuildPath / 环境变量 MSBUILD_EXE 指定 MSBuild.exe。"
}

if (-not (Test-Path $Solution)) { throw "找不到解决方案：$Solution" }
$msbuild = Find-MSBuild
Write-Host "MSBuild: $msbuild" -ForegroundColor Cyan

if ($Clean -and (Test-Path $PublishRoot)) {
    Remove-Item $PublishRoot -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $PublishRoot | Out-Null

$configs = if ($Configuration -eq "Both") { @("Debug","Release") } else { @($Configuration) }
foreach ($config in $configs) {
    Write-Host "`n========== 编译 $config ==========" -ForegroundColor Cyan
    & $msbuild $Solution /m /t:Rebuild /p:Configuration=$config "/p:Platform=$Platform" /p:RestoreIgnoreFailedSources=true /v:minimal
    if ($LASTEXITCODE -ne 0) { throw "$config 编译失败，MSBuild exit code=$LASTEXITCODE" }

    $exeDir = Join-Path $ProjectRoot "bin\$config"
    $exe = Join-Path $exeDir "MediaStudioWpf48.exe"
    if (-not (Test-Path $exe)) { throw "编译结束但未找到输出文件：$exe" }

    $packageName = "MediaStudioWpf48_$config"
    $stage = Join-Path $PublishRoot $packageName
    if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $stage | Out-Null
    Copy-Item (Join-Path $exeDir "*") $stage -Recurse -Force

    # FFmpeg may be supplied explicitly, placed beside this script, or already in build output.
    $ffmpegCandidates = @()
    if ($FfmpegPath) { $ffmpegCandidates += $FfmpegPath }
    if ($env:FFMPEG_PATH) { $ffmpegCandidates += $env:FFMPEG_PATH }
    $ffmpegCandidates += @((Join-Path $ProjectRoot "ffmpeg.exe"), (Join-Path $exeDir "ffmpeg.exe"))
    $ffmpeg = $null
    foreach ($candidate in $ffmpegCandidates) {
        if (-not $candidate) { continue }
        $candidate = [Environment]::ExpandEnvironmentVariables($candidate.Trim().Trim('"'))
        if (Test-Path $candidate -PathType Container) {
            $candidate = Join-Path $candidate "ffmpeg.exe"
        } elseif ((Split-Path $candidate -Leaf) -ieq "ffprobe.exe") {
            $candidate = Join-Path (Split-Path $candidate -Parent) "ffmpeg.exe"
        }
        if ((Test-Path $candidate -PathType Leaf) -and ((Split-Path $candidate -Leaf) -ieq "ffmpeg.exe")) {
            $ffmpeg = (Resolve-Path $candidate).Path
            break
        }
    }
    if (-not $ffmpeg) {
        foreach ($folder in (($env:PATH -split [IO.Path]::PathSeparator) | Where-Object { $_ })) {
            $candidate = Join-Path $folder.Trim().Trim('"') "ffmpeg.exe"
            if (Test-Path $candidate -PathType Leaf) { $ffmpeg = (Resolve-Path $candidate).Path; break }
        }
    }
    if ($ffmpeg) {
        Copy-Item $ffmpeg (Join-Path $stage "ffmpeg.exe") -Force
        $ffmpegDir = Split-Path $ffmpeg -Parent
        foreach ($tool in @("ffprobe.exe","ffplay.exe")) {
            $toolPath = Join-Path $ffmpegDir $tool
            if (Test-Path $toolPath) { Copy-Item $toolPath (Join-Path $stage $tool) -Force }
        }
        Write-Host "使用 FFmpeg：$ffmpeg" -ForegroundColor Green
    } else {
        Write-Warning "未找到 FFmpeg。已检查 -FfmpegPath、环境变量 FFMPEG_PATH、项目目录及 PATH。"
    }

    @"
MediaStudioWpf48 - $config
运行：双击 MediaStudioWpf48.exe
依赖：Windows .NET Framework 4.8 Runtime
FFmpeg：请将 ffmpeg.exe 放在本目录，或在打包时通过 -FfmpegPath 指定。
"@ | Set-Content (Join-Path $stage "运行说明.txt") -Encoding UTF8

    if (-not $NoZip) {
        $zip = Join-Path $PublishRoot ("{0}_{1}.zip" -f $packageName,$Timestamp)
        if (Test-Path $zip) { Remove-Item $zip -Force }
        Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zip -CompressionLevel Optimal
        Remove-Item $stage -Recurse -Force
        Write-Host "打包完成：$zip" -ForegroundColor Green
        Write-Host "已清理临时发布目录：$stage" -ForegroundColor DarkGray
    } else {
        Write-Host "保留发布目录（-NoZip）：$stage" -ForegroundColor Green
    }
}
Write-Host "`n全部任务完成。" -ForegroundColor Green
