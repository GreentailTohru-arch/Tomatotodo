param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'output'),
    [string]$CompilerPath = (Join-Path (Split-Path $PSScriptRoot -Parent) 'tools\InnoSetup\ISCC.exe')
)
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    if (!(Test-Path -LiteralPath $CompilerPath)) { throw 'Provide the Inno Setup 6 ISCC.exe path using -CompilerPath.' }
    dotnet publish Tomatotodo.Windows.csproj -c Release -p:Platform=x64 -p:WindowsPackageType=None -p:EnableWinAppRunSupport=false -p:WindowsAppSDKSelfContained=true -p:SelfContained=true -p:PublishReadyToRun=false -o packaging/publish-1.6.2-final
    if ($LASTEXITCODE -ne 0) { throw 'Client publish failed.' }
    & $CompilerPath "/O$OutputDirectory" packaging/Tomatotodo.iss
    if ($LASTEXITCODE -ne 0) { throw 'Installer compile failed.' }
} finally { Pop-Location }
