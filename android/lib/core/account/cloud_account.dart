import 'package:tomatotodo/core/localization/ui_text.dart';
import 'dart:async';
import 'dart:convert';
import 'dart:math';

import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart';
import 'package:http/http.dart' as http;

import '../settings/app_settings.dart';
import '../../features/dashboard/dashboard_controller.dart';
import '../../features/general/general_state.dart';
import 'local_profile.dart';
import 'unified_user_data.dart';

class CloudApiException implements Exception {
  CloudApiException(this.status, this.message);
  final int status;
  final String message;
  @override
  String toString() => message;
}

class CloudAccount extends ChangeNotifier {
  CloudAccount(
    this.settings,
    this.dashboard,
    this.general,
    this.profile, {
    http.Client? client,
    Uri? endpoint,
  }) : _http = client ?? http.Client(),
       endpoint = endpoint ?? Uri.parse('https://47.245.57.193:8443/') {
    if (this.endpoint.scheme != 'https') throw ArgumentError(UiText.t("云端账户必须使用 HTTPS"));
    for (final source in [settings, dashboard, general, profile]) {
      source.addListener(_changed);
    }
    _document = _read('unified_user_data') ?? UnifiedUserData.empty();
    _poll = Timer.periodic(
      Duration(seconds: 20),
      (_) => unawaited(synchronize()),
    );
  }
  final AppSettings settings;
  final DashboardController dashboard;
  final GeneralState general;
  final LocalProfile profile;
  final http.Client _http;
  final Uri endpoint;
  static final _vault = MethodChannel('com.tomatotodo/cloud_vault');
  JsonMap? _session;
  late JsonMap _document;
  Timer? _debounce, _poll;
  bool _applying = false, busy = false, _disposed = false;
  bool _switching = false;
  String? _deferredConflict;
  bool _lastSyncSucceeded = false;
  String status = UiText.t("本地账户 · 数据保存在此设备");
  bool get isCloud => _session != null;
  String get email => _session?['email'] as String? ?? '';
  String? get userId => _session?['id'] as String?;
  Future<bool?> Function(List<JsonMap>)? resolveConflicts;
  JsonMap? _read(String key) {
    final raw = settings.preferences.getString(key);
    return raw == null ? null : jsonDecode(raw) as JsonMap;
  }

  Future<void> _write(String key, Object value) =>
      settings.preferences.setString(key, jsonEncode(value));
  void _announce(String message) {
    if (_disposed) return;
    status = message;
    notifyListeners();
  }

  JsonMap capture() {
    final previous = _document;
    _document = UnifiedUserData.capture(
      dashboard: dashboard.exportData(),
      general: general.exportData(),
      appearance: settings.exportData(),
      profile: profile.exportData(),
      previous: _document,
    );
    if (!UnifiedUserData.equal(previous, _document)) {
      unawaited(_write('unified_user_data', _document));
    }
    return UnifiedUserData.clone(_document);
  }

  void _changed() {
    if (_applying || _disposed) return;
    final previous = _document;
    final current = capture();
    if (!isCloud || UnifiedUserData.equal(previous, current)) return;
    unawaited(_write('cloud_cache_$userId', current));
    _announce(UiText.t("修改已保存在本机，等待同步"));
    _debounce?.cancel();
    _debounce = Timer(
      Duration(seconds: 1),
      () => unawaited(synchronize()),
    );
  }

  Future<JsonMap> _request(
    String path, {
    JsonMap? body,
    bool authorized = true,
  }) async {
    if (authorized && path != 'api/auth/refresh' && _session != null) {
      final expires = DateTime.tryParse(
        _session!['expiresAt']?.toString() ?? '',
      );
      if (expires != null &&
          expires.isBefore(DateTime.now().add(Duration(minutes: 5))) &&
          expires.isAfter(DateTime.now())) {
        try {
          final refreshed = await _request('api/auth/refresh', body: {});
          _session!['token'] = refreshed['access_token'];
          _session!['expiresAt'] = DateTime.now()
              .toUtc()
              .add(Duration(seconds: refreshed['expires_in'] as int))
              .toIso8601String();
          await _vault.invokeMethod('write', jsonEncode(_session));
        } on CloudApiException catch (e) {
          if (e.status != 404) rethrow;
        }
      }
    }
    final headers = {
      'Content-Type': 'application/json',
      if (authorized && _session != null)
        'Authorization': 'Bearer ${_session!['token']}',
    };
    final uri = endpoint.resolve(path);
    final response =
        await (body == null
                ? _http.get(uri, headers: headers)
                : _http.post(uri, headers: headers, body: jsonEncode(body)))
            .timeout(Duration(seconds: 20));
    final data = response.body.isEmpty
        ? <String, dynamic>{}
        : jsonDecode(response.body);
    if (response.statusCode >= 300) {
      throw CloudApiException(
        response.statusCode,
        data is Map
            ? (data['detail'] is Map
                          ? data['detail']['message']
                          : data['detail'])
                      ?.toString() ??
                  UiText.t("云端请求失败")
            : UiText.t("云端请求失败"),
      );
    }
    return Map<String, dynamic>.from(data as Map);
  }

  Future<void> restore() async {
    try {
      await profile.ready;
      if (_disposed) return;
      final recovery = _read('cloud_apply_recovery');
      if (recovery != null) await apply(recovery);
      final raw = await _vault.invokeMethod<String>('read');
      if (raw == null) return;
      if (_disposed) return;
      _session = jsonDecode(raw) as JsonMap;
      _announce(UiText.t("正在连接云端"));
      await synchronize();
    } on MissingPluginException {
      /* Widget tests and unsupported hosts keep local mode. */
    } catch (_) {
      _announce(UiText.t("云端会话暂不可用，请重新登录"));
    }
  }

  Future<void> signIn({
    required String email,
    required String password,
    String? nickname,
    String? activationCode,
  }) async {
    if (busy) return;
    await profile.ready;
    if (dashboard.isRunning) dashboard.toggleRunning();
    busy = true;
    _announce(UiText.t("正在登录"));
    final oldSession = _session;
    final oldDocument = capture();
    try {
      if (nickname != null) {
        final rawCode = activationCode!
            .replaceAll(RegExp(r'[^a-zA-Z0-9]'), '')
            .toUpperCase();
        if (rawCode.length != 25) throw ArgumentError(UiText.t("激活码应为 25 位"));
        final formattedCode = List.generate(
          5,
          (i) => rawCode.substring(i * 5, i * 5 + 5),
        ).join('-');
        await _request(
          'api/auth/register',
          authorized: false,
          body: {
            'email': email.trim(),
            'password': password,
            'nickname': nickname.trim(),
            'activation_code': formattedCode,
          },
        );
      }
      var device = settings.preferences.getString('cloud_device_id');
      device ??= UnifiedUserData.stableId(
        '${DateTime.now().microsecondsSinceEpoch}:${Random.secure().nextInt(1 << 32)}',
      );
      await settings.preferences.setString('cloud_device_id', device);
      final response = await _request(
        'api/auth/login',
        authorized: false,
        body: {
          'email': email.trim(),
          'password': password,
          'device_id': device,
        },
      );
      if (oldSession != null && oldSession['id'] != response['user']['id']) {
        throw StateError(UiText.t("请先退出当前账户后再切换"));
      }
      if (oldSession == null) await _write('cloud_local_data', capture());
      _session = {
        'id': response['user']['id'],
        'email': response['user']['email'],
        'token': response['access_token'],
        'expiresAt': DateTime.now()
            .toUtc()
            .add(Duration(seconds: response['expires_in'] as int))
            .toIso8601String(),
      };
      final remote = await _request('api/sync/pull');
      final cache = _read('cloud_cache_$userId');
      final remoteDoc = _remoteDocument(remote);
      final doc = cache ?? remoteDoc;
      await apply(doc);
      if (cache == null) {
        await _write('cloud_base_$userId', remoteDoc);
      }
      await _vault.invokeMethod('write', jsonEncode(_session));
      _announce(UiText.t("已登录云端账户"));
    } catch (_) {
      _session = oldSession;
      await apply(oldDocument);
      rethrow;
    } finally {
      busy = false;
      notifyListeners();
    }
    _deferredConflict = null;
    // Allow the login dialog to close before showing a conflict dialog.
    _debounce?.cancel();
    _debounce = Timer(
      Duration(milliseconds: 400),
      () => unawaited(synchronize()),
    );
  }

  JsonMap _remoteDocument(JsonMap response) {
    final value = UnifiedUserData.clone(response['app_settings'] as Map);
    if (value['format'] == 'tomatotodo-user-data') {
      UnifiedUserData.validate(value);
      return value;
    }
    // Upgrade older desktop snapshots without throwing away desktop-only fields.
    final local = capture();
    final doc = UnifiedUserData.empty();
    final s = doc['shared'];
    final presets =
        value['Presets'] as List? ??
        [
          {
            'Id': UnifiedUserData.stableId('cloud-default:$userId'),
            'Name': UiText.t("我的一天"),
            'Tasks': <dynamic>[],
            'Repeat': 'none',
            'RepeatDays': <dynamic>[],
            'RepeatInterval': 1,
            'RepeatUnit': 'week',
          },
        ];
    final logs = value['FocusLogs'] as List? ?? [];
    s['tasks'] = {
      'Presets': presets,
      'ActivePresetId': value['ActivePresetId'] ?? presets.first['Id'],
      'ActiveTaskId': value['ActiveTaskId'],
      'ArchivedTasks': value['ArchivedTasks'] ?? [],
    };
    for (final p in presets) {
      p['RepeatDays'] = (p['RepeatDays'] as List? ?? [])
          .map(
            (d) => d is int
                ? d
                : [
                    'Sunday',
                    'Monday',
                    'Tuesday',
                    'Wednesday',
                    'Thursday',
                    'Friday',
                    'Saturday',
                  ].indexOf(d.toString()),
          )
          .where((d) => d >= 0)
          .toList();
      for (final t in p['Tasks']) {
        t['CompletedPomodoros'] =
            ((t['CompletedPomodoros'] as int? ?? 0) -
                    logs
                        .where(
                          (l) =>
                              l['TaskId'] == t['Id'] &&
                              l['CompletedPomodoro'] == true,
                        )
                        .length)
                .clamp(0, 1000000);
      }
    }
    s['timer'] = {
      'FocusMinutes': value['FocusMinutes'] ?? 25,
      'BreakMinutes': value['BreakMinutes'] ?? 5,
      'EnableShortBreak': value['EnableShortBreak'] ?? true,
      'PositiveCountup': value['PositiveCountup'] ?? false,
    };
    s['countdown'] = {
      'CountdownName': value['CountdownName'] ?? UiText.t("假期结束"),
      'CountdownDate': value['CountdownDate'],
    };
    s['archive'] = {
      'records': {for (final log in logs) log['Id']: log},
      'deleted': <String>[],
    };
    Object? camel(Object? node) {
      if (node is Map) {
        return {
          for (final e in node.entries)
            '${e.key.toString()[0].toLowerCase()}${e.key.toString().substring(1)}':
                camel(e.value),
        };
      }
      if (node is List) return node.map(camel).toList();
      return node;
    }

    final timetable = response['timetable'] as Map;
    s['timetable'] = timetable.isEmpty
        ? {
            'format': 'tomatotodo-course-schedule',
            'version': 1,
            'term': {'name': UiText.t("大三上学期"), 'timezone': 'Asia/Shanghai'},
            'weeks': List.generate(
              16,
              (i) => {
                'date': DateTime(
                  2026,
                  9,
                  7,
                ).add(Duration(days: i * 7)).toIso8601String().substring(0, 10),
                'events': <dynamic>[],
              },
            ),
          }
        : camel(timetable);
    s['profile'] = {
      'Nickname': response['user_profile']['Nickname'] ?? UiText.t("用户"),
      'Biography': response['user_profile']['Biography'] ?? '',
      'AvatarBase64': null,
    };
    doc['platforms']['windows'] = UnifiedUserData.clone(value)
      ..removeWhere(
        (key, _) => [
          'Presets',
          'ActivePresetId',
          'ActiveTaskId',
          'ArchivedTasks',
          'FocusLogs',
          'FocusMinutes',
          'BreakMinutes',
          'EnableShortBreak',
          'PositiveCountup',
          'CountdownName',
          'CountdownDate',
          'CourseSchedule',
          'UnifiedData',
          'LocalMusicFolderToken',
          'WeatherLocationConsent',
          'DeliveredCourseReminders',
          'CachedWeatherJson',
          'CachedFocusQuotes',
        ].contains(key),
      );
    doc['platforms']['mobile'] = local['platforms']['mobile'];
    for (final key in (s as Map).keys) {
      doc['changes'][key] = {
        'platform': 'windows',
        'at': response['updated_at'] ?? '1970-01-01T00:00:00.000000Z',
      };
    }
    // Canonicalize legacy dates, nullable fields and weekday values once.
    final decoded = UnifiedUserData.decode(
      doc,
      dashboard: dashboard.exportData(),
      general: general.exportData(),
      appearance: settings.exportData(),
    );
    final normalized = UnifiedUserData.capture(
      dashboard: decoded['dashboard'],
      general: decoded['general'],
      appearance: decoded['personalization'],
      profile: s['profile'],
      previous: doc,
    );
    normalized['changes'] = doc['changes'];
    return normalized;
  }

  Future<void> apply(JsonMap doc, {bool preserveRuntime = false}) async {
    final data = UnifiedUserData.decode(
      doc,
      dashboard: dashboard.exportData(),
      general: general.exportData(),
      appearance: settings.exportData(),
    );
    DashboardController.validateExportData(data['dashboard']);
    GeneralState.validateExportData(data['general']);
    AppSettings.validateExportData(data['personalization']);
    final before = capture();
    final safeRuntime =
        preserveRuntime &&
        UnifiedUserData.equal(
          before['shared']['timer'],
          doc['shared']['timer'],
        ) &&
        before['shared']['tasks']['ActiveTaskId'] ==
            doc['shared']['tasks']['ActiveTaskId'] &&
        before['shared']['tasks']['ActivePresetId'] ==
            doc['shared']['tasks']['ActivePresetId'];
    final rollback = UnifiedUserData.decode(
      before,
      dashboard: dashboard.exportData(),
      general: general.exportData(),
      appearance: settings.exportData(),
    );
    final avatar = doc['shared']['profile']?['AvatarBase64'];
    if (avatar is String) base64Decode(avatar);
    await _write('cloud_apply_recovery', before);
    _applying = true;
    try {
      await dashboard.replaceData(
        data['dashboard'],
        preserveRuntime: safeRuntime,
      );
      await general.replaceData(data['general']);
      await settings.replaceData(data['personalization']);
      if (doc['shared']['profile'] is Map) {
        await profile.applyData(doc['shared']['profile']);
      }
      _document = UnifiedUserData.clone(doc);
      await _write('unified_user_data', _document);
      if (userId != null) await _write('cloud_cache_$userId', _document);
      await settings.preferences.remove('cloud_apply_recovery');
    } catch (_) {
      await dashboard.replaceData(rollback['dashboard'], preserveRuntime: true);
      await general.replaceData(rollback['general']);
      await settings.replaceData(rollback['personalization']);
      await profile.applyData(before['shared']['profile']);
      _document = before;
      await _write('unified_user_data', before);
      rethrow;
    } finally {
      _applying = false;
    }
  }

  Future<void> synchronize({bool manual = false, bool? preferLocal}) async {
    if (busy && manual) throw StateError(UiText.t("同步正在进行，请稍后重试"));
    if (!isCloud || busy || _disposed || _switching) return;
    busy = true;
    final identity = userId;
    _lastSyncSucceeded = false;
    try {
      for (var attempt = 0; attempt < 4; attempt++) {
        final local = capture();
        await _write('cloud_cache_$identity', local);
        final baseline =
            _read('cloud_base_$identity') ?? UnifiedUserData.empty();
        final response = await _request('api/sync/pull');
        final remote = _remoteDocument(response);
        var mergeBase = baseline;
        final mergeLocal = UnifiedUserData.clone(local);
        if (preferLocal == true) {
          mergeBase = remote;
          for (final entry in (remote['platforms'] as Map).entries) {
            if (entry.key != 'mobile') {
              mergeLocal['platforms'][entry.key] = entry.value;
            }
          }
        }
        var merged = UnifiedUserData.merge(mergeBase, mergeLocal, remote);
        if (merged.conflicts.isNotEmpty) {
          final signature = jsonEncode([local['changes'], remote['changes']]);
          if (!manual &&
              preferLocal == null &&
              _deferredConflict == signature) {
            return;
          }
          final choice =
              preferLocal ?? await resolveConflicts?.call(merged.conflicts);
          if (choice == null) {
            _deferredConflict = signature;
            _announce(UiText.t("存在同步冲突，保留两端数据，等待选择"));
            return;
          }
          if (!UnifiedUserData.equal(local, capture())) continue;
          // Keep both versions even after a user resolves a conflict.
          await _write('cloud_conflict_backup_$identity', {
            'local': local,
            'remote': remote,
          });
          merged = UnifiedUserData.merge(
            baseline,
            local,
            remote,
            useLocal: choice,
          );
        }
        if (!UnifiedUserData.equal(local, capture())) continue;
        if (dashboard.isRunning &&
            (!UnifiedUserData.equal(
                  local['shared']['tasks'],
                  merged.document['shared']['tasks'],
                ) ||
                !UnifiedUserData.equal(
                  local['shared']['timer'],
                  merged.document['shared']['timer'],
                ))) {
          _announce(UiText.t("云端有新任务或计时设置，将在暂停后同步"));
          return;
        }
        if (!UnifiedUserData.equal(remote, merged.document) ||
            response['app_settings']['format'] != 'tomatotodo-user-data') {
          try {
            final shared = merged.document['shared'];
            await _request(
              'api/sync/push',
              body: {
                'base_version': response['version'],
                'mode': 'replace',
                'app_settings': merged.document,
                'timetable': shared['timetable'],
                'user_profile': {
                  'Nickname': shared['profile']?['Nickname'] ?? profile.name,
                  'Biography': shared['profile']?['Biography'] ?? '',
                },
              },
            );
          } on CloudApiException catch (error) {
            if (error.status == 409) continue;
            rethrow;
          }
        }
        if (!UnifiedUserData.equal(local, capture())) {
          // Rebase edits made while the request was in flight onto its acknowledged result.
          final latest = capture();
          final rebased = UnifiedUserData.merge(
            local,
            latest,
            merged.document,
            useLocal: true,
          ).document;
          await apply(rebased, preserveRuntime: true);
          await _write('cloud_base_$identity', merged.document);
          continue;
        }
        if (!UnifiedUserData.equal(local, merged.document)) {
          await apply(merged.document, preserveRuntime: true);
        }
        _document = merged.document;
        await _write('cloud_base_$identity', merged.document);
        await _write('cloud_cache_$identity', merged.document);
        _lastSyncSucceeded = true;
        _announce(
          UiText.f("已同步 · {0}", [DateTime.now().toLocal().toString().substring(11, 16)]),
        );
        return;
      }
      _announce(UiText.t("数据仍在更新，稍后自动重试"));
    } catch (error) {
      _announce(
        error is CloudApiException &&
                (error.status == 401 || error.status == 403)
            ? UiText.t("登录已失效，请重新登录；本机修改已保留")
            : UiText.t("暂未同步，本机修改已保留，将自动重试"),
      );
      if (manual) rethrow;
    } finally {
      busy = false;
      if (!_disposed) notifyListeners();
    }
  }

  Future<void> importDocument(JsonMap document) async {
    if (busy) throw StateError(UiText.t("同步正在进行，请稍后导入"));
    final previous = capture();
    await _write('cloud_import_backup', previous);
    final next = UnifiedUserData.clone(document);
    final platforms = next['platforms'] as Map;
    for (final entry in (previous['platforms'] as Map).entries) {
      platforms.putIfAbsent(entry.key, () => entry.value);
    }
    final archive = next['shared']['archive'];
    final deleted = <String>{
      ...List<String>.from(previous['shared']['archive']['deleted']),
      ...List<String>.from(archive['deleted']),
    };
    for (final id in (previous['shared']['archive']['records'] as Map).keys) {
      if (!(archive['records'] as Map).containsKey(id)) {
        deleted.add(id as String);
      }
    }
    final records = archive['records'] as Map;
    for (final id in records.keys.toList()) {
      if (deleted.contains(id)) {
        final row = records.remove(id);
        final restoredId = UnifiedUserData.stableId(
          'restore:$id:${DateTime.now().microsecondsSinceEpoch}:${Random.secure().nextInt(1 << 32)}',
        );
        row['Id'] = restoredId;
        records[restoredId] = row;
      }
    }
    archive['deleted'] = deleted.toList()..sort();
    for (final key in (next['shared'] as Map).keys) {
      if (!UnifiedUserData.equal(next['shared'][key], previous['shared'][key])) {
        next['changes'][key] = {
          'platform': 'mobile',
          'at': DateTime.now().toUtc().toIso8601String(),
        };
      }
    }
    await apply(next);
  }

  Future<void> migrateLocalToCloud() async {
    if (!isCloud) throw StateError(UiText.t("请先登录云端账户"));
    if (busy) throw StateError(UiText.t("同步正在进行，请稍后迁移"));
    final local = _read('cloud_local_data');
    if (local == null) throw StateError(UiText.t("没有可迁移的本地数据"));
    await _write('cloud_migration_backup_$userId', capture());
    final next = UnifiedUserData.clone(_document);
    next['shared'] = local['shared'];
    next['platforms']['mobile'] = local['platforms']['mobile'];
    next['changes'] = local['changes'];
    await apply(next);
    await synchronize(manual: true, preferLocal: true);
  }

  Future<void> copyCloudToLocal() async {
    if (!isCloud) throw StateError(UiText.t("请先登录云端账户"));
    if (busy) throw StateError(UiText.t("同步正在进行，请稍后迁移"));
    await synchronize(manual: true);
    if (!_lastSyncSucceeded) throw StateError(status);
    await _write('cloud_local_backup', _read('cloud_local_data') ?? {});
    await _write('cloud_local_data', capture());
  }

  Future<void> signOut() async {
    if (busy) return;
    if (dashboard.isRunning) dashboard.toggleRunning();
    await synchronize();
    _switching = true;
    _debounce?.cancel();
    try {
      final id = userId;
      if (id != null) await _write('cloud_cache_$id', capture());
      try {
        await _request('api/auth/logout', body: {});
      } catch (_) {}
      await _vault.invokeMethod('write', null);
      _session = null;
      final local = _read('cloud_local_data');
      if (local != null) await apply(local);
      _announce(UiText.t("本地账户 · 云端账户的未同步修改已保留"));
    } finally {
      _switching = false;
    }
  }

  @override
  void dispose() {
    _disposed = true;
    _debounce?.cancel();
    _poll?.cancel();
    for (final source in [settings, dashboard, general, profile]) {
      source.removeListener(_changed);
    }
    _http.close();
    super.dispose();
  }
}
