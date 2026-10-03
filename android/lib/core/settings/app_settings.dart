import 'package:tomatotodo/core/localization/ui_text.dart';
import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:dynamic_color/dynamic_color.dart';
import 'app_language.dart';
import 'dart:ui' show PlatformDispatcher;

class AppSettings extends ChangeNotifier {
  AppSettings(this._preferences);
  final SharedPreferences _preferences;
  SharedPreferences get preferences => _preferences;

  String get language => AppLanguage.codes.contains(_preferences.getString('ui.language'))
      ? _preferences.getString('ui.language')! : _preferences.getString('ui.language') == 'en' ? 'en-US' : 'system';

  Future<void> setLanguage(String code) async {
    if (!AppLanguage.codes.contains(code)) throw ArgumentError.value(code, 'code');
    await _preferences.setString('ui.language', code);
    UiText.language = AppLanguage.resolve(code, PlatformDispatcher.instance.locales).toLanguageTag();
    notifyListeners();
  }

  ThemeMode get themeMode => switch (_preferences.getString('themeMode')) {
    'light' => ThemeMode.light,
    'dark' => ThemeMode.dark,
    _ => ThemeMode.system,
  };
  Color get seedColor => Color(_preferences.getInt('seedColor') ?? 0xFF30643B);
  bool get useSystemColor => _preferences.getBool('useSystemColor') ?? false;
  ColorScheme? _systemLightScheme;
  ColorScheme? _systemDarkScheme;
  bool get systemColorAvailable =>
      _systemLightScheme != null && _systemDarkScheme != null;

  void setSystemColorSchemes(ColorScheme? light, ColorScheme? dark) {
    if (_systemLightScheme == light && _systemDarkScheme == dark) return;
    _systemLightScheme = light;
    _systemDarkScheme = dark;
    notifyListeners();
  }

  Future<void> refreshSystemColor() async {
    try {
      final palette = await DynamicColorPlugin.getCorePalette();
      if (palette != null) {
        ColorScheme convert(Brightness brightness) {
          final system = palette.toColorScheme(brightness: brightness);
          return ColorScheme.fromSeed(
            seedColor: system.primary,
            brightness: brightness,
          ).copyWith(
            primary: system.primary,
            onPrimary: system.onPrimary,
            primaryContainer: system.primaryContainer,
            onPrimaryContainer: system.onPrimaryContainer,
            secondary: system.secondary,
            onSecondary: system.onSecondary,
            secondaryContainer: system.secondaryContainer,
            onSecondaryContainer: system.onSecondaryContainer,
            tertiary: system.tertiary,
            onTertiary: system.onTertiary,
            tertiaryContainer: system.tertiaryContainer,
            onTertiaryContainer: system.onTertiaryContainer,
            error: system.error,
            onError: system.onError,
            errorContainer: system.errorContainer,
            onErrorContainer: system.onErrorContainer,
            outline: system.outline,
            outlineVariant: system.outlineVariant,
            surface: system.surface,
            onSurface: system.onSurface,
            onSurfaceVariant: system.onSurfaceVariant,
            inverseSurface: system.inverseSurface,
            onInverseSurface: system.onInverseSurface,
            inversePrimary: system.inversePrimary,
            shadow: system.shadow,
            scrim: system.scrim,
            surfaceTint: system.primary,
          );
        }

        setSystemColorSchemes(
          convert(Brightness.light),
          convert(Brightness.dark),
        );
        return;
      }
      final accent = await DynamicColorPlugin.getAccentColor();
      if (accent != null) {
        setSystemColorSchemes(
          ColorScheme.fromSeed(seedColor: accent),
          ColorScheme.fromSeed(seedColor: accent, brightness: Brightness.dark),
        );
        return;
      }
    } catch (_) {
      // Older platforms and unavailable native channels keep the saved seed.
    }
    setSystemColorSchemes(null, null);
  }

  ColorScheme colorSchemeFor(Brightness brightness) {
    if (useSystemColor && systemColorAvailable) {
      return brightness == Brightness.light
          ? _systemLightScheme!
          : _systemDarkScheme!;
    }
    return ColorScheme.fromSeed(
      seedColor: seedColor,
      brightness: brightness,
      dynamicSchemeVariant: schemeVariant,
    );
  }

  Future<void> setUseSystemColor(bool value) async {
    await _preferences.setBool('useSystemColor', value);
    notifyListeners();
  }

  Color? get customSeedColor =>
      switch (_preferences.getInt('customSeedColor')) {
        final value? => Color(value),
        null => null,
      };
  DynamicSchemeVariant get schemeVariant =>
      DynamicSchemeVariant.values.firstWhere(
        (value) => value.name == _preferences.getString('schemeVariant'),
        orElse: () => DynamicSchemeVariant.tonalSpot,
      );
  bool get pureBlack => _preferences.getBool('pureBlack') ?? false;
  bool get textScaleEnabled => _preferences.getBool('textScaleEnabled') ?? true;
  double get textScale =>
      (_preferences.getDouble('textScale') ?? 1.0).clamp(0.85, 1.25);

  Future<void> setThemeMode(ThemeMode mode) async {
    await _preferences.setString('themeMode', mode.name);
    notifyListeners();
  }

  Future<void> setSeedColor(Color color) async {
    await _preferences.setInt('seedColor', color.toARGB32());
    await _preferences.setBool('useSystemColor', false);
    notifyListeners();
  }

  Future<void> setCustomSeedColor(Color color) async {
    await _preferences.setInt('customSeedColor', color.toARGB32());
    await setSeedColor(color);
  }

  Future<void> setSchemeVariant(DynamicSchemeVariant variant) async {
    await _preferences.setString('schemeVariant', variant.name);
    notifyListeners();
  }

  Future<void> setPureBlack(bool value) async {
    await _preferences.setBool('pureBlack', value);
    notifyListeners();
  }

  Future<void> setTextScaleEnabled(bool value) async {
    await _preferences.setBool('textScaleEnabled', value);
    notifyListeners();
  }

  Future<void> setTextScale(double value) async {
    await _preferences.setDouble('textScale', value.clamp(0.85, 1.25));
    notifyListeners();
  }

  Map<String, Object?> exportData() => {
    'themeMode': themeMode.name,
    'seedColor': seedColor.toARGB32(),
    'useSystemColor': useSystemColor,
    'customSeedColor': customSeedColor?.toARGB32(),
    'schemeVariant': schemeVariant.name,
    'pureBlack': pureBlack,
    'textScaleEnabled': textScaleEnabled,
    'textScale': textScale,
  };

  Future<void> replaceData(Map<String, dynamic> data) async {
    validateExportData(data);
    final mode = data['themeMode'];
    final seed = data['seedColor'];
    if (mode is! String ||
        !['system', 'light', 'dark'].contains(mode) ||
        seed is! int) {
      throw FormatException(UiText.t("个性化数据格式无效"));
    }
    await _preferences.setString('themeMode', mode);
    await _preferences.setInt('seedColor', seed);
    await _preferences.setBool(
      'useSystemColor',
      (data['useSystemColor'] as bool?) ?? false,
    );
    final custom = data['customSeedColor'];
    if (custom is int) {
      await _preferences.setInt('customSeedColor', custom);
    } else {
      await _preferences.remove('customSeedColor');
    }
    await _preferences.setString(
      'schemeVariant',
      (data['schemeVariant'] as String?) ?? 'tonalSpot',
    );
    await _preferences.setBool(
      'pureBlack',
      (data['pureBlack'] as bool?) ?? false,
    );
    await _preferences.setBool(
      'textScaleEnabled',
      (data['textScaleEnabled'] as bool?) ?? true,
    );
    await _preferences.setDouble(
      'textScale',
      (data['textScale'] as num?)?.toDouble() ?? 1.0,
    );
    notifyListeners();
  }

  static void validateExportData(Map<String, dynamic> data) {
    final mode = data['themeMode'];
    final seed = data['seedColor'];
    if (mode is! String ||
        !['system', 'light', 'dark'].contains(mode) ||
        seed is! int) {
      throw FormatException(UiText.t("个性化数据格式无效"));
    }
    if (data.containsKey('customSeedColor') &&
            data['customSeedColor'] != null &&
            data['customSeedColor'] is! int ||
        data.containsKey('useSystemColor') && data['useSystemColor'] is! bool ||
        data.containsKey('schemeVariant') &&
            !DynamicSchemeVariant.values.any(
              (v) => v.name == data['schemeVariant'],
            ) ||
        data.containsKey('pureBlack') && data['pureBlack'] is! bool ||
        data.containsKey('textScaleEnabled') &&
            data['textScaleEnabled'] is! bool ||
        data.containsKey('textScale') &&
            (data['textScale'] is! num ||
                (data['textScale'] as num) < 0.85 ||
                (data['textScale'] as num) > 1.25)) {
      throw FormatException(UiText.t("个性化数据格式无效"));
    }
  }

  Future<void> factoryReset() async {
    await _preferences.remove('themeMode');
    await _preferences.remove('seedColor');
    for (final key in [
      'customSeedColor',
      'useSystemColor',
      'schemeVariant',
      'pureBlack',
      'textScaleEnabled',
      'textScale',
    ]) {
      await _preferences.remove(key);
    }
    notifyListeners();
  }
}
