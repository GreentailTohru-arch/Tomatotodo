param(
  [string]$PayloadPath = (Join-Path $PSScriptRoot "TomatotodoPayload.zip")
)

$ErrorActionPreference = "Stop"
$InstallRoot = Join-Path $env:LOCALAPPDATA "Programs\Tomatotodo"
$TempRoot = Join-Path $env:TEMP ("Tomatotodo-Install-" + [Guid]::NewGuid().ToString("N"))
$DesktopShortcut = Join-Path ([Environment]::GetFolderPath("Desktop")) "Tomatotodo.lnk"
$StartMenuFolder = Join-Path ([Environment]::GetFolderPath("Programs")) "Tomatotodo"
$StartMenuShortcut = Join-Path $StartMenuFolder "Tomatotodo.lnk"
$UninstallShortcut = Join-Path $StartMenuFolder "卸载 Tomatotodo.lnk"

try {
  Get-Process -Name "Tomatotodo" -ErrorAction SilentlyContinue | Stop-Process -Force
  New-Item -ItemType Directory -Path $TempRoot -Force | Out-Null
  Expand-Archive -LiteralPath $PayloadPath -DestinationPath $TempRoot -Force
  $SourceRoot = Join-Path $TempRoot "Tomatotodo"
  if (-not (Test-Path -LiteralPath (Join-Path $SourceRoot "Tomatotodo.exe"))) {
    throw "安装包内容不完整。"
  }

  New-Item -ItemType Directory -Path $InstallRoot -Force | Out-Null
  Copy-Item -Path (Join-Path $SourceRoot "*") -Destination $InstallRoot -Recurse -Force
  New-Item -ItemType Directory -Path $StartMenuFolder -Force | Out-Null

  $Shell = New-Object -ComObject WScript.Shell
  foreach ($ShortcutPath in @($DesktopShortcut, $StartMenuShortcut)) {
    $Shortcut = $Shell.CreateShortcut($ShortcutPath)
    $Shortcut.TargetPath = Join-Path $InstallRoot "Tomatotodo.exe"
    $Shortcut.WorkingDirectory = $InstallRoot
    $Shortcut.IconLocation = (Join-Path $InstallRoot "Tomatotodo.exe") + ",0"
    $Shortcut.Description = "Tomatotodo 番茄专注与任务清单"
    $Shortcut.Save()
  }

  $Uninstall = $Shell.CreateShortcut($UninstallShortcut)
  $Uninstall.TargetPath = "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe"
  $Uninstall.Arguments = '-NoProfile -ExecutionPolicy Bypass -File "' + (Join-Path $InstallRoot "Uninstall-Tomatotodo.ps1") + '"'
  $Uninstall.WorkingDirectory = $InstallRoot
  $Uninstall.IconLocation = (Join-Path $InstallRoot "Tomatotodo.exe") + ",0"
  $Uninstall.Save()

  $UninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Tomatotodo"
  New-Item -Path $UninstallKey -Force | Out-Null
  $SizeKb = [math]::Ceiling(((Get-ChildItem -LiteralPath $InstallRoot -Recurse -File | Measure-Object Length -Sum).Sum) / 1KB)
  New-ItemProperty -Path $UninstallKey -Name DisplayName -Value "Tomatotodo" -PropertyType String -Force | Out-Null
  New-ItemProperty -Path $UninstallKey -Name DisplayVersion -Value "1.2.0" -PropertyType String -Force | Out-Null
  New-ItemProperty -Path $UninstallKey -Name Publisher -Value "Tomatotodo" -PropertyType String -Force | Out-Null
  New-ItemProperty -Path $UninstallKey -Name DisplayIcon -Value (Join-Path $InstallRoot "Tomatotodo.exe") -PropertyType String -Force | Out-Null
  New-ItemProperty -Path $UninstallKey -Name InstallLocation -Value $InstallRoot -PropertyType String -Force | Out-Null
  New-ItemProperty -Path $UninstallKey -Name UninstallString -Value ('powershell.exe -NoProfile -ExecutionPolicy Bypass -File "' + (Join-Path $InstallRoot "Uninstall-Tomatotodo.ps1") + '"') -PropertyType String -Force | Out-Null
  New-ItemProperty -Path $UninstallKey -Name EstimatedSize -Value $SizeKb -PropertyType DWord -Force | Out-Null
  New-ItemProperty -Path $UninstallKey -Name NoModify -Value 1 -PropertyType DWord -Force | Out-Null
  New-ItemProperty -Path $UninstallKey -Name NoRepair -Value 1 -PropertyType DWord -Force | Out-Null

  Start-Process -FilePath (Join-Path $InstallRoot "Tomatotodo.exe") -WorkingDirectory $InstallRoot
  Add-Type -AssemblyName System.Windows.Forms
  [System.Windows.Forms.MessageBox]::Show("Tomatotodo 1.2.0 安装完成。", "Tomatotodo", "OK", "Information") | Out-Null
}
catch {
  Add-Type -AssemblyName System.Windows.Forms
  [System.Windows.Forms.MessageBox]::Show("安装失败：$($_.Exception.Message)", "Tomatotodo", "OK", "Error") | Out-Null
  exit 1
}
finally {
  if (Test-Path -LiteralPath $TempRoot) {
    Remove-Item -LiteralPath $TempRoot -Recurse -Force -ErrorAction SilentlyContinue
  }
}
