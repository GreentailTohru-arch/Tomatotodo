param(
    [Parameter(Mandatory = $true)][string]$InputPath,
    [Parameter(Mandatory = $true)][string]$OutputPath
)

$ErrorActionPreference = 'Stop'
$legacy = Get-Content -LiteralPath $InputPath -Raw -Encoding UTF8 | ConvertFrom-Json -AsHashtable -Depth 100
if ($legacy.format -ne 'tomatotodo-user-data' -or $legacy.version -ne 1) {
    throw '输入文件不是受支持的 Tomatotodo 旧版用户备份。'
}

function StableGuid([string]$kind, [string]$value) {
    $bytes = [System.Security.Cryptography.SHA256]::HashData([System.Text.Encoding]::UTF8.GetBytes("legacy:${kind}:$value"))
    return [guid]::new([byte[]]$bytes[0..15]).ToString()
}
function NativeTask($task) {
    return [ordered]@{
        Id = StableGuid 'task' ([string]$task.id)
        Title = [string]$task.title
        Subtitle = [string]$task.subtitle
        EstimatedPomodoros = if ($null -ne $task.estimatedPomodoros -and [int]$task.estimatedPomodoros -gt 0) { [int]$task.estimatedPomodoros } else { $null }
        CompletedPomodoros = [int]$task.completedPomodoros
        IsComplete = [bool]$task.done
    }
}
function LocalDateOffset([string]$value) {
    if ([string]::IsNullOrWhiteSpace($value)) { return $null }
    return ([DateTimeOffset]::new([datetime]::ParseExact($value, 'yyyy-MM-dd', [cultureinfo]::InvariantCulture), [timespan]::FromHours(8))).ToString('o')
}

$source = $legacy.data
$presets = @()
foreach ($list in $source.configuration.taskLists) {
    $repeat = $list.repeat
    $days = @()
    foreach ($day in $repeat.weekdays) {
        $days += [int]$day
    }
    $presets += [ordered]@{
        Id = StableGuid 'preset' ([string]$list.id)
        Name = [string]$list.name
        Tasks = @($list.tasks | ForEach-Object { NativeTask $_ })
        DueAt = LocalDateOffset ([string]$list.dueDate)
        RemindAt = if ($list.reminder) { ([DateTimeOffset]::Parse([string]$list.reminder)).ToString('o') } else { $null }
        Repeat = if ($repeat.type -eq 'weekly') { 'weekly' } elseif ($repeat.type -eq 'custom') { 'custom' } else { 'none' }
        RepeatInterval = [Math]::Max(1, [int]$repeat.interval)
        RepeatUnit = if ($repeat.unit) { [string]$repeat.unit } else { 'week' }
        RepeatDays = $days
        LastRemindedAt = $null
    }
}
$history = @()
foreach ($entry in $source.configuration.taskHistory) {
    $history += [ordered]@{
        Id = StableGuid 'history' ([string]$entry.id)
        PresetId = StableGuid 'preset' ([string]$entry.sourceListId)
        Task = NativeTask $entry.task
        ArchivedAt = ([DateTimeOffset]::Parse([string]$entry.archivedAt)).ToString('o')
        Status = if ($entry.status -eq 'expired') { 'expired' } else { 'completed' }
    }
}
$logs = @()
foreach ($entry in $source.archive.focusLogs) {
    $end = [DateTimeOffset]::ParseExact("$($entry.date) $($entry.time)", 'yyyy-MM-dd HH:mm', [cultureinfo]::InvariantCulture).ToOffset([timespan]::FromHours(8))
    $logs += [ordered]@{
        Id = StableGuid 'focus' ([string]$entry.id)
        TaskId = if ($null -ne $entry.taskId) { StableGuid 'task' ([string]$entry.taskId) } else { $null }
        StartedAt = $end.AddSeconds(-[int]$entry.seconds).ToString('o')
        Seconds = [int]$entry.seconds
        CompletedPomodoro = $entry.reason -eq 'completed'
    }
}

$course = $source.general.courseScheduleData
$weeks = @()
foreach ($week in $course.weeks) {
    $events = @()
    foreach ($event in $week.events) {
        $events += [ordered]@{
            Day = [int]$event.day; Start = [int]$event.start; End = [int]$event.end
            StartTime = [string]$event.startTime; EndTime = [string]$event.endTime
            Name = [string]$event.name; Room = [string]$event.room; Teacher = [string]$event.teacher
            Tone = [string]$event.tone; PeriodPart = [string]$event.periodPart
        }
    }
    $weeks += [ordered]@{ Date = [string]$week.date; Events = $events }
}
$visible = [ordered]@{}
foreach ($key in $source.dashboard.gridLayout.visible.Keys) { $visible[$key] = [bool]$source.dashboard.gridLayout.visible[$key] }
$scale = if ($source.personalization.fontScaleEnabled) { [int]$source.personalization.fontScale } else { 100 }
$data = [ordered]@{
    Version = 1
    Presets = $presets
    ActivePresetId = StableGuid 'preset' ([string]$source.configuration.activeListId)
    ActiveTaskId = if ($null -ne $source.configuration.activeTaskId) { StableGuid 'task' ([string]$source.configuration.activeTaskId) } else { $null }
    FocusLogs = $logs
    ArchivedTasks = $history
    FocusMinutes = [int]$source.general.timerSettings.focus
    BreakMinutes = [int]$source.general.timerSettings.short
    EnableShortBreak = [bool]$source.general.timerSettings.shortBreakEnabled
    PositiveCountup = [bool]$source.general.timerSettings.countUpEnabled
    ImmersiveMode = [bool]$source.general.immersive
    MiniWindowMode = $false
    ShowClockMarkers = [bool]$source.general.analogClockSettings.showMarkers
    ShowClockSeconds = [bool]$source.general.analogClockSettings.showSeconds
    CountdownName = [string]$source.general.countdown.label
    CountdownDate = LocalDateOffset ([string]$source.general.countdown.date)
    CourseReminderEnabled = $false
    CourseReminderLeadMinutes = 10
    CourseReminderSystemToast = $true
    CourseReminderAppMessage = $true
    CourseReminderSound = $true
    DeliveredCourseReminders = [ordered]@{}
    MusicMode = 'system'
    WeatherLocationConsent = $false
    CachedWeatherJson = $null
    CachedFocusQuotes = @()
    LocalMusicFolderToken = $null
    Theme = if ($source.personalization.appearanceMode -in @('auto','dark','light')) { [string]$source.personalization.appearanceMode } else { 'auto' }
    AccentColor = '#30643B'
    AccentMode = 'manual'
    RecentAccentColors = @('#30643B','#00B294','#8E7B22','#E62117','#0078D7')
    PureBlack = [bool]$source.personalization.pureBlack
    UiScale = [Math]::Clamp($scale, 85, 125)
    QuickNote = ''
    CourseScheduleEnabled = [bool]$source.general.courseScheduleEnabled
    CourseSchedule = [ordered]@{
        Format = 'tomatotodo-course-schedule'; Version = 1
        Term = [ordered]@{ Name = [string]$course.term.name; Timezone = [string]$course.term.timezone }
        Weeks = $weeks
    }
    DashboardLayout = [ordered]@{ Order = @($source.dashboard.gridLayout.order); Visible = $visible }
}
$native = [ordered]@{ Format = 'tomatotodo-native-backup'; Version = 1; Data = $data }
$json = $native | ConvertTo-Json -Depth 100
[System.IO.File]::WriteAllText($OutputPath, $json, [System.Text.UTF8Encoding]::new($false))
if ($source.notes.Count -gt 0) {
    $notesPath = [System.IO.Path]::Combine(
        [System.IO.Path]::GetDirectoryName($OutputPath),
        [System.IO.Path]::GetFileNameWithoutExtension($OutputPath) + '-legacy-notes.json')
    $notesJson = @{ Format = 'tomatotodo-legacy-notes'; SourceBackup = [System.IO.Path]::GetFileName($InputPath); Notes = @($source.notes) } | ConvertTo-Json -Depth 100
    [System.IO.File]::WriteAllText($notesPath, $notesJson, [System.Text.UTF8Encoding]::new($false))
}
"Converted $($presets.Count) presets, $($history.Count) archived tasks, $($logs.Count) focus logs, and $($weeks.Count) course weeks; preserved $($source.notes.Count) legacy notes separately."
