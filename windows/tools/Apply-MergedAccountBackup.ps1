param(
    [Parameter(Mandatory = $true)][string]$MergedBackupPath,
    [Parameter(Mandatory = $true)][string]$AccountDirectory,
    [Parameter(Mandatory = $true)][string]$ExpectedNickname
)

$ErrorActionPreference = 'Stop'
if (Get-Process -Name 'Tomatotodo.Windows' -ErrorAction SilentlyContinue) {
    throw 'Tomatotodo 仍在运行；请先正常关闭软件，避免覆盖内存中的数据。'
}
$account = (Resolve-Path -LiteralPath $AccountDirectory).Path
$profilePath = Join-Path $account 'UserProfile.json'
$profile = Get-Content -LiteralPath $profilePath -Raw -Encoding UTF8 | ConvertFrom-Json -AsHashtable
if ($profile.Nickname -ne $ExpectedNickname) { throw '目标账户昵称与预期不符。' }
$merged = Get-Content -LiteralPath $MergedBackupPath -Raw -Encoding UTF8 | ConvertFrom-Json -AsHashtable -Depth 100
if ($merged.Format -ne 'tomatotodo-native-backup' -or $merged.Version -ne 1) { throw '合并备份格式无效。' }
$settings = $merged.Data
$timetable = $settings.CourseSchedule
if ($null -eq $timetable -or $timetable.Format -ne 'tomatotodo-course-schedule') { throw '合并备份缺少有效课程表。' }
$settings.Remove('CourseSchedule')

$settingsPath = Join-Path $account 'AppSettings.json'
$timetablePath = Join-Path $account 'Timetable.json'
$settingsTemp = Join-Path $account 'AppSettings.json.migration-tmp'
$timetableTemp = Join-Path $account 'Timetable.json.migration-tmp'
if ((Test-Path -LiteralPath $settingsTemp) -or (Test-Path -LiteralPath $timetableTemp)) {
    throw '存在上次迁移临时文件，停止以便检查。'
}
$encoding = [System.Text.UTF8Encoding]::new($false)
[System.IO.File]::WriteAllText($settingsTemp, ($settings | ConvertTo-Json -Depth 100), $encoding)
[System.IO.File]::WriteAllText($timetableTemp, ($timetable | ConvertTo-Json -Depth 100), $encoding)
[System.IO.File]::Move($settingsTemp, $settingsPath, $true)
[System.IO.File]::Move($timetableTemp, $timetablePath, $true)
"Imported merged data for $ExpectedNickname."
