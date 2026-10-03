import 'package:tomatotodo/core/localization/ui_text.dart';
import 'dart:async';
import 'dart:convert';

import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart';

import '../settings/app_settings.dart';
import '../../features/dashboard/dashboard_controller.dart';
import '../../features/dashboard/dashboard_quotes.dart';
import '../../features/general/general_state.dart';

/// Native widgets own a durable clock and an acknowledged event journal.
/// This keeps launcher actions independent of the Flutter activity lifetime.
class HomeWidgetsBridge {
  HomeWidgetsBridge(
    this.dashboard,
    this.settings,
    this.general,
    this.quotes, {
    required this.onOpen,
  });
  static final channel = MethodChannel('com.tomatotodo/home_widgets');
  final DashboardController dashboard;
  final AppSettings settings;
  final GeneralState general;
  final DashboardQuotes quotes;
  final void Function(String) onOpen;
  bool _disposed = false, _applying = false, _available = false;
  int _revision = 0;
  String? _lastPayload;
  Future<void> _queue = Future.value();
  bool get supported =>
      !kIsWeb && defaultTargetPlatform == TargetPlatform.android;

  Future<void> start() async {
    if (!supported) return;
    dashboard.externalTimerDriver = true;
    channel.setMethodCallHandler((call) async {
      if (call.method == 'changed') await refresh();
      if (call.method == 'open' && call.arguments is String) {
        onOpen(call.arguments as String);
      }
    });
    await refresh();
    if (_disposed) return;
    for (final source in <Listenable>[dashboard, settings, general, quotes]) {
      source.addListener(_changed);
    }
    try {
      final target = await channel.invokeMethod<String>('launchTarget');
      if (target != null && !_disposed) onOpen(target);
    } on MissingPluginException {
      /* Non-Android test host. */
    }
  }

  void _changed() {
    if (_applying || _disposed) return;
    final payload = jsonEncode(_payload());
    if (payload == _lastPayload) return;
    _lastPayload = payload;
    _enqueue(() => _sync());
  }

  Future<void> _enqueue(Future<void> Function() work) {
    _queue = _queue.then((_) async {
      if (_disposed) return;
      try {
        await work();
      } on MissingPluginException {
        dashboard.externalTimerDriver = false;
      } on PlatformException {
        dashboard.externalTimerDriver = false;
      }
    });
    return _queue;
  }

  Future<void> refresh() => _enqueue(() async {
    final response = await channel.invokeMethod<String>('read');
    _available = true;
    await _apply(response);
    await _sync();
  });

  Future<void> command(String action) async {
    if (!supported || !_available) {
      if (action == 'toggle') dashboard.toggleRunning();
      if (action == 'reset') dashboard.reset();
      return;
    }
    await _enqueue(() async {
      await _apply(await channel.invokeMethod<String>('command', action));
      await _sync();
    });
  }

  Future<void> _apply(String? raw) async {
    if (raw == null || _disposed) return;
    final data = jsonDecode(raw) as Map<String, dynamic>;
    _revision = data['revision'] as int? ?? 0;
    final runtime = (data['snapshot'] as Map?)?['runtime'];
    _applying = true;
    try {
      await dashboard.applyWidgetEvents(
        data['events'] as List? ?? [],
        runtime is Map ? Map<String, dynamic>.from(runtime) : null,
      );
    } finally {
      _applying = false;
    }
  }

  Future<void> _sync() async {
    final payload = _payload();
    final response = await channel.invokeMethod<String>('publish', {
      'snapshot': jsonEncode(payload),
      'revision': _revision,
      'ack': dashboard.widgetEventId,
    });
    if (response == null) return;
    final data = jsonDecode(response) as Map<String, dynamic>;
    if (data['accepted'] == false) {
      await _apply(response);
      _lastPayload = null;
      _changed();
    } else {
      _revision = data['revision'] as int? ?? _revision;
      _lastPayload = jsonEncode(payload);
    }
  }

  Map<String, Object?> _payload() {
    Map<String, int> colors(Brightness brightness) {
      final scheme = settings.colorSchemeFor(brightness);
      return {
        'surface': scheme.surfaceContainer.toARGB32(),
        'primary': scheme.primary.toARGB32(),
        'onSurface': scheme.onSurface.toARGB32(),
        'secondary': scheme.onSurfaceVariant.toARGB32(),
        'container': scheme.primaryContainer.toARGB32(),
        'onContainer': scheme.onPrimaryContainer.toARGB32(),
        'outline': scheme.outlineVariant.toARGB32(),
        'tertiary': scheme.tertiaryContainer.toARGB32(),
      };
    }

    return {
      'runtime': dashboard.widgetRuntime,
      'language': UiText.language,
      'languagePreference': settings.language,
      'theme': settings.themeMode.name,
      'light': colors(Brightness.light),
      'dark': colors(Brightness.dark),
      'list': dashboard.presets
          .firstWhere((p) => p.id == dashboard.activePresetId)
          .name,
      'task': dashboard.activeTask?.title ?? UiText.t("未选择任务"),
      'subtitle': dashboard.activeTask?.subtitle ?? '',
      'tasks': dashboard.activeTasks
          .map(
            (task) => {
              'id': task.id,
              'title': task.title,
              'subtitle': task.subtitle,
              'done': task.done,
              'estimate': task.estimate,
              'tomatoes': dashboard.tomatoesForTask(task),
            },
          )
          .toList(),
      'countdownName': dashboard.countdownName,
      'countdownDate': dashboard.countdownDate?.toIso8601String(),
      'quote': quotes.current.text,
      'quoteSource': quotes.current.source,
      'offlineQuotes': DashboardQuotes.offlineQuotes,
      'weeks': general.schedule.weeks.map((week) => week.toJson()).toList(),
    };
  }

  static Future<bool> pin(String kind) async =>
      await channel.invokeMethod<bool>('pin', kind) ?? false;

  void dispose() {
    _disposed = true;
    for (final source in <Listenable>[dashboard, settings, general, quotes]) {
      source.removeListener(_changed);
    }
    if (supported) channel.setMethodCallHandler(null);
  }
}
