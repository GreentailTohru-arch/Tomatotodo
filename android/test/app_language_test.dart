import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:tomatotodo/core/settings/app_language.dart';
import 'package:tomatotodo/core/settings/app_settings.dart';

void main() {
  test('system locale uses primary language and falls back to English', () {
    expect(AppLanguage.resolve('system', [const Locale('zz', 'ZZ'), const Locale('zh', 'CN')]), const Locale('en', 'US'));
    expect(AppLanguage.resolve('system', [const Locale('zh', 'HK')]), const Locale('zh', 'TW'));
    expect(AppLanguage.resolve('system', [const Locale.fromSubtags(languageCode: 'zh', scriptCode: 'Hant')]), const Locale('zh', 'TW'));
    expect(AppLanguage.resolve('system', [const Locale('ja', 'JP')]), const Locale('ja'));
    expect(AppLanguage.resolve('zh-CN', [const Locale('en')]), const Locale('zh', 'CN'));
  });
  test('all language choices and region aliases resolve correctly', () {
    for (final code in AppLanguage.codes.skip(1)) {
      expect(AppLanguage.resolveCode(code), code);
    }
    for (final entry in {'ar-SA':'ar', 'bn-IN':'bn', 'zh-Hant':'zh-TW', 'es-MX':'es-419', 'es-AR':'es-419', 'pt-BR':'pt-BR', 'sr-RS':'sr-Cyrl', 'iw-IL':'he', 'no-NO':'nb', 'zz-ZZ':'en-US'}.entries) {
      expect(AppLanguage.resolveCode(entry.key), entry.value);
    }
  });
  test('language defaults to system and persists independently of user backup', () async {
    SharedPreferences.setMockInitialValues({});
    final preferences = await SharedPreferences.getInstance();
    final settings = AppSettings(preferences);
    expect(settings.language, 'system');
    await settings.setLanguage('ja');
    expect(AppSettings(preferences).language, 'ja');
    expect(settings.exportData().containsKey('ui.language'), false);
    await expectLater(settings.setLanguage('invalid'), throwsArgumentError);
  });
}
