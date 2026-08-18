$ErrorActionPreference = "SilentlyContinue"
$InstallRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$DesktopShortcut = Join-Path ([Environment]::GetFolderPath("Desktop")) "Tomatotodo.lnk"
$StartMenuFolder = Join-Path ([Environment]::GetFolderPath("Programs")) "Tomatotodo"

Add-Type -AssemblyName System.Windows.Forms
$Choice = [System.Windows.Forms.MessageBox]::Show(
  "确定要卸载 Tomatotodo 吗？本地任务、便签和专注档案也会一并删除。",
  "卸载 Tomatotodo",
  "YesNo",
  "Warning"
)
if ($Choice -ne "Yes") { exit 0 }

Get-Process -Name "Tomatotodo" -ErrorAction SilentlyContinue | Stop-Process -Force
Remove-Item -LiteralPath $DesktopShortcut -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $StartMenuFolder -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Tomatotodo" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath (Join-Path $env:LOCALAPPDATA "Tomatotodo") -Recurse -Force -ErrorAction SilentlyContinue

$Cleanup = Join-Path $env:TEMP ("Tomatotodo-Cleanup-" + [Guid]::NewGuid().ToString("N") + ".ps1")
$QuotedRoot = $InstallRoot.Replace("'", "''")
Set-Content -LiteralPath $Cleanup -Encoding UTF8 -Value "Start-Sleep -Milliseconds 800`nRemove-Item -LiteralPath '$QuotedRoot' -Recurse -Force -ErrorAction SilentlyContinue`nRemove-Item -LiteralPath `$MyInvocation.MyCommand.Path -Force -ErrorAction SilentlyContinue"
Start-Process -FilePath "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -ArgumentList @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $Cleanup) -WindowStyle Hidden
