param(
    [Parameter(Mandatory = $true)][string]$NativeBackupPath,
    [Parameter(Mandatory = $true)][string]$AccountDirectory,
    [Parameter(Mandatory = $true)][string]$OutputPath
)

$ErrorActionPreference = 'Stop'
$incoming = Get-Content -LiteralPath $NativeBackupPath -Raw -Encoding UTF8 | ConvertFrom-Json -AsHashtable -Depth 100
if ($incoming.Format -ne 'tomatotodo-native-backup' -or $incoming.Version -ne 1) { throw '不是受支持的原生备份。' }
$current = Get-Content -LiteralPath (Join-Path $AccountDirectory 'AppSettings.json') -Raw -Encoding UTF8 | ConvertFrom-Json -AsHashtable -Depth 100
$timetable = Get-Content -LiteralPath (Join-Path $AccountDirectory 'Timetable.json') -Raw -Encoding UTF8 | ConvertFrom-Json -AsHashtable -Depth 100
$current.CourseSchedule = $timetable
$source = $incoming.Data

function ListGroup([string]$name) {
    if ($name.Contains('我的一天')) { return 'today' }
    if ($name.Contains('重要')) { return 'important' }
    if ($name.Contains('计划内')) { return 'planned' }
    return $name.Trim()
}

$presetMap = @{}
$taskMap = @{}
foreach ($preset in $source.Presets) {
    $group = ListGroup ([string]$preset.Name)
    $matching = @($current.Presets | Where-Object { (ListGroup ([string]$_.Name)) -eq $group })
    if ($matching.Count -gt 0) {
        $target = $matching[0]
        $presetMap[[string]$preset.Id] = [string]$target.Id
        if ($null -eq $target.DueAt) { $target.DueAt = $preset.DueAt }
        if ($null -eq $target.RemindAt) { $target.RemindAt = $preset.RemindAt }
        if ($target.Repeat -eq 'none' -and $preset.Repeat -ne 'none') {
            foreach ($key in @('Repeat','RepeatInterval','RepeatUnit','RepeatDays')) { $target[$key] = $preset[$key] }
        }
        foreach ($task in $preset.Tasks) {
            $same = @($target.Tasks | Where-Object { $_.Title -eq $task.Title })
            if ($same.Count -gt 0) {
                $existing = $same[0]
                $taskMap[[string]$task.Id] = [string]$existing.Id
                if ([string]::IsNullOrWhiteSpace([string]$existing.Subtitle)) { $existing.Subtitle = $task.Subtitle }
                if ($null -eq $existing.EstimatedPomodoros) { $existing.EstimatedPomodoros = $task.EstimatedPomodoros }
                $existing.CompletedPomodoros = [Math]::Max([int]$existing.CompletedPomodoros, [int]$task.CompletedPomodoros)
                $existing.IsComplete = [bool]$existing.IsComplete -or [bool]$task.IsComplete
            } else {
                $target.Tasks += $task
                $taskMap[[string]$task.Id] = [string]$task.Id
            }
        }
    } else {
        $current.Presets += $preset
        $presetMap[[string]$preset.Id] = [string]$preset.Id
        foreach ($task in $preset.Tasks) { $taskMap[[string]$task.Id] = [string]$task.Id }
    }
}

$knownLogs = [System.Collections.Generic.HashSet[string]]::new([string[]]@($current.FocusLogs | ForEach-Object { [string]$_.Id }))
foreach ($log in $source.FocusLogs) {
    if ($log.TaskId -and $taskMap.ContainsKey([string]$log.TaskId)) { $log.TaskId = $taskMap[[string]$log.TaskId] }
    if ($knownLogs.Add([string]$log.Id)) { $current.FocusLogs += $log }
}
$knownHistory = [System.Collections.Generic.HashSet[string]]::new([string[]]@($current.ArchivedTasks | ForEach-Object { [string]$_.Id }))
foreach ($history in $source.ArchivedTasks) {
    if ($presetMap.ContainsKey([string]$history.PresetId)) { $history.PresetId = $presetMap[[string]$history.PresetId] }
    if ($taskMap.ContainsKey([string]$history.Task.Id)) { $history.Task.Id = $taskMap[[string]$history.Task.Id] }
    if ($knownHistory.Add([string]$history.Id)) { $current.ArchivedTasks += $history }
}

# Keep account identity, avatar, native-only preferences and current active task.
# Bring across old user-facing appearance, timer, layout, countdown and the populated timetable.
foreach ($key in @('FocusMinutes','BreakMinutes','EnableShortBreak','PositiveCountup','ImmersiveMode',
        'ShowClockMarkers','ShowClockSeconds','CountdownName','CountdownDate','Theme','AccentColor',
        'AccentMode','PureBlack','UiScale','DashboardLayout','CourseScheduleEnabled')) {
    $current[$key] = $source[$key]
}
if (-not $current.ActivePresetId -and $source.ActivePresetId) {
    $current.ActivePresetId = $presetMap[[string]$source.ActivePresetId]
}
if (-not $current.ActiveTaskId -and $source.ActiveTaskId) {
    $current.ActiveTaskId = $taskMap[[string]$source.ActiveTaskId]
}
$current.CourseSchedule = $source.CourseSchedule
$merged = [ordered]@{ Format = 'tomatotodo-native-backup'; Version = 1; Data = $current }
[System.IO.File]::WriteAllText($OutputPath, ($merged | ConvertTo-Json -Depth 100), [System.Text.UTF8Encoding]::new($false))
"Merged $($current.Presets.Count) presets, $($current.ArchivedTasks.Count) history items, $($current.FocusLogs.Count) focus logs and $($current.CourseSchedule.Weeks.Count) timetable weeks."
