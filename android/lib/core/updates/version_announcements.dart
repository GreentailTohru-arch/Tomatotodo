import 'package:tomatotodo/core/localization/ui_text.dart';
import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';

import 'app_updates.dart';

class VersionAnnouncements {
  VersionAnnouncements(this.preferences, {http.Client? client})
    : client = client ?? http.Client();
  final SharedPreferences preferences;
  final http.Client client;
  static final readKey = 'announcements.read_ids';
  bool _started = false;

  bool _showing = false;
  Future<void> showStartup(
    BuildContext context,
    String version, {
    bool manual = false,
  }) async {
    if (_showing || (!manual && _started)) return;
    _showing = true;
    if (!manual) _started = true;
    var shown = 0;
    try {
      final response = await client
          .get(
            Uri.https('47.245.57.193:8443', '/api/announcements', {
              'platform': 'android',
              'version': version,
            }),
          )
          .timeout(Duration(seconds: 12));
      if (response.statusCode != 200) throw FormatException(UiText.t("加载失败"));
      final rows = jsonDecode(response.body) as List;
      final read = preferences.getStringList(readKey)?.toSet() ?? <String>{};
      for (final item in rows) {
        final row = item as Map<String, dynamic>;
        final id = row['id'] as String;
        final urgent = row['severity'] == 'urgent';
        if (row['active'] != true ||
            row['platform'] != 'android' ||
            compareVersions(row['version'] as String, version) != 0 ||
            (!manual && !urgent && read.contains(id))) {
          continue;
        }
        if (!context.mounted) return;
        final dark = Theme.of(context).brightness == Brightness.dark;
        final color = dark
            ? (urgent ? Color(0xffffb4ab) : Color(0xff7cdb9b))
            : urgent
            ? Color(0xffba1a1a)
            : Color(0xff16834a);
        shown++;
        final acknowledged = await showDialog<bool>(
          context: context,
          barrierDismissible: false,
          builder: (dialog) => PopScope(
            canPop: false,
            child: AlertDialog(
              title: Row(
                children: [
                  Icon(
                    urgent
                        ? Icons.warning_amber_rounded
                        : Icons.campaign_outlined,
                    color: color,
                  ),
                  SizedBox(width: 12),
                  Expanded(
                    child: Text(
                      urgent ? UiText.t("紧急公告") : UiText.t("版本公告"),
                      style: TextStyle(color: color),
                    ),
                  ),
                ],
              ),
              content: SingleChildScrollView(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      row['title'] as String,
                      style: Theme.of(dialog).textTheme.titleLarge,
                    ),
                    if ((row['subtitle'] as String).isNotEmpty) ...[
                      SizedBox(height: 12),
                      Text(row['subtitle'] as String),
                    ],
                  ],
                ),
              ),
              actions: [
                FilledButton(
                  onPressed: () => Navigator.pop(dialog, true),
                  child: Text(UiText.t("我知道了")),
                ),
              ],
            ),
          ),
        );
        if (acknowledged == true && !urgent) {
          read.add(id);
          await preferences.setStringList(readKey, read.toList());
        }
      }
      if (manual && shown == 0 && context.mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text(UiText.t("当前版本暂无公告"))));
      }
    } catch (_) {
      if (manual && context.mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text(UiText.t("公告加载失败，请检查网络后重试"))));
      }
    } finally {
      _showing = false;
    }
  }

  void dispose() => client.close();
}
