import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:tomatotodo/core/settings/app_settings.dart';

void main() {
  test(
    'system palette is applied, backed up, and falls back when unavailable',
    () async {
      SharedPreferences.setMockInitialValues({});
      final preferences = await SharedPreferences.getInstance();
      final settings = AppSettings(preferences);
      final selectedSeed = settings.seedColor;
      final light = ColorScheme.fromSeed(seedColor: Colors.deepPurple);
      final dark = ColorScheme.fromSeed(
        seedColor: Colors.deepPurple,
        brightness: Brightness.dark,
      );

      await settings.setUseSystemColor(true);
      expect(
        settings.colorSchemeFor(Brightness.light),
        ColorScheme.fromSeed(seedColor: selectedSeed),
      );
      settings.setSystemColorSchemes(light, dark);
      expect(settings.colorSchemeFor(Brightness.light), light);
      expect(settings.colorSchemeFor(Brightness.dark), dark);
      expect(settings.exportData()['useSystemColor'], true);

      final restored = AppSettings(preferences);
      expect(restored.useSystemColor, true);
      expect(restored.systemColorAvailable, false);
      await restored.replaceData(settings.exportData());
      expect(restored.useSystemColor, true);
      await restored.setSeedColor(Colors.orange);
      expect(restored.useSystemColor, false);
      await restored.factoryReset();
      expect(restored.useSystemColor, false);
    },
  );
}
