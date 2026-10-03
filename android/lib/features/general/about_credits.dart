import 'package:tomatotodo/core/localization/ui_text.dart';
import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';

class AboutCredits extends StatefulWidget {
  const AboutCredits({super.key, this.client});
  final http.Client? client;
  @override
  State<AboutCredits> createState() => _AboutCreditsState();
}

class _AboutCreditsState extends State<AboutCredits> {
  Map<String, dynamic>? _data;
  String? _error;
  late final http.Client _client;
  @override
  void initState() {
    super.initState();
    _client = widget.client ?? http.Client();
    _load();
  }

  @override
  void dispose() {
    _client.close();
    super.dispose();
  }

  Future<void> _load() async {
    final prefs = await SharedPreferences.getInstance();
    final cached = prefs.getString('about.public_credits');
    if (_data == null && cached != null) {
      try {
        final data = _decode(cached);
        if (mounted) setState(() => _data = data);
      } catch (_) {
        /* Fetch valid content below. */
      }
    }
    try {
      final response = await _client
          .get(Uri.parse('https://47.245.57.193:8443/api/about'))
          .timeout(Duration(seconds: 12));
      if (response.statusCode != 200) throw FormatException(UiText.t("加载失败"));
      final data = _decode(utf8.decode(response.bodyBytes));
      await prefs.setString('about.public_credits', jsonEncode(data));
      if (mounted) {
        setState(() {
          _data = data;
          _error = null;
        });
      }
    } catch (_) {
      if (mounted) {
        setState(
          () => _error = _data == null ? UiText.t("暂时无法加载名单，请稍后重试") : UiText.t("当前显示已缓存的名单"),
        );
      }
    }
  }

  Map<String, dynamic> _decode(String source) {
    if (source.length > 5 * 1024 * 1024) throw FormatException(UiText.t("名单过大"));
    final data = jsonDecode(source) as Map<String, dynamic>;
    for (final group in ['developers', 'thanks', 'sponsors']) {
      final people = data[group];
      if (people is! List ||
          people.length > (group == 'sponsors' ? 200 : 100)) {
        throw FormatException(UiText.t("名单格式无效"));
      }
      data[group] = people.map((raw) {
        final person = Map<String, dynamic>.from(raw as Map);
        final name = (person['name'] as String).trim();
        if (name.isEmpty || name.length > 80) {
          throw FormatException(UiText.t("姓名格式无效"));
        }
        person['name'] = name;
        person['description'] = person['description'] as String? ?? '';
        person['avatar'] = person['avatar'] as String? ?? '';
        final url = person['url'] as String? ?? '';
        person['url'] = group == 'sponsors' || !_safeLink(url) ? '' : url;
        return person;
      }).toList();
    }
    final sponsor = data['sponsor_url'] as String? ?? '';
    data['sponsor_url'] = _safeLink(sponsor) ? sponsor : '';
    return data;
  }

  bool _safeLink(String raw) {
    final uri = Uri.tryParse(raw);
    return uri != null &&
        uri.scheme == 'https' &&
        uri.host.isNotEmpty &&
        uri.userInfo.isEmpty &&
        !RegExp(r'[\s\x00-\x1f]').hasMatch(raw);
  }

  Future<void> _open(String raw) async {
    if (!_safeLink(raw)) {
      return;
    }
    try {
      await MethodChannel('com.tomatotodo/app_updates')
          .invokeMethod<void>('openDownload', raw);
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text(UiText.t("无法打开链接"))));
      }
    }
  }

  Widget _avatar(Map<String, dynamic> person, {double size = 56}) {
    final colors = Theme.of(context).colorScheme;
    final name = person['name'] as String;
    final raw = person['avatar'] as String? ?? '';
    ImageProvider? image;
    try {
      if (raw.isNotEmpty) {
        image = MemoryImage(base64Decode(raw.split(',').last));
      }
    } catch (_) {
      /* Initial fallback. */
    }
    final url = person['url'] as String? ?? '';
    return Tooltip(
      message: name,
      child: Material(
        color: colors.secondaryContainer,
        shape: CircleBorder(),
        clipBehavior: Clip.antiAlias,
        child: InkWell(
          customBorder: CircleBorder(),
          onTap: url.isEmpty ? null : () => _open(url),
          child: SizedBox.square(
            dimension: size,
            child: CircleAvatar(
              backgroundColor: colors.secondaryContainer,
              foregroundColor: colors.onSecondaryContainer,
              foregroundImage: image,
              onForegroundImageError: image == null ? null : (_, _) {},
              child: Text(
                name.isEmpty ? '?' : name.characters.first,
                style: Theme.of(context).textTheme.titleLarge,
              ),
            ),
          ),
        ),
      ),
    );
  }

  Widget _person(Map<String, dynamic> person, {bool sponsor = false}) {
    final detail = person['description'] as String? ?? '';
    final sponsorUrl = _data?['sponsor_url'] as String? ?? '';
    return Padding(
      padding: EdgeInsets.symmetric(vertical: 12),
      child: Row(
        children: [
          _avatar(person),
          SizedBox(width: 16),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  person['name'] as String,
                  style: Theme.of(context).textTheme.titleMedium,
                ),
                if (detail.isNotEmpty) ...[
                  SizedBox(height: 4),
                  Text(detail, style: Theme.of(context).textTheme.bodyMedium),
                ],
              ],
            ),
          ),
          if (sponsor) ...[
            SizedBox(width: 12),
            ConstrainedBox(
              constraints: BoxConstraints(maxWidth: 150),
              child: FilledButton.tonal(
                onPressed: sponsorUrl.isEmpty ? null : () => _open(sponsorUrl),
                child: Text(UiText.t("赞助 Tomatotodo"), textAlign: TextAlign.center),
              ),
            ),
          ],
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    Localizations.localeOf(context);
    if (_data == null && _error == null) {
      return Padding(
        padding: EdgeInsets.all(32),
        child: Center(child: CircularProgressIndicator()),
      );
    }
    return Padding(
      padding: EdgeInsets.symmetric(horizontal: 16, vertical: 24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          if (_error != null)
            Row(
              children: [
                Expanded(child: Text(_error!)),
                TextButton(onPressed: _load, child: Text(UiText.t("重试"))),
              ],
            ),
          if (_data != null) ...[
            for (final entry in {
              'developers': UiText.t("开发者"),
              'thanks': UiText.t("特别鸣谢"),
            }.entries) ...[
              Text(entry.value, style: Theme.of(context).textTheme.titleMedium),
              if ((_data![entry.key] as List).isEmpty)
                Padding(
                  padding: EdgeInsets.symmetric(vertical: 16),
                  child: Text(UiText.t("名单待添加")),
                ),
              for (var i = 0; i < (_data![entry.key] as List).length; i++)
                _person(
                  Map<String, dynamic>.from(_data![entry.key][i] as Map),
                  sponsor: entry.key == 'developers' && i == 0,
                ),
              SizedBox(height: 16),
            ],
            Text(UiText.t("赞助者"), style: Theme.of(context).textTheme.titleMedium),
            SizedBox(height: 16),
            if ((_data!['sponsors'] as List).isEmpty) Text(UiText.t("感谢每一份支持")),
            Wrap(
              spacing: 12,
              runSpacing: 12,
              children: [
                for (final person in _data!['sponsors'] as List)
                  _avatar(Map<String, dynamic>.from(person as Map), size: 48),
              ],
            ),
          ],
        ],
      ),
    );
  }
}
