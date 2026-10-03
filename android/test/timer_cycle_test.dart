import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:tomatotodo/features/dashboard/dashboard_controller.dart';

void main() {
  for (final auto in [false, true]) {
    for (final breaks in [false, true]) {
      testWidgets('cycle auto=$auto breaks=$breaks', (tester) async {
        SharedPreferences.setMockInitialValues({});
        final prefs = await SharedPreferences.getInstance();
        var now = DateTime(2026, 10, 3);
        final timer = DashboardController(prefs, now: () => now);
        timer.setDurations(focus: 1, shortBreak: 1, enableShortBreak: breaks);
        timer.setAutomaticCycle(auto);
        timer.toggleRunning();
        now = now.add(const Duration(minutes: 1));
        await tester.pump(const Duration(seconds: 1));
        expect(timer.completedSessions, 1);
        expect(timer.phase, breaks ? TimerPhase.shortBreak : TimerPhase.focus);
        expect(timer.isRunning, auto);
        expect(timer.exportData()['automaticCycle'], auto);
        timer.dispose();
      });
    }
  }
  testWidgets('automatic cycle stops after target and final break', (tester) async {
    SharedPreferences.setMockInitialValues({});
    final prefs = await SharedPreferences.getInstance();
    var now = DateTime(2026, 10, 3);
    final timer = DashboardController(prefs, now: () => now);
    timer.addTask('Goal', estimate: 1);
    timer.setDurations(focus: 1, shortBreak: 1, enableShortBreak: true);
    timer.toggleRunning();
    now = now.add(const Duration(minutes: 1));
    await tester.pump(const Duration(seconds: 1));
    expect(timer.isRunning, true);
    expect(timer.phase, TimerPhase.shortBreak);
    now = now.add(const Duration(minutes: 1));
    await tester.pump(const Duration(seconds: 1));
    expect(timer.isRunning, false);
    expect(timer.completedSessions, 1);
    timer.dispose();
  });
}
