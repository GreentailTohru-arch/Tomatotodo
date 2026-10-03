import 'dart:convert';

import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:tomatotodo/core/account/cloud_account.dart';
import 'package:tomatotodo/core/account/local_profile.dart';
import 'package:tomatotodo/core/account/unified_user_data.dart';
import 'package:tomatotodo/core/settings/app_settings.dart';
import 'package:tomatotodo/features/dashboard/dashboard_controller.dart';
import 'package:tomatotodo/features/general/general_state.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  test('cloud sync preserves offline edits, rebases in-flight changes and isolates local account', () async {
    SharedPreferences.setMockInitialValues({});
    final prefs = await SharedPreferences.getInstance();
    final settings = AppSettings(prefs), general = GeneralState(prefs);
    final dashboard = DashboardController(prefs);
    final profile = LocalProfile(prefs);
    await profile.ready;
    String? vault;
    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
        .setMockMethodCallHandler(
          const MethodChannel('com.tomatotodo/cloud_vault'),
          (call) async {
            if (call.method == 'write') {
              vault = call.arguments as String?;
              return null;
            }
            return vault;
          },
        );
    var offline = false, race = false, rejectVersion = false, version = 0;
    final legacyPreset = UnifiedUserData.stableId('legacy-preset');
    var doc = <String, dynamic>{
      'Presets': [
        {
          'Id': legacyPreset,
          'Name': '旧版清单',
          'Tasks': <dynamic>[],
          'Repeat': 'weekly',
          'RepeatDays': ['Monday', 'Wednesday'],
          'RepeatInterval': 1,
          'RepeatUnit': 'week',
        },
      ],
      'ActivePresetId': legacyPreset,
    };
    final user = UnifiedUserData.stableId('account');
    JsonMap response() => {
      'app_settings': doc,
      'timetable': doc['shared']?['timetable'] ?? {},
      'user_profile': {'Nickname': '云端用户'},
      'version': version,
      'updated_at': '2026-09-30T00:00:00Z',
    };
    final client = MockClient((request) async {
      if (offline) throw http.ClientException('offline');
      if (request.url.path == '/api/auth/login') {
        return http.Response(
          jsonEncode({
            'access_token': 'test-only-token',
            'expires_in': 3600,
            'user': {'id': user, 'email': 'test@example.com'},
          }),
          200,
        );
      }
      if (request.url.path == '/api/auth/logout') {
        return http.Response('{}', 200);
      }
      if (request.url.path == '/api/sync/pull') {
        return http.Response(
          jsonEncode(response()),
          200,
          headers: {"content-type": "application/json; charset=utf-8"},
        );
      }
      if (request.url.path == '/api/sync/push') {
        final body = jsonDecode(request.body);
        if (rejectVersion) {
          rejectVersion = false;
          version++;
          return http.Response('{"detail":"conflict"}', 409);
        }
        expect(body['base_version'], version);
        doc = UnifiedUserData.clone(body['app_settings']);
        version++;
        if (race) {
          race = false;
          dashboard.addTask('上传途中添加');
        }
        return http.Response(
          jsonEncode(response()),
          200,
          headers: {"content-type": "application/json; charset=utf-8"},
        );
      }
      return http.Response('{}', 404);
    });
    final cloud = CloudAccount(
      settings,
      dashboard,
      general,
      profile,
      client: client,
    );
    addTearDown(() {
      cloud.dispose();
      dashboard.dispose();
      general.dispose();
      settings.dispose();
      profile.dispose();
    });
    dashboard.addTask('仅本地任务');
    final localBefore = cloud.capture();
    await cloud.signIn(email: 'test@example.com', password: 'Test!123');
    expect(dashboard.tasks.where((t) => t.title == '仅本地任务'), isEmpty);
    expect(vault, isNotNull);
    expect(dashboard.presets.first.repeatDays, [1, 3]);
    await cloud.synchronize(manual: true);
    final stableVersion = version;
    await cloud.synchronize(manual: true);
    expect(
      version,
      stableVersion,
      reason: 'unchanged data must not create another upload',
    );
    offline = true;
    dashboard.addTask('离线添加');
    await cloud.synchronize();
    expect(cloud.status, contains('本机修改已保留'));
    offline = false;
    race = true;
    rejectVersion = true;
    await cloud.synchronize(manual: true);
    final titles = (doc['shared']['tasks']['Presets'] as List)
        .expand((p) => p['Tasks'] as List)
        .map((t) => t['Title']);
    expect(titles, containsAll(['离线添加', '上传途中添加']));
    doc['shared']['timer']['FocusMinutes'] = 40;
    version++;
    await cloud.synchronize(manual: true);
    expect(
      dashboard.focusMinutes,
      40,
      reason: 'remote rhythm must replace paused timer duration',
    );
    final base = UnifiedUserData.clone(doc);
    dashboard.addTask('本机冲突任务');
    doc['shared']['tasks']['Presets'][0]['Name'] = '电脑修改清单';
    version++;
    var asked = 0;
    cloud.resolveConflicts = (_) async {
      asked++;
      return null;
    };
    await cloud.synchronize();
    await cloud.synchronize();
    expect(asked, 1);
    cloud.resolveConflicts = (_) async => false;
    await cloud.synchronize(manual: true);
    expect(dashboard.presets.first.name, '电脑修改清单');
    expect(base['platforms']['mobile'], doc['platforms']['mobile']);
    final imported = cloud.capture();
    final logId = UnifiedUserData.stableId('restore-fixture');
    imported['shared']['archive']['records'][logId] = {
      'Id': logId,
      'StartedAt': '2026-09-30T00:00:00.000000Z',
      'Seconds': 60,
      'CompletedPomodoro': false,
      'TaskId': null,
      'TaskTitle': '备份日志',
    };
    await cloud.importDocument(imported);
    final cleared = cloud.capture();
    cleared['shared']['archive']['records'] = <String, dynamic>{};
    await cloud.importDocument(cleared);
    expect(cloud.capture()['shared']['archive']['deleted'], contains(logId));
    await cloud.importDocument(imported);
    expect((cloud.capture()['shared']['archive']['records'] as Map).length, 1);
    expect(
      (cloud.capture()['shared']['archive']['records'] as Map).containsKey(
        logId,
      ),
      isFalse,
    );
    await cloud.signOut();
    expect(cloud.isCloud, isFalse);
    expect(vault, isNull);
    expect(cloud.capture()['shared']['tasks'], localBefore['shared']['tasks']);
  });
}
