import 'package:flutter/material.dart';
import '../localization/ui_text.dart';

class AppLanguage {
  static const codes = ['system', 'ar', 'bn', 'bg', 'ca', 'zh-CN', 'zh-TW', 'hr', 'cs', 'da', 'nl', 'en-US', 'en-GB', 'fil', 'fi', 'fr', 'de', 'el', 'he', 'hi', 'hu', 'id', 'it', 'ja', 'ko', 'ms', 'nb', 'pl', 'pt-BR', 'pt-PT', 'ro', 'ru', 'sr-Cyrl', 'sr-Latn', 'sk', 'es-ES', 'es-419', 'sv', 'th', 'tr', 'uk', 'vi'];
  static const names = {
    'ar': "العربية",
    'bn': "বাংলা",
    'bg': "Български",
    'ca': "Català",
    'zh-CN': "简体中文",
    'zh-TW': "繁體中文",
    'hr': "Hrvatski",
    'cs': "Čeština",
    'da': "Dansk",
    'nl': "Nederlands",
    'en-US': "English (US)",
    'en-GB': "English (UK)",
    'fil': "Filipino",
    'fi': "Suomi",
    'fr': "Français",
    'de': "Deutsch",
    'el': "Ελληνικά",
    'he': "עברית",
    'hi': "हिन्दी",
    'hu': "Magyar",
    'id': "Bahasa Indonesia",
    'it': "Italiano",
    'ja': "日本語",
    'ko': "한국어",
    'ms': "Bahasa Melayu",
    'nb': "Norsk bokmål",
    'pl': "Polski",
    'pt-BR': "Português (Brasil)",
    'pt-PT': "Português (Portugal)",
    'ro': "Română",
    'ru': "Русский",
    'sr-Cyrl': "Српски (ћирилица)",
    'sr-Latn': "Srpski (latinica)",
    'sk': "Slovenčina",
    'es-ES': "Español (España)",
    'es-419': "Español (Latinoamérica)",
    'sv': "Svenska",
    'th': "ไทย",
    'tr': "Türkçe",
    'uk': "Українська",
    'vi': "Tiếng Việt",
  };
  static final locales = codes.skip(1).map(localeFor).toList();
  static Locale localeFor(String code) {
    final parts = code.split('-');
    return Locale.fromSubtags(languageCode: parts[0],
      scriptCode: parts.length > 1 && parts[1].length == 4 ? parts[1] : null,
      countryCode: parts.length > 1 && parts[1].length != 4 ? parts[1] : null);
  }
  static String resolveCode(String code) {
    code = code.replaceAll('_', '-');
    if (code == 'system') return 'en-US';
    if (code == 'en') return 'en-US';
    final parts = code.toLowerCase().split('-');
    var base = parts.first;
    base = {'iw':'he', 'in':'id', 'tl':'fil', 'no':'nb'}[base] ?? base;
    if (base == 'zh') return parts.contains('hant') || parts.any(['tw','hk','mo'].contains) ? 'zh-TW' : 'zh-CN';
    if (base == 'en') return parts.contains('gb') ? 'en-GB' : 'en-US';
    if (base == 'pt') return parts.skip(1).contains('pt') ? 'pt-PT' : 'pt-BR';
    if (base == 'es') return parts.length == 1 || parts.skip(1).contains('es') ? 'es-ES' : 'es-419';
    if (base == 'sr') return parts.contains('latn') ? 'sr-Latn' : 'sr-Cyrl';
    return codes.contains(base) ? base : 'en-US';
  }
  static Locale resolve(String preference, List<Locale> system) => localeFor(resolveCode(
      preference == 'system' ? (system.isEmpty ? 'en-US' : system.first.toLanguageTag()) : preference));
  static String label(Locale locale, String code) => code == 'system' ? UiText.t('跟随系统') : names[resolveCode(code)] ?? 'English (US)';
  static String title(Locale locale) => UiText.t('语言');
}
