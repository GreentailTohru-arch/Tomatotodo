import 'package:tomatotodo/core/localization/ui_text.dart';
import 'dart:convert';

import 'package:flutter/foundation.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';

@immutable
class DashboardQuote {
  const DashboardQuote(this.text, this.source, {this.uuid});

  final String text;
  final String source;
  final String? uuid;
}

/// A short cached quote, with original local copy available without network.
class DashboardQuotes extends ChangeNotifier {
  DashboardQuotes(
    this._preferences, {
    Future<http.Response> Function(Uri)? fetch,
    DateTime Function()? now,
  }) : _fetch = fetch ?? http.get,
       _now = now ?? DateTime.now {
    _restore();
  }

  static final _textKey = 'dashboard_quote_text';
  static final _sourceKey = 'dashboard_quote_source';
  static final _uuidKey = 'dashboard_quote_uuid';
  static final _fetchKey = 'dashboard_quote_fetched_at';

  static List<String> get offlineQuotes => [
    UiText.t("把今天的一小步走稳，就是明天的起点。"),
    UiText.t("成长从来不是一跃而成，而是一次次认真开始。"),
    UiText.t("允许自己慢一点，但别忘了继续向前。"),
    UiText.t("专注眼前能做的事，改变会慢慢发生。"),
    UiText.t("每一次坚持，都在为未来积蓄力量。"),
    UiText.t("学会与困难同行，也是在学会相信自己。"),
    UiText.t("不必等准备完美，现在就可以迈出第一步。"),
    UiText.t("让努力成为习惯，让进步有迹可循。"),
  ];

  final SharedPreferences _preferences;
  final Future<http.Response> Function(Uri) _fetch;
  final DateTime Function() _now;
  bool _loading = false;
  bool _disposed = false;
  DateTime? _lastFetch;
  late DashboardQuote current;

  void _restore() {
    final today = _now();
    final fallback = offlineQuotes[today.day % offlineQuotes.length];
    final text = _preferences.getString(_textKey);
    current = text == null || text.isEmpty
        ? DashboardQuote(fallback, UiText.t("Tomatotodo · 离线语录"))
        : DashboardQuote(
            text,
            _preferences.getString(_sourceKey) ?? UiText.t("一言"),
            uuid: _preferences.getString(_uuidKey),
          );
    _lastFetch = DateTime.tryParse(_preferences.getString(_fetchKey) ?? '');
  }

  Future<void> refresh({bool force = false}) async {
    if (_loading) return;
    final now = _now();
    if (!force &&
        _lastFetch != null &&
        now.difference(_lastFetch!) < Duration(hours: 6)) {
      return;
    }
    _loading = true;
    try {
      final response = await _fetch(
        Uri.https('v1.hitokoto.cn', '/', {
          'c': 'k',
          'encode': 'json',
          'max_length': '48',
        }),
      ).timeout(Duration(seconds: 5));
      if (response.statusCode != 200) return;
      final data = jsonDecode(utf8.decode(response.bodyBytes));
      if (data is! Map) return;
      final rawText = data['hitokoto'];
      if (rawText is! String) return;
      final text = rawText.trim();
      if (text.isEmpty || text.runes.length > 48 || !_isGrowthQuote(text)) {
        return;
      }
      final from = data['from'] is String
          ? (data['from'] as String).trim()
          : '';
      final uuid = data['uuid'] is String ? data['uuid'] as String : null;
      current = DashboardQuote(
        text,
        from.isEmpty ? UiText.t("一言") : UiText.f("一言 · {0}", [from]),
        uuid: uuid,
      );
      _lastFetch = now;
      await Future.wait([
        _preferences.setString(_textKey, current.text),
        _preferences.setString(_sourceKey, current.source),
        _preferences.setString(_fetchKey, now.toIso8601String()),
        if (uuid != null) _preferences.setString(_uuidKey, uuid),
      ]);
      if (!_disposed) notifyListeners();
    } catch (_) {
      // Keep the cached or bundled quote visible when offline.
    } finally {
      _loading = false;
    }
  }

  static bool _isGrowthQuote(String text) => RegExp(
    r'成长|努力|坚持|梦想|希望|勇气|未来|前行|奋斗|改变|学习|热爱|生活|时间|目标|青春|人生|向前|自己|行动|成功|失败',
  ).hasMatch(text);

  @override
  void dispose() {
    _disposed = true;
    super.dispose();
  }
}
