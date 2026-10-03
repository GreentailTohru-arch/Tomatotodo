import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:tomatotodo/core/account/unified_user_data.dart';
import 'package:tomatotodo/core/settings/app_settings.dart';
import 'package:tomatotodo/features/dashboard/dashboard_controller.dart';
import 'package:tomatotodo/features/general/general_state.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  test(
    'shared data survives both adapters, with log merge and conflict choice',
    () async {
      SharedPreferences.setMockInitialValues({});
      final prefs = await SharedPreferences.getInstance();
      final dashboard = DashboardController(prefs);
      final general = GeneralState(prefs);
      final settings = AppSettings(prefs);
      addTearDown(dashboard.dispose);
      addTearDown(general.dispose);
      addTearDown(settings.dispose);
      dashboard.addTask('测试任务', estimate: 3);
      JsonMap capture({JsonMap? previous}) => UnifiedUserData.capture(
        dashboard: dashboard.exportData(),
        general: general.exportData(),
        appearance: settings.exportData(),
        profile: {'Nickname': '测试', 'Biography': '', 'AvatarBase64': null},
        previous: previous,
      );
      var doc = capture();
      var decoded = UnifiedUserData.decode(
        doc,
        dashboard: dashboard.exportData(),
        general: general.exportData(),
        appearance: settings.exportData(),
      );
      await dashboard.replaceData(decoded['dashboard']);
      expect(UnifiedUserData.equal(doc, capture(previous: doc)), isTrue);
      final base = capture(previous: doc),
          local = UnifiedUserData.clone(base),
          remote = UnifiedUserData.clone(base);
      local['shared']['timer']['FocusMinutes'] = 30;
      remote['shared']['timer']['FocusMinutes'] = 40;
      expect(UnifiedUserData.merge(base, local, remote).conflicts.length, 1);
      expect(
        UnifiedUserData.merge(
          base,
          local,
          remote,
          useLocal: false,
        ).document['shared']['timer']['FocusMinutes'],
        40,
      );
      final id = UnifiedUserData.stableId('log');
      local['shared']['archive']['records'][id] = {'Id': id, 'Seconds': 60};
      remote['shared']['archive']['records'][id] = {'Id': id, 'Seconds': 60};
      expect(
        UnifiedUserData.merge(
          base,
          local,
          remote,
        ).document['shared']['archive']['records'].length,
        1,
      );
      remote['shared']['archive']['deleted'] = [id];
      expect(
        UnifiedUserData.merge(
          base,
          local,
          remote,
        ).document['shared']['archive']['records'],
        isEmpty,
      );
      const fixturePath = String.fromEnvironment('SYNC_FIXTURE');
      const outputPath = String.fromEnvironment('SYNC_OUTPUT');
      if (fixturePath.isNotEmpty) {
        doc = jsonDecode(File(fixturePath).readAsStringSync()) as JsonMap;
        decoded = UnifiedUserData.decode(
          doc,
          dashboard: dashboard.exportData(),
          general: general.exportData(),
          appearance: settings.exportData(),
        );
        await dashboard.replaceData(decoded['dashboard']);
        await general.replaceData(decoded['general']);
        await settings.replaceData(decoded['personalization']);
        final roundtrip = UnifiedUserData.capture(
          dashboard: dashboard.exportData(),
          general: general.exportData(),
          appearance: settings.exportData(),
          profile: doc['shared']['profile'],
          previous: doc,
        );
        expect(
          roundtrip['shared'],
          doc['shared'],
          reason: 'Mobile must not create false shared edits',
        );
        expect(roundtrip['platforms']['windows'], doc['platforms']['windows']);
        if (outputPath.isNotEmpty) {
          File(outputPath).writeAsStringSync(jsonEncode(roundtrip));
        }
      }
    },
  );
}
