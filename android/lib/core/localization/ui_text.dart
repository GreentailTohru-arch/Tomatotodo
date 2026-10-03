import 'dart:convert';
import 'package:flutter/services.dart';
import 'package:flutter/widgets.dart';

/// Only source-owned interface literals call this API. User text bypasses it.
class UiText {
  static T watch<T>(BuildContext context, T Function() builder) {
    Localizations.maybeLocaleOf(context);
    return builder();
  }
  static String language = 'zh-CN';
  static Map<String, dynamic> _catalog = {};
  static Future<void> initialize() async {
    _catalog = jsonDecode(await rootBundle.loadString('assets/localization/catalog.json')) as Map<String, dynamic>;
  }
  static String t(String key) => language == 'zh-CN' ? key :
      (_catalog[language]?[key] ?? _catalog['en-US']?[key] ?? key) as String;
  static String f(String key, List<Object?> arguments) => t(key).replaceAllMapped(
      RegExp(r'\{(\d+)\}'), (match) {
        final index = int.parse(match[1]!);
        return index < arguments.length ? '${arguments[index] ?? ''}' : match[0]!;
      });
}

