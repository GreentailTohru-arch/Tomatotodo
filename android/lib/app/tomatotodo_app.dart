import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';

import '../core/settings/app_settings.dart';
import '../core/settings/app_language.dart';
import 'app_shell.dart';
import '../core/localization/ui_text.dart';

class TomatotodoApp extends StatelessWidget {
  const TomatotodoApp({super.key, required this.settings});
  final AppSettings settings;

  @override
  Widget build(BuildContext context) => UiText.watch(context, () => ListenableBuilder(
    listenable: settings,
    builder: (context, _) => MaterialApp(
      title: 'Tomatotodo',
      locale: settings.language == 'system' ? null : AppLanguage.resolve(settings.language, const []),
      supportedLocales: AppLanguage.locales,
      localeListResolutionCallback: (locales, _) {
        final resolved = AppLanguage.resolve(settings.language, locales ?? const []);
        UiText.language = resolved.toLanguageTag();
        return resolved;
      },
      localizationsDelegates: GlobalMaterialLocalizations.delegates,
      debugShowCheckedModeBanner: false,
      themeMode: settings.themeMode,
      theme: _theme(Brightness.light),
      darkTheme: _theme(Brightness.dark),
      builder: (context, child) {
        UiText.language = Localizations.localeOf(context).toLanguageTag();
        return MediaQuery(
        data: MediaQuery.of(context).copyWith(
          textScaler: TextScaler.linear(
            settings.textScaleEnabled ? settings.textScale : 1.0,
          ),
        ),
        child: child ?? const SizedBox.shrink(),
      );
      },
      home: AppShell(settings: settings),
    ),
  ));

  ThemeData _theme(Brightness brightness) {
    var scheme = settings.colorSchemeFor(brightness);
    if (brightness == Brightness.dark && settings.pureBlack) {
      scheme = scheme.copyWith(surface: Colors.black);
    }
    return ThemeData(
      useMaterial3: true,
      colorScheme: scheme,
      scaffoldBackgroundColor: scheme.surface,
    );
  }
}
