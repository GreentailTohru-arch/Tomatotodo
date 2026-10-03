import 'dart:convert';

import 'package:crypto/crypto.dart';

typedef JsonMap = Map<String, dynamic>;

/// Same v2 protocol and three-way rules as Accounts/UnifiedUserData.cs.
class UnifiedUserData {
  static JsonMap clone(Map value) => jsonDecode(jsonEncode(value)) as JsonMap;
  static JsonMap empty() => {
    'format': 'tomatotodo-user-data',
    'version': 2,
    'shared': <String, dynamic>{},
    'platforms': <String, dynamic>{},
    'changes': <String, dynamic>{},
  };
  static String stableId(String value) {
    if (RegExp(
      r'^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$',
    ).hasMatch(value)) {
      return value.toLowerCase();
    }
    final hex = md5.convert(utf8.encode('tomatotodo:$value')).toString();
    return '${hex.substring(0, 8)}-${hex.substring(8, 12)}-${hex.substring(12, 16)}-${hex.substring(16, 20)}-${hex.substring(20)}';
  }

  static bool equal(Object? a, Object? b) {
    if (a is Map && b is Map) {
      return a.length == b.length &&
          a.keys.every((k) => b.containsKey(k) && equal(a[k], b[k]));
    }
    if (a is List && b is List) {
      return a.length == b.length &&
          List.generate(a.length, (i) => i).every((i) => equal(a[i], b[i]));
    }
    return a == b;
  }

  static String? utc(Object? value) {
    if (value == null) return null;
    final at = DateTime.parse(value.toString()).toUtc();
    final raw = at.toIso8601String();
    return '${raw.substring(0, raw.indexOf('.'))}.${(at.millisecond * 1000 + at.microsecond).toString().padLeft(6, '0')}Z';
  }

  static JsonMap capture({
    required Map dashboard,
    required Map general,
    required Map appearance,
    required Map profile,
    JsonMap? previous,
  }) {
    final doc = clone(previous ?? empty());
    void put(String key, Object value, {bool platform = false}) {
      final group = doc[platform ? 'platforms' : 'shared'] as Map;
      if (equal(group[key], value)) return;
      group[key] = value;
      doc['changes'][key] = {
        'platform': 'mobile',
        'at': DateTime.now().toUtc().toIso8601String(),
      };
    }

    final presets = (dashboard['presets'] as List)
        .map(
          (p) => <String, dynamic>{
            'Id': stableId(p['id']),
            'Name': p['name'],
            'DueAt': utc(p['dueAt']),
            'RemindAt': utc(p['remindAt']),
            'Repeat': p['repeat'],
            'RepeatInterval': p['repeatInterval'],
            'RepeatUnit': p['repeatUnit'],
            'RepeatDays': (p['repeatDays'] as List)
                .map((d) => d == 7 ? 0 : d)
                .toList(),
            'Tasks': (dashboard['tasks'] as List)
                .where((t) => t['presetId'] == p['id'])
                .map(
                  (t) => {
                    'Id': stableId(t['id']),
                    'Title': t['title'],
                    'Subtitle': t['subtitle'],
                    'EstimatedPomodoros': t['estimate'] == 0
                        ? null
                        : t['estimate'],
                    'CompletedPomodoros': t['completedBaseline'] ?? 0,
                    'IsComplete': t['done'],
                  },
                )
                .toList(),
          },
        )
        .toList();
    final oldHistory = doc['shared']['tasks']?['ArchivedTasks'] as List? ?? [];
    final history = (dashboard['taskHistory'] as List).map((h) {
      final id = stableId(h['id']);
      final old = oldHistory.where((e) => e['Id'] == id).firstOrNull;
      return old ??
          {
            'Id': id,
            'PresetId': stableId('default'),
            'Task': {
              'Id': id,
              'Title': h['title'],
              'Subtitle': '',
              'IsComplete': h['completed'],
              'CompletedPomodoros': 0,
            },
            'ArchivedAt': utc(h['at']),
            'Status': h['status'],
          };
    }).toList();
    put('tasks', {
      'Presets': presets,
      'ActivePresetId': stableId(dashboard['activePresetId']),
      'ActiveTaskId': dashboard['activeTaskId'] == null
          ? null
          : stableId(dashboard['activeTaskId']),
      'ArchivedTasks': history,
    });
    put('timer', {
      'FocusMinutes': dashboard['focusMinutes'],
      'BreakMinutes': dashboard['shortMinutes'],
      'EnableShortBreak': dashboard['shortBreakEnabled'],
      'PositiveCountup': dashboard['countUp'],
      'AutomaticCycle': dashboard['automaticCycle'] ?? true,
    });
    put('countdown', {
      'CountdownName': dashboard['countdownName'],
      'CountdownDate': utc(dashboard['countdownDate']),
    });
    put('timetable', general['schedule']);
    put('profile', profile);
    final records = <String, dynamic>{};
    for (final log in dashboard['logs'] as List) {
      final at = DateTime.parse(log['at']).toUtc();
      final id = stableId(
        log['id'] ??
            'log:${at.toIso8601String()}:${log['seconds']}:${log['taskId']}:${log['completedSession']}',
      );
      records[id] = {
        'Id': id,
        'StartedAt': utc(
          at
              .subtract(Duration(seconds: log['seconds'] as int))
              .toIso8601String(),
        ),
        'Seconds': log['seconds'],
        'CompletedPomodoro': log['completedSession'],
        'TaskId': log['taskId'] == null ? null : stableId(log['taskId']),
        'TaskTitle': log['taskTitle'],
      };
    }
    final oldArchive = doc['shared']['archive'] as Map? ?? {};
    final deleted = <String>{
      ...?(oldArchive['deleted'] as List?)?.cast<String>(),
    };
    for (final id in (oldArchive['records'] as Map? ?? {}).keys) {
      if (!records.containsKey(id)) deleted.add(id as String);
    }
    for (final id in deleted) {
      records.remove(id);
    }
    put('archive', {'records': records, 'deleted': deleted.toList()..sort()});
    final mobileGeneral = clone(general)..remove('schedule');
    put('mobile', {
      'general': mobileGeneral,
      'personalization': appearance,
      'visibleTiles': dashboard['visibleTiles'],
      'tileSizes': dashboard['tileSizes'],
    }, platform: true);
    return doc;
  }

  static void validate(JsonMap doc) {
    if (doc['format'] != 'tomatotodo-user-data' ||
        doc['version'] != 2 ||
        doc['shared'] is! Map ||
        doc['platforms'] is! Map ||
        doc['changes'] is! Map) {
      throw FormatException('不是 Tomatotodo 跨平台用户数据 v2');
    }
    final s = doc['shared'] as Map;
    if (s['tasks']?['Presets'] is! List ||
        (s['tasks']['Presets'] as List).isEmpty ||
        s['archive']?['records'] is! Map ||
        s['archive']?['deleted'] is! List ||
        s['timetable'] is! Map) {
      throw FormatException('备份缺少清单、档案或课表');
    }
    final ids = <String>{};
    for (final p in s['tasks']['Presets']) {
      if (p['Id'] is! String ||
          !ids.add(p['Id']) ||
          p['Name'] is! String ||
          p['Tasks'] is! List) {
        throw FormatException('清单数据无效');
      }
      for (final t in p['Tasks']) {
        if (t['Id'] is! String ||
            t['Title'] is! String ||
            (t['Title'] as String).trim().isEmpty) {
          throw FormatException('任务数据无效');
        }
      }
    }
    for (final row in (s['archive']['records'] as Map).values) {
      if (row['Seconds'] is! int ||
          row['Seconds'] <= 0 ||
          DateTime.tryParse(row['StartedAt'] ?? '') == null) {
        throw FormatException('专注日志无效');
      }
    }
    if (s['timer']?['FocusMinutes'] is! int ||
        s['timer']['FocusMinutes'] < 1 ||
        s['timer']['FocusMinutes'] > 180 ||
        s['timer']['BreakMinutes'] is! int ||
        s['timer']['BreakMinutes'] < 1 ||
        s['timer']['BreakMinutes'] > 60) {
      throw FormatException('计时长度无效');
    }
  }

  static JsonMap decode(
    JsonMap doc, {
    required Map dashboard,
    required Map general,
    required Map appearance,
  }) {
    validate(doc);
    final s = doc['shared'];
    final mobile = doc['platforms']['mobile'] as Map? ?? {};
    final lists = s['tasks']['Presets'] as List;
    return clone({
      'dashboard': {
        ...dashboard,
        'phase': 'focus',
        'focusMinutes': s['timer']['FocusMinutes'],
        'shortMinutes': s['timer']['BreakMinutes'],
        'shortBreakEnabled': s['timer']['EnableShortBreak'],
        'countUp': s['timer']['PositiveCountup'],
        'automaticCycle': s['timer']['AutomaticCycle'] ?? true,
        'activePresetId': s['tasks']['ActivePresetId'] ?? lists.first['Id'],
        'activeTaskId': s['tasks']['ActiveTaskId'],
        'presets': lists
            .map(
              (p) => {
                'id': p['Id'],
                'name': p['Name'],
                'dueAt': p['DueAt'],
                'remindAt': p['RemindAt'],
                'repeat': p['Repeat'],
                'repeatInterval': p['RepeatInterval'],
                'repeatUnit': p['RepeatUnit'],
                'repeatDays': (p['RepeatDays'] as List? ?? [])
                    .map((d) => d == 0 ? 7 : d)
                    .toList(),
              },
            )
            .toList(),
        'tasks': [
          for (final p in lists)
            for (final t in p['Tasks'])
              {
                'id': t['Id'],
                'presetId': p['Id'],
                'title': t['Title'],
                'subtitle': t['Subtitle'] ?? '',
                'estimate': t['EstimatedPomodoros'] ?? 0,
                'done': t['IsComplete'] == true,
                'completedBaseline': t['CompletedPomodoros'] ?? 0,
              },
        ],
        'taskHistory': [
          for (final h in s['tasks']['ArchivedTasks'] as List? ?? [])
            {
              'id': h['Id'],
              'title': h['Task']['Title'],
              'completed': h['Task']['IsComplete'] == true,
              'at': h['ArchivedAt'],
              'status': h['Status'],
            },
        ],
        'logs': [
          for (final row in (s['archive']['records'] as Map).values)
            {
              'id': row['Id'],
              'at': DateTime.parse(row['StartedAt'])
                  .add(Duration(seconds: row['Seconds']))
                  .toIso8601String(),
              'seconds': row['Seconds'],
              'completedSession': row['CompletedPomodoro'],
              'taskId': row['TaskId'],
              'taskTitle': row['TaskTitle'] ?? '未指定任务',
            },
        ],
        'countdownName': s['countdown']['CountdownName'] ?? '',
        'countdownDate': s['countdown']['CountdownDate'],
        'visibleTiles': mobile['visibleTiles'] ?? dashboard['visibleTiles'],
        'tileSizes': mobile['tileSizes'] ?? dashboard['tileSizes'],
      },
      'general': {
        ...general,
        ...?mobile['general'] as Map?,
        'schedule': s['timetable'],
      },
      'personalization': mobile['personalization'] ?? appearance,
    });
  }

  static ({JsonMap document, List<JsonMap> conflicts}) merge(
    JsonMap baseline,
    JsonMap local,
    JsonMap remote, {
    bool? useLocal,
  }) {
    final result = clone(remote);
    final conflicts = <JsonMap>[];
    for (final group in ['shared', 'platforms']) {
      final keys = <String>{
        ...(local[group] as Map).keys.cast<String>(),
        ...(remote[group] as Map).keys.cast<String>(),
      };
      for (final key in keys) {
        final b = baseline[group][key],
            l = local[group][key],
            r = remote[group][key];
        if (key == 'archive') {
          final deleted = <String>{};
          final records = <String, dynamic>{};
          for (final archive in [r, l]) {
            if (archive is! Map) continue;
            deleted.addAll((archive['deleted'] as List? ?? []).cast<String>());
            for (final entry in (archive['records'] as Map? ?? {}).entries) {
              if (records[entry.key] == null ||
                  entry.value['Seconds'] > records[entry.key]['Seconds']) {
                records[entry.key as String] = entry.value;
              }
            }
          }
          for (final id in deleted) {
            records.remove(id);
          }
          result[group][key] = {
            'records': records,
            'deleted': deleted.toList()..sort(),
          };
          continue;
        }
        if (equal(l, b) || equal(l, r)) continue;
        final conflict = !equal(r, b);
        if (conflict) {
          conflicts.add({
            'section': key,
            'local': local['changes'][key],
            'remote': remote['changes'][key],
          });
        }
        if (!conflict || useLocal == true) {
          result[group][key] = l;
          result['changes'][key] = local['changes'][key];
        }
      }
    }
    return (document: clone(result), conflicts: conflicts);
  }
}
