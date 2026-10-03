import 'dart:math' as math;

/// Calculates the next local occurrence after a preset deadline.
DateTime? nextPresetDue(
  DateTime current, {
  required String repeat,
  required int interval,
  required String unit,
  required List<int> weekdays,
}) {
  final step = math.max(1, interval);
  DateTime addMonths(int amount) {
    final first = DateTime(current.year, current.month + amount, 1);
    final lastDay = DateTime(first.year, first.month + 1, 0).day;
    return DateTime(
      first.year,
      first.month,
      math.min(current.day, lastDay),
      current.hour,
      current.minute,
    );
  }

  switch (repeat) {
    case 'daily':
      return current.add(Duration(days: 1));
    case 'workday':
      var next = current.add(Duration(days: 1));
      while (next.weekday > 5) {
        next = next.add(Duration(days: 1));
      }
      return next;
    case 'weekly':
      return current.add(Duration(days: 7));
    case 'monthly':
      return addMonths(1);
    case 'yearly':
      return addMonths(12);
    case 'custom':
      if (unit == 'day') return current.add(Duration(days: step));
      if (unit == 'month') return addMonths(step);
      if (unit == 'year') return addMonths(step * 12);
      if (weekdays.isEmpty) return current.add(Duration(days: step * 7));
      final sorted = weekdays.toSet().where((d) => d >= 1 && d <= 7).toList()
        ..sort();
      if (sorted.isEmpty) return current.add(Duration(days: step * 7));
      for (final day in sorted) {
        if (day > current.weekday) {
          return current.add(Duration(days: day - current.weekday));
        }
      }
      final monday = current.subtract(Duration(days: current.weekday - 1));
      return monday.add(Duration(days: step * 7 + sorted.first - 1));
    default:
      return null;
  }
}
