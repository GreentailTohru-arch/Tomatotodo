param(
  [switch]$Setup
)

$ErrorActionPreference = "Stop"
$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$BuildRoot = Join-Path $PSScriptRoot "build"
$DependencyRoot = Join-Path $PSScriptRoot "deps"
$PackageRoot = Join-Path $DependencyRoot "webview2"
$AppRoot = Join-Path $BuildRoot "Tomatotodo"
$PayloadStage = Join-Path $BuildRoot "payload\Tomatotodo"
$PayloadZip = Join-Path $BuildRoot "TomatotodoPayload.zip"
$WebViewVersion = "1.0.4078.44"
$AppVersion = "1.2.0"
$NugetPackage = Join-Path $DependencyRoot "Microsoft.Web.WebView2.$WebViewVersion.nupkg"

function Reset-Directory([string]$Path) {
  if (Test-Path -LiteralPath $Path) {
    Remove-Item -LiteralPath $Path -Recurse -Force
  }
  New-Item -ItemType Directory -Path $Path -Force | Out-Null
}

if (-not (Test-Path -LiteralPath (Join-Path $ProjectRoot "dist\client\index.html"))) {
  throw "dist/client/index.html is missing. Run pnpm run build first."
}

New-Item -ItemType Directory -Path $DependencyRoot -Force | Out-Null
if (-not (Test-Path -LiteralPath $NugetPackage)) {
  $Url = "https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/$WebViewVersion/microsoft.web.webview2.$WebViewVersion.nupkg"
  Invoke-WebRequest -Uri $Url -OutFile $NugetPackage
}
if (-not (Test-Path -LiteralPath (Join-Path $PackageRoot "lib\net462\Microsoft.Web.WebView2.Core.dll"))) {
  $ZipPath = Join-Path $DependencyRoot "webview2.zip"
  Copy-Item -LiteralPath $NugetPackage -Destination $ZipPath -Force
  Expand-Archive -LiteralPath $ZipPath -DestinationPath $PackageRoot -Force
  Remove-Item -LiteralPath $ZipPath -Force
}

Reset-Directory $AppRoot
New-Item -ItemType Directory -Path (Join-Path $AppRoot "www") -Force | Out-Null

$Core = Join-Path $PackageRoot "lib\net462\Microsoft.Web.WebView2.Core.dll"
$WinForms = Join-Path $PackageRoot "lib\net462\Microsoft.Web.WebView2.WinForms.dll"
$Loader = Join-Path $PackageRoot "build\native\x64\WebView2Loader.dll"
$Icon = Join-Path $ProjectRoot "public\icons\Tomatotodo.ico"
$Compiler = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"

& $Compiler /nologo /target:winexe /platform:x64 /optimize+ `
  /win32manifest:"$PSScriptRoot\Tomatotodo.manifest" `
  /win32icon:"$Icon" `
  /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll `
  /reference:"$Core" /reference:"$WinForms" `
  /out:"$AppRoot\Tomatotodo.exe" "$PSScriptRoot\Tomatotodo.cs"
if ($LASTEXITCODE -ne 0) { throw "Tomatotodo.exe compilation failed." }

Copy-Item -LiteralPath $Core,$WinForms,$Loader -Destination $AppRoot -Force
Copy-Item -LiteralPath (Join-Path $PackageRoot "LICENSE.txt") -Destination (Join-Path $AppRoot "WebView2-LICENSE.txt") -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "Tomatotodo.exe.config") -Destination $AppRoot -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "installer\Uninstall-Tomatotodo.ps1") -Destination $AppRoot -Force
Copy-Item -Path (Join-Path $ProjectRoot "dist\client\*") -Destination (Join-Path $AppRoot "www") -Recurse -Force

$Verify = Start-Process -FilePath (Join-Path $AppRoot "Tomatotodo.exe") -ArgumentList "--verify" -WorkingDirectory $AppRoot -PassThru -WindowStyle Hidden
$Verify.WaitForExit()
if ($Verify.ExitCode -ne 0) { throw "Native app verification failed with exit code $($Verify.ExitCode)." }

Write-Host "Windows app created: $AppRoot"

if ($Setup) {
  Reset-Directory $PayloadStage
  Copy-Item -Path (Join-Path $AppRoot "*") -Destination $PayloadStage -Recurse -Force
  Compress-Archive -Path $PayloadStage -DestinationPath $PayloadZip -CompressionLevel Optimal -Force

  $SetupPath = Join-Path $BuildRoot "Tomatotodo-Setup-$AppVersion.exe"
  & $Compiler /nologo /target:winexe /platform:x64 /optimize+ `
    /win32manifest:"$PSScriptRoot\Tomatotodo.manifest" `
    /win32icon:"$Icon" `
    /reference:System.dll /reference:System.Core.dll /reference:System.Windows.Forms.dll `
    /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll /reference:Microsoft.CSharp.dll `
    "/resource:$PayloadZip,TomatotodoPayload.zip" `
    /out:"$SetupPath" "$PSScriptRoot\TomatotodoInstaller.cs"
  if ($LASTEXITCODE -ne 0) { throw "Setup compilation failed." }
  Write-Host "Single-file setup created: $SetupPath"
}
