import 'package:tomatotodo/core/localization/ui_text.dart';
import 'dart:io';
import 'dart:convert';
import 'dart:ui' as ui;

import 'package:file_picker/file_picker.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';

import 'avatar_crop_page.dart';

import 'package:path_provider/path_provider.dart';
import 'package:shared_preferences/shared_preferences.dart';

class LocalProfile extends ChangeNotifier {
  LocalProfile(this.preferences) {
    name = preferences.getString('local_profile_name') ?? UiText.t("本地用户");
    ready = _loadAvatar();
  }

  final SharedPreferences preferences;
  late String name;
  Uint8List? avatarBytes;
  late final Future<void> ready;
  Map<String, dynamic> exportData() => {
    'Nickname': name,
    'Biography': preferences.getString('profile_biography') ?? '',
    'AvatarBase64': avatarBytes == null ? null : base64Encode(avatarBytes!),
  };

  Future<void> applyData(Map data) async {
    final nickname = data['Nickname'];
    if (nickname is String && nickname.trim().isNotEmpty) name = nickname;
    await preferences.setString('local_profile_name', name);
    await preferences.setString(
      'profile_biography',
      data['Biography'] as String? ?? '',
    );
    final encoded = data['AvatarBase64'];
    avatarBytes = encoded is String ? base64Decode(encoded) : null;
    try {
      final directory = await getApplicationSupportDirectory();
      final file = File('${directory.path}/profile_avatar');
      if (avatarBytes != null) {
        await file.writeAsBytes(avatarBytes!);
      } else if (await file.exists()) {
        await file.delete();
      }
    } catch (_) {}
    notifyListeners();
  }

  Future<void> _loadAvatar() async {
    try {
      final directory = await getApplicationSupportDirectory();
      final file = File('${directory.path}/profile_avatar');
      if (await file.exists()) {
        avatarBytes = await file.readAsBytes();
        notifyListeners();
      }
    } catch (_) {
      // A missing local avatar leaves the initials avatar available.
    }
  }

  Future<void> rename(String value) async {
    final trimmed = value.trim();
    if (trimmed.isEmpty) return;
    name = trimmed;
    notifyListeners();
    await preferences.setString('local_profile_name', trimmed);
  }

  Future<bool> chooseAvatar(BuildContext context) async {
    final picked = await FilePicker.pickFile(type: FileType.image);
    if (picked == null) return false;
    if ((await picked.length() ?? 0) > 10 * 1024 * 1024) {
      throw FormatException(UiText.t("请选择小于 10 MB 的图片"));
    }
    var bytes = await picked.readAsBytes();
    if (bytes.isEmpty || bytes.length > 10 * 1024 * 1024) {
      throw FormatException(UiText.t("请选择小于 10 MB 的图片"));
    }
    final codec = await ui.instantiateImageCodec(bytes);
    final frame = await codec.getNextFrame();
    final source = frame.image;
    if (!context.mounted) {
      source.dispose();
      codec.dispose();
      return false;
    }
    final route = MaterialPageRoute<ui.Rect>(
      builder: (_) => AvatarCropPage(image: source),
    );
    final crop = await Navigator.of(context).push(route);
    await route.completed;
    if (crop == null) {
      source.dispose();
      codec.dispose();
      return false;
    }
    final recorder = ui.PictureRecorder();
    final canvas = ui.Canvas(recorder);
    canvas.drawImageRect(
      source,
      crop,
      ui.Rect.fromLTWH(0, 0, 256, 256),
      ui.Paint()..filterQuality = ui.FilterQuality.high,
    );
    final picture = recorder.endRecording();
    final avatar = await picture.toImage(256, 256);
    bytes = (await avatar.toByteData(format: ui.ImageByteFormat.png))!.buffer
        .asUint8List();
    avatar.dispose();
    picture.dispose();
    source.dispose();
    codec.dispose();
    final directory = await getApplicationSupportDirectory();
    await File('${directory.path}/profile_avatar').writeAsBytes(bytes);
    avatarBytes = bytes;
    notifyListeners();
    return true;
  }
}
