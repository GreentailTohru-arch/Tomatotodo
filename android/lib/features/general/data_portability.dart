import 'package:tomatotodo/core/localization/ui_text.dart';
import 'dart:convert';
import 'dart:typed_data';

import 'package:file_picker/file_picker.dart';

Future<Object?> pickJsonFile() async {
  final file = await FilePicker.pickFile(
    type: FileType.custom,
    allowedExtensions: ['json'],
  );
  if (file == null) return null;
  final size = file.lengthSync() ?? await file.length();
  if (size != null && size > 10 * 1024 * 1024) {
    throw FormatException(UiText.t("JSON 文件不能超过 10 MB"));
  }
  final bytes = await file.readAsBytes();
  return jsonDecode(utf8.decode(bytes));
}

Future<bool> saveJsonFile(String name, Object data) async {
  final bytes = Uint8List.fromList(
    utf8.encode(JsonEncoder.withIndent('  ').convert(data)),
  );
  return await FilePicker.saveFile(
        dialogTitle: UiText.t("保存 Tomatotodo JSON"),
        fileName: name,
        bytes: bytes,
      ) !=
      null;
}
