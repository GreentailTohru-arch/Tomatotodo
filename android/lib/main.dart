import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'app/tomatotodo_app.dart';
import 'core/settings/app_settings.dart';
import 'core/localization/ui_text.dart';
import 'core/settings/app_language.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await UiText.initialize();
  final preferences = await SharedPreferences.getInstance();
  final settings = AppSettings(preferences);
  UiText.language = AppLanguage.resolve(settings.language, WidgetsBinding.instance.platformDispatcher.locales).toLanguageTag();
  runApp(TomatotodoApp(settings: settings));
}
