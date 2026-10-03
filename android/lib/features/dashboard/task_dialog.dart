import 'package:tomatotodo/core/localization/ui_text.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import 'dashboard_controller.dart';

Future<void> showTaskDialog(
  BuildContext context,
  DashboardController controller, {
  FocusTask? task,
}) async {
  var title = task?.title ?? '';
  var estimateText = task == null ? '3' : '${task.estimate}';
  final result = await showDialog<({String title, int estimate})>(
    context: context,
    builder: (dialogContext) => AlertDialog(
      title: Text(task == null ? UiText.t("添加任务") : UiText.t("编辑任务")),
      content: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            TextFormField(
              initialValue: title,
              onChanged: (value) => title = value,
              autofocus: true,
              maxLength: 120,
              textInputAction: TextInputAction.next,
              decoration: InputDecoration(
                border: OutlineInputBorder(),
                labelText: UiText.t("任务名称"),
              ),
            ),
            SizedBox(height: 12),
            TextFormField(
              initialValue: estimateText,
              onChanged: (value) => estimateText = value,
              keyboardType: TextInputType.number,
              inputFormatters: [FilteringTextInputFormatter.digitsOnly],
              decoration: InputDecoration(
                border: OutlineInputBorder(),
                labelText: UiText.t("目标番茄数"),
                helperText: UiText.t("0 表示不设置目标"),
                prefixIcon: Icon(Icons.timer_outlined),
              ),
            ),
          ],
        ),
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.pop(dialogContext),
          child: Text(UiText.t("取消")),
        ),
        FilledButton(
          onPressed: () => Navigator.pop(dialogContext, (
            title: title.trim(),
            estimate: (int.tryParse(estimateText) ?? 0).clamp(0, 99),
          )),
          child: Text(UiText.t("保存")),
        ),
      ],
    ),
  );
  if (result == null || result.title.isEmpty) return;
  if (task == null) {
    controller.addTask(result.title, estimate: result.estimate);
  } else {
    controller.updateTask(
      task.id,
      result.title,
      task.subtitle,
      result.estimate,
    );
  }
}
