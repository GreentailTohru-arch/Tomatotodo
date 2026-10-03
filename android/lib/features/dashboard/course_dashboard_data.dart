import '../general/course_schedule.dart';

class CourseOccurrence {
  const CourseOccurrence({
    required this.event,
    required this.start,
    required this.end,
  });

  final CourseEvent event;
  final DateTime start;
  final DateTime end;

  bool isOngoing(DateTime now) => !start.isAfter(now) && end.isAfter(now);
}

class CourseDashboardData {
  const CourseDashboardData._();

  static List<CourseOccurrence> today(CourseSchedule schedule, DateTime now) =>
      _occurrences(schedule)
          .where(
            (item) =>
                item.start.year == now.year &&
                item.start.month == now.month &&
                item.start.day == now.day,
          )
          .toList();

  static CourseOccurrence? next(CourseSchedule schedule, DateTime now) {
    for (final item in _occurrences(schedule)) {
      if (item.end.isAfter(now)) return item;
    }
    return null;
  }

  static List<CourseOccurrence> _occurrences(CourseSchedule schedule) {
    final items = <CourseOccurrence>[
      for (final week in schedule.weeks)
        for (final event in week.events)
          CourseOccurrence(
            event: event,
            start: _dateTime(week.monday, event.day, event.startTime),
            end: _dateTime(week.monday, event.day, event.endTime),
          ),
    ];
    items.sort((a, b) => a.start.compareTo(b.start));
    return items;
  }

  static DateTime _dateTime(DateTime monday, int day, String time) {
    final date = monday.add(Duration(days: day));
    return DateTime(
      date.year,
      date.month,
      date.day,
      int.parse(time.substring(0, 2)),
      int.parse(time.substring(3, 5)),
    );
  }
}
