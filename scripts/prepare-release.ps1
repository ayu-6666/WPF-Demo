[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$Version)
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$solution = Join-Path $root "IpanelWpf.sln"
$batch = Join-Path $root "build_all.bat"
$releaseDir = Join-Path $root "Publish"
$normalizedVersion = $Version -replace '^(?i:RELEASE-)', ''
$normalizedVersion = $normalizedVersion -replace '^[vV]', ''
if ($normalizedVersion -notmatch '^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$') {
    throw "Invalid release version '$Version'. Use v1.2.3 or RELEASE-V1.2.3."
}
if (!(Test-Path $batch)) { throw "Required build script not found: $batch" }
if (!(Test-Path $solution)) { throw "Solution file not found: $solution" }

Write-Host "Restoring NuGet dependencies for IpanelWpf.sln..."
& nuget restore $solution -NonInteractive
if ($LASTEXITCODE -ne 0) { throw "NuGet solution restore failed with exit code $LASTEXITCODE" }

# These two modules are built by build_all.bat but are not restored by the
# solution-level restore above. Restore them explicitly before the batch build.
$generalDbProject = Join-Path $root "GenaralDBTools\GenaralDBTools.csproj"
if (!(Test-Path $generalDbProject)) { throw "GenaralDBTools project not found: $generalDbProject" }
Write-Host "Restoring standalone SDK project: GenaralDBTools..."
& dotnet restore $generalDbProject --verbosity minimal
if ($LASTEXITCODE -ne 0) { throw "GenaralDBTools restore failed with exit code $LASTEXITCODE" }

$rxbitProject = Join-Path $root "Rxbit.Calibration\Rxbit.Calibration.csproj"
if (!(Test-Path $rxbitProject)) { throw "Rxbit.Calibration project not found: $rxbitProject" }
$vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
if (!(Test-Path $vswhere)) { throw "Visual Studio locator not found: $vswhere" }
$msbuildCandidates = @(& $vswhere -latest -products "*" -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe")
$msbuild = $msbuildCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (!$msbuild) { throw "Visual Studio MSBuild.exe not found for Rxbit.Calibration restore." }
Write-Host "Restoring standalone .NET Framework project: Rxbit.Calibration..."
& $msbuild $rxbitProject /t:Restore /p:Configuration=Release /v:minimal /nologo
if ($LASTEXITCODE -ne 0) { throw "Rxbit.Calibration restore failed with exit code $LASTEXITCODE" }

# CPM.Recipe.Tool.csproj has a legacy absolute Newtonsoft.Json HintPath.
# Recreate that location on the runner without changing the project file.
$tempPackages = Join-Path $env:RUNNER_TEMP "legacy-nuget-packages"
& nuget install Newtonsoft.Json -Version 13.0.3 -OutputDirectory $tempPackages -NonInteractive
if ($LASTEXITCODE -ne 0) { throw "Failed to download Newtonsoft.Json 13.0.3." }
$sourceDll = Join-Path $tempPackages "Newtonsoft.Json.13.0.3\lib\net45\Newtonsoft.Json.dll"
if (!(Test-Path $sourceDll)) { throw "Expected assembly not found: $sourceDll" }
$legacyDll = "D:\tang\donet_librarys\global_package\newtonsoft.json\13.0.3\lib\net45\Newtonsoft.Json.dll"
New-Item -ItemType Directory -Path (Split-Path $legacyDll -Parent) -Force | Out-Null
Copy-Item -LiteralPath $sourceDll -Destination $legacyDll -Force

Write-Host "Running the original build_all.bat Release..."
Push-Location $root
try {
    & $batch Release
    $batchExitCode = $LASTEXITCODE
}
finally { Pop-Location }
if ($batchExitCode -ne 0) { throw "build_all.bat failed with exit code $batchExitCode. See Publish\logs." }
if (!(Test-Path $releaseDir)) { throw "Publish directory was not created." }

$expectedModules = @(
    "GenaralDBTools", "BitTomrrorApp", "CPM.Recipe.Tool", "Cmp.UTools.GUI",
    "CoderControl", "CoordinateFunction", "Elastic.BMS", "GenealogyDemo",
    "IpanelWpf", "MediaStudioVideo", "MindMap", "Radae3DDemo3", "RadarApp",
    "RebootHMI", "Rxbit.Calibration", "Tang.RadarApplication",
    "Tang.VideoThrimalgoAI", "VisioLikeMap", "WpfDevDemo", "WxChatApp"
)
$zipFiles = @(Get-ChildItem -LiteralPath $releaseDir -File -Filter "*.zip")
if ($zipFiles.Count -ne $expectedModules.Count) {
    throw "Expected $($expectedModules.Count) module ZIPs, found $($zipFiles.Count): $($zipFiles.Name -join ', ')"
}
foreach ($module in $expectedModules) {
    $matches = @($zipFiles | Where-Object { $_.BaseName -match ("^" + [regex]::Escape($module) + "-\d{8}_\d{6}$") })
    if ($matches.Count -ne 1) { throw "Expected exactly one archive for module '$module'." }
    $destination = Join-Path $releaseDir ("{0}_V{1}.zip" -f $module, $normalizedVersion)
    Move-Item -LiteralPath $matches[0].FullName -Destination $destination -Force
    Write-Host "Packaged: $(Split-Path $destination -Leaf)"
}
$checksumFile = Join-Path $releaseDir "SHA256SUMS.txt"
$lines = foreach ($file in Get-ChildItem -LiteralPath $releaseDir -File -Filter "*_V$normalizedVersion.zip" | Sort-Object Name) {
    $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $($file.Name)"
}
if (@($lines).Count -ne $expectedModules.Count) { throw "Checksum generation did not find all module packages." }
$lines | Set-Content -LiteralPath $checksumFile -Encoding UTF8
Write-Host "All 20 module packages are ready."
Get-ChildItem -LiteralPath $releaseDir -File | Where-Object { $_.Name -like "*_V$normalizedVersion.zip" -or $_.Name -eq "SHA256SUMS.txt" } | Sort-Object Name | ForEach-Object { Write-Host $_.Name }
