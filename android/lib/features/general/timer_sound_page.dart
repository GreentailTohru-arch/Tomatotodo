import 'package:tomatotodo/core/localization/ui_text.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:file_picker/file_picker.dart';
import 'package:shared_preferences/shared_preferences.dart';

class TimerSoundPage extends StatefulWidget {
  const TimerSoundPage({super.key, required this.preferences, this.onBack});
  final VoidCallback? onBack;
  final SharedPreferences preferences;
  @override
  State<TimerSoundPage> createState() => _TimerSoundPageState();
}

class _TimerSoundPageState extends State<TimerSoundPage> {
  static final channel = MethodChannel('com.tomatotodo/notification_sounds');
  String event = 'focus';
  List<Map> sounds = [];
  String get key => 'timer_sound_$event';
  @override
  void initState() {
    super.initState();
    load();
  }

  Future<void> load() async {
    try {
      final values = await channel.invokeListMethod<Map>('list');
      if (mounted) setState(() => sounds = values ?? []);
    } catch (_) {
      message(UiText.t("无法读取系统音效，可使用默认音效"));
    }
  }

  void message(String text) {
    if (mounted) {
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(text)));
    }
  }

  Future<void> preview() async {
    try {
      await channel.invokeMethod(
        'preview',
        widget.preferences.getString('${key}_uri'),
      );
    } catch (_) {
      message(UiText.t("音效播放失败，请重新选择"));
    }
  }

  @override
  void dispose() {
    channel.invokeMethod('stop').catchError((Object _) {});
    super.dispose();
  }

  Future<void> select(String uri, String name) async {
    await widget.preferences.setString('${key}_uri', uri);
    await widget.preferences.setString('${key}_name', name);
    if (mounted) setState(() {});
  }

  Future<void> chooseSound() async {
    var uri = widget.preferences.getString('${key}_uri') ?? '';
    var name = widget.preferences.getString('${key}_name') ?? UiText.t("系统默认");
    final result = await showDialog<({String uri, String name})>(
      context: context,
      builder: (dialogContext) => StatefulBuilder(
        builder: (context, update) => AlertDialog(
          title: Text(UiText.t("选择音效")),
          content: SizedBox(
            width: 440,
            height: MediaQuery.sizeOf(context).height * .5,
            child: RadioGroup<String>(
              groupValue: uri,
              onChanged: (value) {
                if (value == null) return;
                update(() {
                  uri = value;
                  name = value.isEmpty
                      ? UiText.t("系统默认")
                      : sounds.firstWhere(
                              (sound) => sound['uri'] == value,
                              orElse: () => {'name': name},
                            )['name']
                            as String;
                });
              },
              child: ListView(
                children: [
                  RadioListTile<String>(value: '', title: Text(UiText.t("系统默认"))),
                  if (uri.isNotEmpty &&
                      !sounds.any((sound) => sound['uri'] == uri))
                    RadioListTile<String>(
                      value: uri,
                      title: Text(name),
                      subtitle: Text(UiText.t("自定义音效")),
                    ),
                  for (final sound in sounds)
                    RadioListTile<String>(
                      value: sound['uri'] as String,
                      title: Text(sound['name'] as String),
                    ),
                  Divider(),
                  ListTile(
                    leading: Icon(Icons.audio_file_outlined),
                    title: Text(UiText.t("选择自定义音效")),
                    subtitle: Text(UiText.t("音频文件，最大 10 MB")),
                    onTap: () async {
                      try {
                        final file = await FilePicker.pickFile(
                          type: FileType.audio,
                        );
                        if (file == null) return;
                        if ((await file.length() ?? 0) > 10 * 1024 * 1024) {
                          message(UiText.t("请选择小于 10 MB 的音效"));
                          return;
                        }
                        final imported = await channel.invokeMethod<String>(
                          'import',
                          file.path,
                        );
                        if (imported != null && context.mounted) {
                          update(() {
                            uri = imported;
                            name = file.name;
                          });
                        }
                      } catch (_) {
                        message(UiText.t("导入失败，请选择可读取的音频文件"));
                      }
                    },
                  ),
                ],
              ),
            ),
          ),
          actions: [
            TextButton.icon(
              onPressed: () async {
                try {
                  await channel.invokeMethod('preview', uri);
                } catch (_) {
                  message(UiText.t("音效播放失败，请重新选择"));
                }
              },
              icon: Icon(Icons.play_arrow),
              label: Text(UiText.t("试听")),
            ),
            TextButton(
              onPressed: () => Navigator.pop(context),
              child: Text(UiText.t("取消")),
            ),
            FilledButton(
              onPressed: () => Navigator.pop(context, (uri: uri, name: name)),
              child: Text(UiText.t("确定")),
            ),
          ],
        ),
      ),
    );
    await channel.invokeMethod('stop').catchError((Object _) {});
    if (result != null && mounted) await select(result.uri, result.name);
  }

  Widget heading(BuildContext context, String title) => Padding(
    padding: EdgeInsets.fromLTRB(16, 24, 16, 12),
    child: Text(
      title,
      style: Theme.of(context).textTheme.titleSmall
          ?.copyWith(color: Theme.of(context).colorScheme.primary),
    ),
  );

  @override
  Widget build(BuildContext context) {
    Localizations.localeOf(context);
    final prefs = widget.preferences;
    final enabled = prefs.getBool('${key}_enabled') ?? true;
    return Scaffold(
      appBar: AppBar(
        automaticallyImplyLeading: widget.onBack == null,
        leading: widget.onBack == null
            ? null
            : IconButton(
                tooltip: UiText.t("返回通用设置"),
                icon: Icon(Icons.arrow_back),
                onPressed: widget.onBack,
              ),
        title: Text(UiText.t("番茄时钟通知音效")),
      ),
      body: SafeArea(
        top: false,
        child: ListView(
          padding: EdgeInsets.fromLTRB(24, 8, 24, 32),
          children: [
            Align(
              alignment: Alignment.centerLeft,
              child: SegmentedButton<String>(
                segments: [
                  ButtonSegment(value: 'focus', label: Text(UiText.t("专注结束"))),
                  ButtonSegment(value: 'break', label: Text(UiText.t("短休结束"))),
                ],
                selected: {event},
                onSelectionChanged: (value) {
                  channel.invokeMethod('stop').catchError((Object _) {});
                  setState(() => event = value.first);
                },
              ),
            ),
            Padding(
              padding: EdgeInsets.fromLTRB(16, 20, 16, 0),
              child: Text(
                event == 'focus' ? UiText.t("专注结束，获得番茄") : UiText.t("短休结束，开始专注"),
                style: Theme.of(context).textTheme.bodyLarge,
              ),
            ),
            heading(context, UiText.t("通知反馈")),
            SwitchListTile(
              secondary: Icon(Icons.volume_up_outlined),
              title: Text(UiText.t("开启音效")),
              subtitle: Text(enabled ? UiText.t("结束时播放所选音效") : UiText.t("结束时静音通知")),
              value: enabled,
              onChanged: (value) async {
                await prefs.setBool('${key}_enabled', value);
                if (!value) {
                  await channel.invokeMethod('stop').catchError((Object _) {});
                }
                if (mounted) setState(() {});
              },
            ),
            SwitchListTile(
              secondary: Icon(Icons.vibration_outlined),
              title: Text(UiText.t("震动")),
              subtitle: Text(UiText.t("结束时震动提醒")),
              value: prefs.getBool('${key}_vibration') ?? true,
              onChanged: (value) async {
                await prefs.setBool('${key}_vibration', value);
                if (mounted) setState(() {});
              },
            ),
            heading(context, UiText.t("通知声音")),
            ListTile(
              leading: Icon(Icons.music_note_outlined),
              title: Text(UiText.t("选择音效")),
              subtitle: Text(prefs.getString('${key}_name') ?? UiText.t("系统默认")),
              trailing: Icon(Icons.chevron_right),
              onTap: chooseSound,
            ),
            ListTile(
              leading: Icon(Icons.play_circle_outline),
              title: Text(UiText.t("试听音效")),
              subtitle: Text(UiText.t("播放当前保存的通知声音")),
              onTap: preview,
            ),
            Padding(
              padding: EdgeInsets.fromLTRB(16, 24, 16, 0),
              child: Text(
                UiText.t("实际音量由系统通知音量和勿扰模式决定。"),
                style: Theme.of(context).textTheme.bodySmall?.copyWith(
                  color: Theme.of(context).colorScheme.onSurfaceVariant,
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
