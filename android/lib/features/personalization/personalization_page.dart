import 'package:tomatotodo/core/localization/ui_text.dart';
import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../../core/settings/app_settings.dart';

class PersonalizationPage extends StatelessWidget {
  const PersonalizationPage({
    super.key,
    required this.settings,
    this.embedded = false,
  });
  final bool embedded;
  final AppSettings settings;
  static List<(String, Color)> get seeds => <(String, Color)>[
    (UiText.t("森林"), Color(0xFF30643B)),
    (UiText.t("暖橙"), Color(0xFF9B4B25)),
    (UiText.t("蓝海"), Color(0xFF386A9B)),
    (UiText.t("暖黄"), Color(0xFF766500)),
    (UiText.t("青瓷"), Color(0xFF006B5E)),
    (UiText.t("草绿"), Color(0xFF4D6B2C)),
    (UiText.t("玫瑰"), Color(0xFF984061)),
    (UiText.t("紫藤"), Color(0xFF70558B)),
  ];
  static List<(DynamicSchemeVariant, String)> get variants => <(DynamicSchemeVariant, String)>[
    (DynamicSchemeVariant.tonalSpot, UiText.t("调性点缀")),
    (DynamicSchemeVariant.fidelity, UiText.t("高保真")),
    (DynamicSchemeVariant.monochrome, UiText.t("单色")),
    (DynamicSchemeVariant.neutral, UiText.t("中性")),
    (DynamicSchemeVariant.vibrant, UiText.t("活力")),
    (DynamicSchemeVariant.expressive, UiText.t("表现力")),
    (DynamicSchemeVariant.content, UiText.t("内容主题")),
    (DynamicSchemeVariant.rainbow, UiText.t("彩虹")),
    (DynamicSchemeVariant.fruitSalad, UiText.t("果缤纷")),
  ];

  @override
  Widget build(BuildContext context) => UiText.watch(context, () => Scaffold(
    appBar: embedded ? null : AppBar(title: Text(UiText.t("个性化"))),
    body: ListenableBuilder(
      listenable: settings,
      builder: (context, _) => ListView(
        padding: EdgeInsets.all(24),
        children: [
          if (embedded) ...[
            Text(UiText.t("个性化"), style: Theme.of(context).textTheme.headlineSmall),
            SizedBox(height: 24),
          ],
          _heading(context, Icons.brightness_6_outlined, UiText.t("主题模式")),
          SizedBox(height: 20),
          Row(
            children: [
              for (final item in [
                (ThemeMode.system, UiText.t("自动"), Icons.brightness_auto_outlined),
                (ThemeMode.light, UiText.t("浅色"), Icons.light_mode_outlined),
                (ThemeMode.dark, UiText.t("深色"), Icons.dark_mode_outlined),
              ]) ...[
                Expanded(child: _modeCard(context, item.$1, item.$2, item.$3)),
                if (item.$1 != ThemeMode.dark) SizedBox(width: 8),
              ],
            ],
          ),
          SizedBox(height: 24),
          SwitchListTile(
            secondary: Icon(Icons.wallpaper_outlined),
            title: Text(UiText.t("跟随系统主题色")),
            subtitle: Text(
              settings.systemColorAvailable
                  ? UiText.t("使用设备当前的 Material You 色板")
                  : UiText.t("此设备暂未提供系统色板，启用后仍可使用下方预设色"),
            ),
            value: settings.useSystemColor,
            onChanged: settings.setUseSystemColor,
          ),
          SizedBox(height: 12),
          Row(
            children: [
              Expanded(
                child: _heading(context, Icons.palette_outlined, UiText.t("主题色彩")),
              ),
              TextButton.icon(
                onPressed: () => _chooseVariant(context),
                icon: Icon(Icons.tune, size: 18),
                label: Text(
                  variants.firstWhere((v) => v.$1 == settings.schemeVariant).$2,
                ),
              ),
            ],
          ),
          SizedBox(height: 16),
          LayoutBuilder(
            builder: (context, constraints) {
              final columns = constraints.maxWidth < 320
                  ? 3
                  : constraints.maxWidth < 480
                  ? 4
                  : ((constraints.maxWidth + 12) / 112).ceil();
              final cell =
                  (constraints.maxWidth - (columns - 1) * 12) / columns;
              final items = <(String, Color)>[
                ...seeds,
                if (settings.customSeedColor case final Color custom)
                  (UiText.t("自定义"), custom),
              ];
              return Wrap(
                spacing: 12,
                runSpacing: 12,
                children: [
                  for (final item in items)
                    SizedBox(
                      width: cell,
                      height: cell,
                      child: _seedTile(context, item.$1, item.$2),
                    ),
                  SizedBox(
                    width: cell,
                    height: cell,
                    child: Card(
                      margin: EdgeInsets.zero,
                      color: Theme.of(context).colorScheme.primaryContainer,
                      child: InkWell(
                        borderRadius: BorderRadius.circular(12),
                        onTap: () => _chooseCustom(context),
                        child: Center(
                          child: Icon(
                            Icons.add,
                            size: 32,
                            color: Theme.of(context)
                                .colorScheme
                                .onPrimaryContainer,
                          ),
                        ),
                      ),
                    ),
                  ),
                ],
              );
            },
          ),
          SizedBox(height: 24),
          SwitchListTile(
            secondary: Icon(Icons.contrast),
            title: Text(UiText.t("纯黑模式")),
            subtitle: Text(UiText.t("深色主题使用纯黑背景")),
            value: settings.pureBlack,
            onChanged: settings.setPureBlack,
          ),
          SwitchListTile(
            secondary: Icon(Icons.text_fields),
            title: Text(UiText.t("文本缩放")),
            value: settings.textScaleEnabled,
            onChanged: settings.setTextScaleEnabled,
          ),
          Row(
            children: [
              Expanded(
                child: Slider(
                  min: 0.85,
                  max: 1.25,
                  divisions: 8,
                  value: settings.textScale,
                  label: '${(settings.textScale * 100).round()}%',
                  onChanged: settings.textScaleEnabled
                      ? settings.setTextScale
                      : null,
                ),
              ),
              SizedBox(
                width: 54,
                child: Text(
                  '${(settings.textScale * 100).round()}%',
                  textAlign: TextAlign.end,
                  style: Theme.of(context).textTheme.labelLarge,
                ),
              ),
            ],
          ),
        ],
      ),
    ),
  ));

  Widget _heading(BuildContext context, IconData icon, String label) => Padding(
    padding: EdgeInsets.symmetric(horizontal: 16, vertical: 8),
    child: Row(
      children: [
        Icon(
          icon,
          size: 24,
          color: Theme.of(context).colorScheme.onSurfaceVariant,
        ),
        SizedBox(width: 16),
        Expanded(
          child: Text(label, style: Theme.of(context).textTheme.titleMedium),
        ),
      ],
    ),
  );

  Widget _modeCard(
    BuildContext context,
    ThemeMode mode,
    String label,
    IconData icon,
  ) {
    final selected = settings.themeMode == mode;
    final scheme = Theme.of(context).colorScheme;
    return Card(
      margin: EdgeInsets.zero,
      color: selected ? scheme.primaryContainer : scheme.surfaceContainerLow,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(16),
        side: BorderSide(
          color: selected ? scheme.primary : scheme.outlineVariant,
        ),
      ),
      child: InkWell(
        borderRadius: BorderRadius.circular(16),
        onTap: () => settings.setThemeMode(mode),
        child: SizedBox(
          height: 72,
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Icon(icon),
              SizedBox(height: 4),
              Text(label, style: Theme.of(context).textTheme.labelLarge),
            ],
          ),
        ),
      ),
    );
  }

  Widget _seedTile(BuildContext context, String name, Color seed) {
    final selected = !settings.useSystemColor && settings.seedColor == seed;
    final scheme = Theme.of(context).colorScheme;
    final preview = ColorScheme.fromSeed(
      seedColor: seed,
      brightness: Theme.of(context).brightness,
      dynamicSchemeVariant: settings.schemeVariant,
    );
    return Semantics(
      label: UiText.f("{0}主题色", [name]),
      selected: selected,
      button: true,
      child: Card(
        margin: EdgeInsets.zero,
        color: selected ? scheme.primaryContainer : scheme.surfaceContainerLow,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(16),
          side: BorderSide(
            color: selected ? scheme.primary : scheme.outlineVariant,
            width: selected ? 2 : 1,
          ),
        ),
        child: InkWell(
          borderRadius: BorderRadius.circular(16),
          onTap: () => settings.setSeedColor(seed),
          child: Padding(
            padding: EdgeInsets.all(10),
            child: Stack(
              alignment: Alignment.center,
              children: [
                CustomPaint(
                  painter: _PalettePainter(preview),
                  child: SizedBox.expand(),
                ),
                if (selected)
                  CircleAvatar(
                    radius: 15,
                    backgroundColor: preview.primary,
                    child: Icon(
                      Icons.check,
                      color: preview.onPrimary,
                      size: 20,
                    ),
                  ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  Future<void> _chooseVariant(BuildContext context) async {
    final chosen = await showDialog<DynamicSchemeVariant>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text(UiText.t("配色方案")),
        contentPadding: EdgeInsets.fromLTRB(8, 12, 8, 12),
        content: SizedBox(
          width: 320,
          child: ListView(
            shrinkWrap: true,
            children: [
              for (final item in variants)
                ListTile(
                  leading: Icon(
                    settings.schemeVariant == item.$1
                        ? Icons.radio_button_checked
                        : Icons.radio_button_unchecked,
                  ),
                  title: Text(item.$2),
                  onTap: () => Navigator.pop(context, item.$1),
                ),
            ],
          ),
        ),
      ),
    );
    if (chosen != null) await settings.setSchemeVariant(chosen);
  }

  Future<void> _chooseCustom(BuildContext context) async {
    final chosen = await showDialog<Color>(
      context: context,
      builder: (context) => _CustomColorDialog(
        initial: settings.customSeedColor ?? settings.seedColor,
      ),
    );
    if (chosen != null) await settings.setCustomSeedColor(chosen);
  }
}

class _PalettePainter extends CustomPainter {
  const _PalettePainter(this.scheme);
  final ColorScheme scheme;
  @override
  void paint(Canvas canvas, Size size) {
    final radius = math.min(size.width, size.height) / 2;
    final rect = Rect.fromCircle(
      center: Offset(size.width / 2, size.height / 2),
      radius: radius,
    );
    final colors = [
      scheme.primary,
      scheme.primaryContainer,
      scheme.secondaryContainer,
      scheme.tertiaryContainer,
    ];
    for (var i = 0; i < 4; i++) {
      canvas.drawArc(
        rect,
        -math.pi / 2 + i * math.pi / 2,
        math.pi / 2,
        true,
        Paint()..color = colors[i],
      );
    }
  }

  @override
  bool shouldRepaint(_PalettePainter old) => old.scheme != scheme;
}

class _CustomColorDialog extends StatefulWidget {
  const _CustomColorDialog({required this.initial});
  final Color initial;
  @override
  State<_CustomColorDialog> createState() => _CustomColorDialogState();
}

class _CustomColorDialogState extends State<_CustomColorDialog> {
  late HSVColor color = HSVColor.fromColor(widget.initial);
  late final TextEditingController hex = TextEditingController(
    text: widget.initial
        .toARGB32()
        .toRadixString(16)
        .substring(2)
        .toUpperCase(),
  );
  void update(HSVColor next) => setState(() {
    color = next;
    hex.text = next
        .toColor()
        .toARGB32()
        .toRadixString(16)
        .substring(2)
        .toUpperCase();
  });
  @override
  void dispose() {
    hex.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    Localizations.localeOf(context);
    final preview = ColorScheme.fromSeed(seedColor: color.toColor());
    return AlertDialog(
      title: Text(UiText.t("自定义主题色")),
      content: SizedBox(
        width: 320,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Container(
              height: 56,
              decoration: BoxDecoration(
                borderRadius: BorderRadius.circular(16),
                gradient: LinearGradient(
                  colors: [
                    preview.primary,
                    preview.secondary,
                    preview.tertiary,
                    preview.primaryContainer,
                  ],
                ),
              ),
            ),
            SizedBox(height: 16),
            _slider(UiText.t("色相"), color.hue, 360, (v) => update(color.withHue(v))),
            _slider(
              UiText.t("饱和度"),
              color.saturation,
              1,
              (v) => update(color.withSaturation(v)),
            ),
            _slider(UiText.t("明度"), color.value, 1, (v) => update(color.withValue(v))),
            TextField(
              controller: hex,
              maxLength: 6,
              inputFormatters: [
                FilteringTextInputFormatter.allow(RegExp('[0-9a-fA-F]')),
              ],
              decoration: InputDecoration(
                prefixText: '#',
                labelText: UiText.t("十六进制颜色"),
                border: OutlineInputBorder(),
              ),
              onChanged: (value) {
                if (value.length == 6) {
                  final parsed = int.tryParse(value, radix: 16);
                  if (parsed != null) {
                    setState(
                      () => color = HSVColor.fromColor(
                        Color(0xFF000000 | parsed),
                      ),
                    );
                  }
                }
              },
            ),
          ],
        ),
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.pop(context),
          child: Text(UiText.t("取消")),
        ),
        FilledButton(
          onPressed: () => Navigator.pop(context, color.toColor()),
          child: Text(UiText.t("应用")),
        ),
      ],
    );
  }

  Widget _slider(
    String label,
    double value,
    double max,
    ValueChanged<double> onChanged,
  ) => Row(
    children: [
      SizedBox(width: 54, child: Text(label)),
      Expanded(
        child: Slider(min: 0, max: max, value: value, onChanged: onChanged),
      ),
    ],
  );
}
