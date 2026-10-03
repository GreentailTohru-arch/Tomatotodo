import 'package:tomatotodo/core/localization/ui_text.dart';
import 'package:flutter/foundation.dart';

String courseDate(DateTime date) =>
    '${date.year.toString().padLeft(4, '0')}-${date.month.toString().padLeft(2, '0')}-${date.day.toString().padLeft(2, '0')}';
DateTime mondayOf(DateTime date) => DateTime(
  date.year,
  date.month,
  date.day,
).subtract(Duration(days: date.weekday - 1));

@immutable
class CourseEvent {
  const CourseEvent({
    required this.day,
    required this.start,
    required this.end,
    required this.startTime,
    required this.endTime,
    required this.name,
    this.room = '',
    this.teacher = '',
    this.tone = 'teal',
    this.periodPart = '',
  });
  final int day, start, end;
  final String startTime, endTime, name, room, teacher, tone;
  final String periodPart;
  static final _clock = RegExp(r'^([01][0-9]|2[0-3]):[0-5][0-9]$');
  static int _minutes(String clock) =>
      int.parse(clock.substring(0, 2)) * 60 + int.parse(clock.substring(3, 5));
  factory CourseEvent.fromJson(Object? value) {
    if (value is! Map) throw FormatException(UiText.t("课程条目必须是对象"));
    final day = value['day'], start = value['start'], end = value['end'];
    final startTime = value['startTime'],
        endTime = value['endTime'],
        name = value['name'];
    if (day is! int ||
        day < 0 ||
        day > 6 ||
        start is! int ||
        start < 1 ||
        end is! int ||
        end < start ||
        end > 99 ||
        startTime is! String ||
        !_clock.hasMatch(startTime) ||
        endTime is! String ||
        !_clock.hasMatch(endTime) ||
        _minutes(endTime) <= _minutes(startTime) ||
        name is! String ||
        name.trim().isEmpty ||
        name.length > 100) {
      throw FormatException(UiText.t("课程的日期、节次、时间或名称无效"));
    }
    String optional(String key) {
      final data = value[key];
      if (data == null) return '';
      if (data is! String || data.length > 100) {
        throw FormatException(UiText.f("课程 {0} 无效", [key]));
      }
      return data.trim();
    }

    return CourseEvent(
      day: day,
      start: start,
      end: end,
      startTime: startTime,
      endTime: endTime,
      name: name.trim(),
      room: optional('room'),
      teacher: optional('teacher'),
      tone: optional('tone').isEmpty ? 'teal' : optional('tone'),
      periodPart: optional('periodPart'),
    );
  }
  Map<String, Object?> toJson() => {
    'day': day,
    'start': start,
    'end': end,
    'startTime': startTime,
    'endTime': endTime,
    'name': name,
    'room': room,
    'teacher': teacher,
    'tone': tone,
    'periodPart': periodPart,
  };
}

@immutable
class CourseWeek {
  const CourseWeek({required this.monday, required this.events});
  final DateTime monday;
  final List<CourseEvent> events;
  factory CourseWeek.fromJson(Object? value) {
    if (value is! Map || value['date'] is! String || value['events'] is! List) {
      throw FormatException(UiText.t("周数据缺少 date 或 events"));
    }
    final raw = value['date'] as String;
    final date = DateTime.tryParse(raw);
    if (date == null ||
        courseDate(date) != raw ||
        date.weekday != DateTime.monday) {
      throw FormatException(UiText.t("周起始日期必须是周一 YYYY-MM-DD"));
    }
    final items = value['events'] as List;
    if (items.length > 80) throw FormatException(UiText.t("每周最多 80 门课程"));
    final events = items.map(CourseEvent.fromJson).toList()
      ..sort(
        (a, b) => a.day.compareTo(b.day) == 0
            ? a.start.compareTo(b.start)
            : a.day.compareTo(b.day),
      );
    return CourseWeek(monday: date, events: events);
  }
  Map<String, Object?> toJson() => {
    'date': courseDate(monday),
    'events': events.map((item) => item.toJson()).toList(),
  };
}

@immutable
class CourseSchedule {
  const CourseSchedule({
    required this.termName,
    required this.timezone,
    required this.weeks,
  });
  final String termName, timezone;
  final List<CourseWeek> weeks;
  factory CourseSchedule.blank() => CourseSchedule(
    termName: UiText.t("我的课程表"),
    timezone: 'Asia/Shanghai',
    weeks: [CourseWeek(monday: mondayOf(DateTime.now()), events: [])],
  );
  factory CourseSchedule.fromJson(Object? value) {
    final source =
        value is Map && value['weeks'] is! List && value['data'] is Map
        ? value['data']
        : value;
    if (source is! Map ||
        source['format'] != 'tomatotodo-course-schedule' ||
        source['version'] != 1 ||
        source['weeks'] is! List) {
      throw FormatException(UiText.t("不是受支持的 Tomatotodo 课程表 v1"));
    }
    final rawWeeks = source['weeks'] as List;
    if (rawWeeks.isEmpty || rawWeeks.length > 64) {
      throw FormatException(UiText.t("课程表需要 1 到 64 周"));
    }
    final weeks = rawWeeks.map(CourseWeek.fromJson).toList()
      ..sort((a, b) => a.monday.compareTo(b.monday));
    if (weeks.map((week) => courseDate(week.monday)).toSet().length !=
        weeks.length) {
      throw FormatException(UiText.t("课程表包含重复周"));
    }
    final term = source['term'];
    String field(String key, String fallback) {
      if (term is! Map || term[key] == null) return fallback;
      final result = term[key];
      if (result is! String || result.length > 80) {
        throw FormatException(UiText.f("term.{0} 无效", [key]));
      }
      return result.trim().isEmpty ? fallback : result.trim();
    }

    return CourseSchedule(
      termName: field('name', UiText.t("我的课程表")),
      timezone: field('timezone', 'Asia/Shanghai'),
      weeks: weeks,
    );
  }
  Map<String, Object?> toJson() => {
    'format': 'tomatotodo-course-schedule',
    'version': 1,
    'term': {'name': termName, 'timezone': timezone},
    'weeks': weeks.map((week) => week.toJson()).toList(),
  };
  Map<String, Object?> template() => {
    ...toJson(),
    'weeks': weeks
        .map((week) => {'date': courseDate(week.monday), 'events': <Object>[]})
        .toList(),
    'eventSchema': {
      'day': UiText.t("0 到 6 对应周一到周日"),
      'start': UiText.t("起始节次"),
      'end': UiText.t("结束节次"),
      'startTime': UiText.t("24 小时 HH:mm"),
      'endTime': UiText.t("24 小时 HH:mm"),
      'name': UiText.t("课程全称"),
      'room': UiText.t("教室"),
      'teacher': UiText.t("教师"),
      'tone': UiText.t("主题颜色名"),
    },
  };
  Iterable<({CourseEvent event, DateTime start})> futureEvents(
    DateTime now,
  ) sync* {
    for (final week in weeks) {
      for (final event in week.events) {
        final date = week.monday.add(Duration(days: event.day));
        final hour = int.parse(event.startTime.substring(0, 2)),
            minute = int.parse(event.startTime.substring(3, 5));
        final start = DateTime(date.year, date.month, date.day, hour, minute);
        if (start.isAfter(now)) yield (event: event, start: start);
      }
    }
  }
}
