import 'package:tomatotodo/core/localization/ui_text.dart';

import 'dart:async';
import 'dart:convert';
import 'dart:math' as math;

import 'package:flutter/material.dart';

import '../core/settings/app_settings.dart';
import '../core/updates/app_updates.dart';
import '../core/updates/version_announcements.dart';
import '../core/home_widgets/home_widgets_bridge.dart';
import '../core/account/account_drawer.dart';
import '../core/account/local_profile.dart';
import '../core/account/cloud_account.dart';
import '../core/account/cloud_account_ui.dart';
import '../core/widgets/md3_action_styles.dart';
import '../features/archive/archive_page.dart';
import '../features/configuration/configuration_page.dart';
import '../features/dashboard/dashboard_page.dart';
import '../features/dashboard/dashboard_quotes.dart';
import '../features/dashboard/dashboard_controller.dart';
import '../features/general/general_page.dart';
import '../features/general/general_state.dart';
import '../features/general/course_schedule_page.dart';

enum _Destination { dashboard, configuration, course, archive, general }

class AppShell extends StatefulWidget {
  const AppShell({super.key, required this.settings});
  final AppSettings settings;
  @override
  State<AppShell> createState() => _AppShellState();
}

class _AppShellState extends State<AppShell> {
  final _scaffoldKey = GlobalKey<ScaffoldState>();
  _Destination _selected = _Destination.dashboard;
  late final DashboardController _dashboard;
  late final DashboardQuotes _quotes;
  late final GeneralState _general;
  late final LocalProfile _profile;
  late final CloudAccount _cloud;
  late final AppUpdates _updates;
  late final VersionAnnouncements _announcements;
  late final AppLifecycleListener _lifecycle;
  late final HomeWidgetsBridge _homeWidgets;
  String? _presetSnapshot, _timerSnapshot;
  Future<void> _presetSync = Future.value();
  bool _timerNotificationStarted = false;
  bool _timerNotificationAllowed = false;
  int _resetTurns = 0;
  String? _notificationLanguage;

  @override
  void initState() {
    super.initState();
    _dashboard = DashboardController(widget.settings.preferences);
    _quotes = DashboardQuotes(widget.settings.preferences);
    unawaited(_quotes.refresh());
    _general = GeneralState(widget.settings.preferences);
    _profile = LocalProfile(widget.settings.preferences);
    _cloud = CloudAccount(widget.settings, _dashboard, _general, _profile);
    _updates = AppUpdates(widget.settings.preferences);
    _announcements = VersionAnnouncements(widget.settings.preferences);
    _cloud.resolveConflicts = (conflicts) =>
        mounted ? showSyncConflict(context, conflicts) : Future.value(null);
    unawaited(_restoreAndCheckUpdates());
    _dashboard.onCompletedFocus = () {
      if (_timerNotificationAllowed) {
        unawaited(
          _general.notifications.showTomato().catchError((Object _) {}),
        );
      }
    };
    _dashboard.onCompletedBreak = () {
      if (_timerNotificationAllowed) {
        unawaited(
          _general.notifications.showPhaseEvent(true).catchError((Object _) {}),
        );
      }
    };
    _homeWidgets = HomeWidgetsBridge(
      _dashboard,
      widget.settings,
      _general,
      _quotes,
      onOpen: (target) {
        if (!mounted) return;
        if (target == 'courses' && !_general.courseEnabled) {
          Navigator.of(context).push(
            MaterialPageRoute<void>(
              builder: (_) => CourseSchedulePage(general: _general),
            ),
          );
          return;
        }
        setState(
          () => _selected = switch (target) {
            'tasks' => _Destination.configuration,
            'courses' =>
              _general.courseEnabled
                  ? _Destination.course
                  : _Destination.general,
            _ => _Destination.dashboard,
          },
        );
      },
    );
    unawaited(_homeWidgets.start());
    unawaited(widget.settings.refreshSystemColor());
    _dashboard.addListener(_onDashboardChanged);
    _lifecycle = AppLifecycleListener(
      onResume: () {
        unawaited(_cloud.synchronize());
        unawaited(widget.settings.refreshSystemColor());
        unawaited(_homeWidgets.refresh());
        _dashboard.checkPresetDeadlines();
        _presetSnapshot = null;
        _onDashboardChanged();
      },
    );
    WidgetsBinding.instance.addPostFrameCallback((_) => _onDashboardChanged());
    _general.onInAppReminder = (message) {
      if (mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text(message)));
      }
    };
  }

  Future<void> _restoreAndCheckUpdates() async {
    await _cloud.restore();
    if (mounted && AppUpdates.supported) {
      await _updates.check(context, manual: false);
      if (mounted && _updates.version != null) {
        await _announcements.showStartup(context, _updates.version!);
      }
    }
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    final language = Localizations.localeOf(context).toLanguageTag();
    if (_notificationLanguage == language) return;
    _notificationLanguage = language;
    UiText.language = language;
    _timerSnapshot = null;
    _presetSnapshot = null;
    unawaited(_general.refreshNotificationLanguage());
    unawaited(_homeWidgets.refresh());
    _onDashboardChanged();
  }

  @override
  void dispose() {
    _updates.dispose();
    _announcements.dispose();
    _lifecycle.dispose();
    _cloud.dispose();
    _homeWidgets.dispose();
    _dashboard.removeListener(_onDashboardChanged);
    _dashboard.dispose();
    _quotes.dispose();
    _general.dispose();
    _profile.dispose();
    super.dispose();
  }

  void _onDashboardChanged() {
    final snapshot = jsonEncode(
      _dashboard.presets.map((preset) => preset.toJson()).toList(),
    );
    if (snapshot != _presetSnapshot) {
      _presetSnapshot = snapshot;
      _presetSync = _presetSync.then((_) => _syncPresetNotifications());
    }
    _syncTimerNotification();
  }

  Future<void> _syncPresetNotifications() async {
    try {
      final previous =
          widget.settings.preferences.getInt(
            'tomatotodo-scheduled-preset-count',
          ) ??
          0;
      final result = await _general.notifications.schedulePresets(
        List.of(_dashboard.presets),
        previousCount: previous,
      );
      await widget.settings.preferences.setInt(
        'tomatotodo-scheduled-preset-count',
        result.count,
      );
    } catch (_) {
      // Local schedule settings stay available if platform notifications fail.
    }
  }

  void _syncTimerNotification() {
    final active = _timerNotificationStarted && _timerNotificationAllowed;
    final stamp = active
        ? '${_dashboard.phase.name}:${_dashboard.isRunning}:${_dashboard.elapsedSeconds ~/ 5}:${_dashboard.durationSeconds}'
        : 'off';
    if (stamp == _timerSnapshot) return;
    _timerSnapshot = stamp;
    unawaited(
      _general.notifications
          .showTimer(
            active: active,
            running: _dashboard.isRunning,
            shortBreak: _dashboard.phase == TimerPhase.shortBreak,
            countUp: _dashboard.countUp,
            elapsed: _dashboard.elapsedSeconds,
            total: _dashboard.durationSeconds,
          )
          .catchError((Object _) {}),
    );
  }

  Future<void> _toggleTimer() async {
    if (!_dashboard.isRunning && !_timerNotificationAllowed) {
      try {
        _timerNotificationAllowed = await _general.notifications
            .requestAccess();
      } catch (_) {
        _timerNotificationAllowed = false;
      }
      if (!mounted) return;
      if (!_timerNotificationAllowed) {
        final messenger = ScaffoldMessenger.of(context);
        messenger.showMaterialBanner(
          MaterialBanner(
            content: Text(UiText.t("请允许通知，以显示专注进度与番茄提醒")),
            actions: [
              TextButton(
                onPressed: messenger.hideCurrentMaterialBanner,
                child: Text(UiText.t("知道了")),
              ),
            ],
          ),
        );
      }
    }
    _timerNotificationStarted = true;
    await _homeWidgets.command('toggle');
    _syncTimerNotification();
  }

  void _resetTimer() {
    setState(() => _resetTurns++);
    _timerNotificationStarted = false;
    unawaited(_homeWidgets.command('reset'));
    _syncTimerNotification();
  }

  bool _dashboardEditing = false;

  Future<bool> _requestScheduleAccess() async {
    try {
      return await _general.notifications.requestAccess(exact: true);
    } catch (_) {
      return false;
    }
  }

  @override
  Widget build(BuildContext context) => UiText.watch(
    context,
    () => ListenableBuilder(
      listenable: _general,
      builder: (context, _) {
        final destinations = [
          _Destination.dashboard,
          _Destination.configuration,
          if (_general.courseEnabled) _Destination.course,
          _Destination.archive,
          _Destination.general,
        ];
        final selected = destinations.contains(_selected)
            ? _selected
            : _Destination.general;
        final wide = MediaQuery.sizeOf(context).width >= 600;
        return Scaffold(
          key: _scaffoldKey,
          endDrawer: AccountDrawer(profile: _profile, cloud: _cloud),
          body: Row(
            children: [
              if (wide)
                SafeArea(
                  child: NavigationRail(
                    selectedIndex: destinations.indexOf(selected),
                    labelType: NavigationRailLabelType.all,
                    onDestinationSelected: (index) =>
                        setState(() => _selected = destinations[index]),
                    destinations: [
                      for (final destination in destinations)
                        NavigationRailDestination(
                          icon: Icon(switch (destination) {
                            _Destination.dashboard => Icons.dashboard_outlined,
                            _Destination.configuration => Icons.tune,
                            _Destination.course => Icons.calendar_view_week,
                            _Destination.archive => Icons.bar_chart,
                            _Destination.general => Icons.settings_outlined,
                          }),
                          label: Text(switch (destination) {
                            _Destination.dashboard => UiText.t("仪表盘"),
                            _Destination.configuration => UiText.t("配置"),
                            _Destination.course => UiText.t("课表"),
                            _Destination.archive => UiText.t("档案"),
                            _Destination.general => UiText.t("常规"),
                          }),
                        ),
                    ],
                  ),
                ),
              Expanded(
                child: Stack(
                  children: [
                    IndexedStack(
                      index: destinations.indexOf(selected),
                      children: [
                        DashboardPage(
                          key: ValueKey(_Destination.dashboard),
                          controller: _dashboard,
                          general: _general,
                          quotes: _quotes,
                          onEditingChanged: (editing) =>
                              setState(() => _dashboardEditing = editing),
                          profile: _profile,
                          onAccountPressed: () =>
                              _scaffoldKey.currentState?.openEndDrawer(),
                          onConfigure: () => setState(
                            () => _selected = _Destination.configuration,
                          ),
                        ),
                        ConfigurationPage(
                          key: ValueKey(_Destination.configuration),
                          controller: _dashboard,
                          onRequestNotificationAccess: _requestScheduleAccess,
                          profile: _profile,
                          onAccountPressed: () =>
                              _scaffoldKey.currentState?.openEndDrawer(),
                        ),
                        if (_general.courseEnabled)
                          CourseSchedulePage(
                            key: ValueKey(_Destination.course),
                            general: _general,
                            profile: _profile,
                            onAccountPressed: () =>
                                _scaffoldKey.currentState?.openEndDrawer(),
                          ),
                        ArchivePage(
                          key: ValueKey(_Destination.archive),
                          controller: _dashboard,
                          preferences: widget.settings.preferences,
                          profile: _profile,
                          onAccountPressed: () =>
                              _scaffoldKey.currentState?.openEndDrawer(),
                        ),
                        GeneralPage(
                          updates: _updates,
                          announcements: _announcements,
                          cloud: _cloud,
                          key: ValueKey(_Destination.general),
                          settings: widget.settings,
                          dashboard: _dashboard,
                          general: _general,
                          profile: _profile,
                          onAccountPressed: () =>
                              _scaffoldKey.currentState?.openEndDrawer(),
                        ),
                      ],
                    ),
                    if (selected == _Destination.dashboard)
                      Positioned(
                        right: 16 + MediaQuery.paddingOf(context).right,
                        bottom: math.max(
                          16,
                          wide ? MediaQuery.paddingOf(context).bottom : 0,
                        ),
                        child: IgnorePointer(
                          ignoring: _dashboardEditing,
                          child: AnimatedSwitcher(
                            duration: MediaQuery.disableAnimationsOf(context)
                                ? Duration.zero
                                : Duration(milliseconds: 250),
                            reverseDuration:
                                MediaQuery.disableAnimationsOf(context)
                                ? Duration.zero
                                : Duration(milliseconds: 180),
                            switchInCurve: Curves.easeOutCubic,
                            switchOutCurve: Curves.easeInCubic,
                            transitionBuilder: (child, animation) =>
                                FadeTransition(
                                  opacity: animation,
                                  child: SlideTransition(
                                    position: Tween<Offset>(
                                      begin: Offset(0, .18),
                                      end: Offset.zero,
                                    ).animate(animation),
                                    child: child,
                                  ),
                                ),
                            child: _dashboardEditing
                                ? SizedBox.shrink(
                                    key: ValueKey('editing-controls-hidden'),
                                  )
                                : Padding(
                                    key: ValueKey('timer-floating-controls'),
                                    padding: EdgeInsets.zero,
                                    child: AnimatedBuilder(
                                      animation: _dashboard,
                                      builder: (context, _) => Row(
                                        mainAxisSize: MainAxisSize.min,
                                        children: [
                                          FilledButton(
                                            onPressed: _dashboardEditing
                                                ? null
                                                : _toggleTimer,
                                            style: Md3ActionStyles.timerAction(
                                              Theme.of(context).colorScheme,
                                              running: _dashboard.isRunning,
                                              reduceMotion: MediaQuery.of(
                                                context,
                                              ).disableAnimations,
                                            ),
                                            child: Row(
                                              mainAxisSize: MainAxisSize.min,
                                              children: [
                                                AnimatedSwitcher(
                                                  duration:
                                                      MediaQuery.of(context)
                                                          .disableAnimations
                                                      ? Duration.zero
                                                      : Duration(
                                                          milliseconds: 220,
                                                        ),
                                                  transitionBuilder:
                                                      (child, animation) =>
                                                          ScaleTransition(
                                                            scale: animation,
                                                            child: child,
                                                          ),
                                                  child: Icon(
                                                    _dashboard.isRunning
                                                        ? Icons.pause_rounded
                                                        : Icons
                                                              .play_arrow_rounded,
                                                    key: ValueKey(
                                                      _dashboard.isRunning,
                                                    ),
                                                    size: 20,
                                                  ),
                                                ),
                                                SizedBox(width: 6),
                                                Text(
                                                  _dashboard.isRunning
                                                      ? UiText.t("暂停")
                                                      : UiText.t("开始"),
                                                ),
                                              ],
                                            ),
                                          ),
                                          SizedBox(width: 12),
                                          Material(
                                            elevation: 8,
                                            shadowColor: Theme.of(context)
                                                .colorScheme
                                                .shadow
                                                .withValues(alpha: 0.42),
                                            color: Theme.of(context)
                                                .colorScheme
                                                .secondaryContainer,
                                            shape: CircleBorder(),
                                            child: IconButton.filledTonal(
                                              tooltip: UiText.t("重置"),
                                              onPressed: _dashboardEditing
                                                  ? null
                                                  : _resetTimer,
                                              style: IconButton.styleFrom(
                                                backgroundColor:
                                                    Theme.of(context)
                                                        .colorScheme
                                                        .secondaryContainer,
                                                foregroundColor:
                                                    Theme.of(context)
                                                        .colorScheme
                                                        .onSecondaryContainer,
                                                minimumSize: Size(56, 56),
                                                maximumSize: Size(56, 56),
                                                shape: CircleBorder(),
                                              ),
                                              icon:
                                                  TweenAnimationBuilder<double>(
                                                    tween: Tween(
                                                      begin: _resetTurns == 0
                                                          ? 0
                                                          : (_resetTurns - 1)
                                                                .toDouble(),
                                                      end: _resetTurns
                                                          .toDouble(),
                                                    ),
                                                    duration:
                                                        MediaQuery.of(context)
                                                            .disableAnimations
                                                        ? Duration.zero
                                                        : Duration(
                                                            milliseconds: 480,
                                                          ),
                                                    curve:
                                                        Curves.easeInOutCubic,
                                                    builder:
                                                        (
                                                          context,
                                                          turns,
                                                          child,
                                                        ) => Transform.rotate(
                                                          angle:
                                                              turns *
                                                              math.pi *
                                                              2,
                                                          child: child,
                                                        ),
                                                    child: Icon(
                                                      Icons.restart_alt,
                                                    ),
                                                  ),
                                            ),
                                          ),
                                        ],
                                      ),
                                    ),
                                  ),
                          ),
                        ),
                      ),
                  ],
                ),
              ),
            ],
          ),
          bottomNavigationBar: wide
              ? null
              : DefaultTextStyle.merge(
                  textAlign: TextAlign.center,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  child: NavigationBar(
                    height: 80,
                    labelPadding: const EdgeInsets.fromLTRB(8, 4, 8, 0),
                    selectedIndex: destinations.indexOf(selected),
                    onDestinationSelected: (index) =>
                        setState(() => _selected = destinations[index]),
                    destinations:
                        [
                              NavigationDestination(
                                icon: Icon(Icons.dashboard_outlined),
                                selectedIcon: Icon(Icons.dashboard),
                                label: UiText.t("仪表盘"),
                              ),
                              NavigationDestination(
                                icon: Icon(Icons.tune_outlined),
                                selectedIcon: Icon(Icons.tune),
                                label: UiText.t("配置"),
                              ),
                              if (_general.courseEnabled)
                                NavigationDestination(
                                  icon: Icon(Icons.calendar_view_week_outlined),
                                  selectedIcon: Icon(Icons.calendar_view_week),
                                  label: UiText.t("课表"),
                                ),
                              NavigationDestination(
                                icon: Icon(Icons.bar_chart_outlined),
                                selectedIcon: Icon(Icons.bar_chart),
                                label: UiText.t("档案"),
                              ),
                              NavigationDestination(
                                icon: Icon(Icons.settings_outlined),
                                selectedIcon: Icon(Icons.settings),
                                label: UiText.t("常规"),
                              ),
                            ]
                            .map(
                              (destination) => DefaultTextStyle.merge(
                                textAlign: TextAlign.center,
                                maxLines: 1,
                                overflow: TextOverflow.ellipsis,
                                child: destination,
                              ),
                            )
                            .toList(),
                  ),
                ),
        );
      },
    ),
  );
}
