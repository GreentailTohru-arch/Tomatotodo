import 'package:tomatotodo/core/localization/ui_text.dart';
import 'dart:convert';

import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';

/// Uses the same numeric version format as the release API (not string order).
List<int> versionParts(String value) {
  if (!RegExp(r'^(0|[1-9][0-9]{0,4})(\.(0|[1-9][0-9]{0,4})){1,3}$')
      .hasMatch(value)) {
    throw FormatException(UiText.t("无效的版本号"));
  }
  final parts = value.split('.').map(int.parse).toList();
  if (parts.any((part) => part > 65535)) {
    throw FormatException(UiText.t("无效的版本号"));
  }
  return [...parts, ...List.filled(4 - parts.length, 0)];
}

int compareVersions(String a, String b) {
  final left = versionParts(a), right = versionParts(b);
  for (var i = 0; i < 4; i++) {
    final result = left[i].compareTo(right[i]);
    if (result != 0) return result;
  }
  return 0;
}

class AppRelease {
  AppRelease(this.version, this.downloadUrl);
  final String version;
  final Uri downloadUrl;

  factory AppRelease.fromJson(Map<String, dynamic> json) {
    final version = json['version'] as String;
    versionParts(version);
    final raw = json['download_url'] as String;
    final url = Uri.parse(raw);
    if (url.scheme != 'https' ||
        url.host.isEmpty ||
        url.userInfo.isNotEmpty ||
        RegExp(r'[\s\x00-\x1f]').hasMatch(raw)) {
      throw FormatException(UiText.t("无效的下载链接"));
    }
    return AppRelease(version, url);
  }
}

enum UpdateResult { available, current, unpublished, skipped }

class AppUpdates extends ChangeNotifier {
  AppUpdates(this.preferences, {http.Client? client, Uri? endpoint})
    : _client = client ?? http.Client(),
      endpoint =
          endpoint ??
          Uri.parse(
            'https://47.245.57.193:8443/api/releases/latest?platform=android',
          );

  static final channel = MethodChannel('com.tomatotodo/app_updates');
  static final skippedKey = 'app_updates.skipped_android_version';
  final SharedPreferences preferences;
  final http.Client _client;
  final Uri endpoint;
  String? version;
  String? buildNumber;
  AppRelease? latest;
  bool checking = false;
  bool _disposed = false;
  String get versionLabel =>
      version == null ? UiText.t("版本信息暂不可用") : '$version（$buildNumber）';

  void _notify() {
    if (!_disposed) notifyListeners();
  }

  Future<void> loadVersion() async {
    final info = await channel.invokeMapMethod<String, dynamic>('version');
    final name = info?['version'] as String?;
    if (name == null) throw FormatException(UiText.t("无法读取安装版本"));
    versionParts(name);
    version = name;
    buildNumber = info?['build'].toString() ?? '';
    _notify();
  }

  Future<UpdateResult> fetch({required bool manual}) async {
    latest = null;
    if (version == null) await loadVersion();
    final response = await _client
        .get(endpoint, headers: {'Cache-Control': 'no-cache'})
        .timeout(Duration(seconds: 12));
    if (response.statusCode != 200) throw FormatException(UiText.t("更新服务暂不可用"));
    final data = jsonDecode(response.body);
    if (data == null) return UpdateResult.unpublished;
    final release = AppRelease.fromJson(data as Map<String, dynamic>);
    if (compareVersions(release.version, version!) <= 0) {
      return UpdateResult.current;
    }
    latest = release;
    final skipped = preferences.getString(skippedKey);
    if (!manual && skipped != null) {
      try {
        if (compareVersions(skipped, release.version) == 0) {
          return UpdateResult.skipped;
        }
      } on FormatException {
        /* Old/corrupt preferences must not hide updates. */
      }
    }
    return UpdateResult.available;
  }

  Future<void> skip(AppRelease release) async {
    if (!await preferences.setString(skippedKey, release.version)) {
      throw FormatException(UiText.t("未能保存跳过设置"));
    }
  }

  Future<void> check(BuildContext context, {required bool manual}) async {
    if (checking || _disposed) return;
    checking = true;
    _notify();
    void message(String text) {
      if (context.mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text(text)));
      }
    }

    try {
      final result = await fetch(manual: manual);
      if (_disposed || !context.mounted) return;
      if (result != UpdateResult.available) {
        if (manual) {
          message(result == UpdateResult.unpublished ? UiText.t("当前没有已发布的更新") : UiText.t("已是最新版本"));
        }
        return;
      }
      // Avoid covering another dialog/navigation flow during startup.
      if (!manual &&
          (ModalRoute.of(context)?.isCurrent == false ||
              WidgetsBinding.instance.lifecycleState ==
                  AppLifecycleState.paused)) {
        return;
      }
      final release = latest!;
      final action = await showDialog<String>(
        context: context,
        builder: (dialog) => AlertDialog(
          icon: Icon(Icons.system_update_outlined),
          title: Text(UiText.t("发现新版本")),
          content: Text(
            UiText.f("Tomatotodo {0}\n当前版本：{1}\n\n下载后可安装更新，已有数据会保留。", [release.version, version]),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(dialog, 'skip'),
              child: Text(UiText.t("跳过此版本")),
            ),
            TextButton(
              onPressed: () => Navigator.pop(dialog),
              child: Text(UiText.t("暂不更新")),
            ),
            FilledButton.icon(
              onPressed: () => Navigator.pop(dialog, 'download'),
              icon: Icon(Icons.download_outlined),
              label: Text(UiText.t("下载更新")),
            ),
          ],
        ),
      );
      if (_disposed) return;
      if (action == 'skip') {
        await skip(release);
        message(UiText.f("已跳过 {0}，仍可在常规中手动检查更新", [release.version]));
      } else if (action == 'download') {
        await channel.invokeMethod<void>(
          'openDownload',
          release.downloadUrl.toString(),
        );
      }
    } catch (_) {
      if (manual) message(UiText.t("检查或打开更新失败，请检查网络后重试"));
    } finally {
      checking = false;
      _notify();
    }
  }

  static bool get supported =>
      !kIsWeb && defaultTargetPlatform == TargetPlatform.android;
  @override
  void dispose() {
    _disposed = true;
    _client.close();
    super.dispose();
  }
}
