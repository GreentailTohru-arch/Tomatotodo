package com.tomatotodo.tomatotodo

import android.os.Build
import android.content.Intent
import android.content.ComponentName
import android.appwidget.AppWidgetManager
import io.flutter.embedding.android.FlutterActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodChannel
import java.time.ZoneId
import java.util.TimeZone

class MainActivity : FlutterActivity() {
    private var previewSound: android.media.Ringtone? = null
    override fun onStop() {
        previewSound?.stop()
        super.onStop()
    }
    private var widgetsChannel: MethodChannel? = null
    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        setIntent(intent)
        intent.getStringExtra("widgetTarget")?.let { widgetsChannel?.invokeMethod("open", it) }
    }
    override fun cleanUpFlutterEngine(flutterEngine: FlutterEngine) {
        WidgetStore.changed = null
        widgetsChannel?.setMethodCallHandler(null)
        widgetsChannel = null
        super.cleanUpFlutterEngine(flutterEngine)
    }
    override fun configureFlutterEngine(flutterEngine: FlutterEngine) {
        super.configureFlutterEngine(flutterEngine)
        MethodChannel(flutterEngine.dartExecutor.binaryMessenger, "com.tomatotodo/notification_sounds")
            .setMethodCallHandler { call, result ->
                try {
                    when (call.method) {
                        "list" -> {
                            val manager = android.media.RingtoneManager(this)
                            manager.setType(android.media.RingtoneManager.TYPE_NOTIFICATION)
                            val cursor = manager.cursor
                            val sounds = mutableListOf<Map<String, String>>()
                            while (cursor.moveToNext()) sounds.add(mapOf("name" to cursor.getString(android.media.RingtoneManager.TITLE_COLUMN_INDEX), "uri" to manager.getRingtoneUri(cursor.position).toString()))
                            cursor.close()
                            result.success(sounds)
                        }
                        "preview" -> {
                            previewSound?.stop()
                            val raw = call.arguments as? String
                            val uri = if (raw.isNullOrEmpty()) android.media.RingtoneManager.getDefaultUri(android.media.RingtoneManager.TYPE_NOTIFICATION) else android.net.Uri.parse(raw)
                            previewSound = android.media.RingtoneManager.getRingtone(this, uri)
                            previewSound?.play()
                            val playing = previewSound
                            android.os.Handler(android.os.Looper.getMainLooper()).postDelayed({ playing?.stop() }, 8000)
                            result.success(null)
                        }
                        "stop" -> { previewSound?.stop(); result.success(null) }
                        "import" -> {
                            val source = java.io.File(call.arguments as String)
                            require(source.length() <= 10 * 1024 * 1024)
                            val name = "tomatotodo_sound_${System.currentTimeMillis()}.${source.extension}"
                            val values = android.content.ContentValues().apply {
                                put(android.provider.MediaStore.Audio.Media.DISPLAY_NAME, name)
                                put(android.provider.MediaStore.Audio.Media.MIME_TYPE, "audio/*")
                                put(android.provider.MediaStore.Audio.Media.IS_NOTIFICATION, true)
                                if (Build.VERSION.SDK_INT >= 29) put(android.provider.MediaStore.Audio.Media.RELATIVE_PATH, "Notifications/Tomatotodo")
                            }
                            val uri = contentResolver.insert(android.provider.MediaStore.Audio.Media.EXTERNAL_CONTENT_URI, values) ?: error(UiText.t(this, "无法导入音效"))
                            try { contentResolver.openOutputStream(uri)!!.use { out -> source.inputStream().use { it.copyTo(out) } } }
                            catch (e: Exception) { contentResolver.delete(uri, null, null); throw e }
                            result.success(uri.toString())
                        }
                        else -> result.notImplemented()
                    }
                } catch (e: Exception) { result.error("SOUND", UiText.t(this, "无法读取或播放音效"), null) }
            }
        MethodChannel(flutterEngine.dartExecutor.binaryMessenger, "com.tomatotodo/app_updates")
            .setMethodCallHandler { call, result ->
                try {
                    when (call.method) {
                        "version" -> {
                            @Suppress("DEPRECATION")
                            val info = packageManager.getPackageInfo(packageName, 0)
                            @Suppress("DEPRECATION")
                            val code = if (Build.VERSION.SDK_INT >= 28) info.longVersionCode else info.versionCode.toLong()
                            result.success(mapOf("version" to info.versionName, "build" to code.toString()))
                        }
                        "openDownload" -> {
                            val raw = call.arguments as? String ?: throw IllegalArgumentException()
                            val uri = java.net.URI(raw)
                            require(uri.scheme == "https" && !uri.host.isNullOrEmpty() && uri.userInfo == null)
                            startActivity(Intent(Intent.ACTION_VIEW, android.net.Uri.parse(raw)))
                            result.success(null)
                        }
                        else -> result.notImplemented()
                    }
                } catch (e: Exception) { result.error("APP_UPDATE", UiText.t(this, "无法读取版本或打开下载链接"), null) }
            }
        MethodChannel(flutterEngine.dartExecutor.binaryMessenger, "com.tomatotodo/cloud_vault")
            .setMethodCallHandler { call, result ->
                try {
                    when (call.method) {
                        "readLogin" -> result.success(CloudTokenVault.read(this, "remembered_login"))
                        "writeLogin" -> { CloudTokenVault.write(this, call.arguments as? String, "remembered_login"); result.success(null) }
                        "read" -> result.success(CloudTokenVault.read(this))
                        "write" -> { CloudTokenVault.write(this, call.arguments as? String); result.success(null) }
                        else -> result.notImplemented()
                    }
                } catch (e: Exception) { result.error("CLOUD_VAULT", UiText.t(this, "云端会话读取失败，请重新登录"), null) }
            }
        widgetsChannel = MethodChannel(flutterEngine.dartExecutor.binaryMessenger, "com.tomatotodo/home_widgets").also { channel ->
            channel.setMethodCallHandler { call, result ->
                try {
                    when(call.method) {
                        "read" -> result.success(WidgetStore.read(this).toString())
                        "publish" -> result.success(WidgetStore.publish(this, call.argument<String>("snapshot") ?: "{}", call.argument<Int>("revision") ?: 0, call.argument<Number>("ack")?.toLong() ?: 0).toString())
                        "command" -> result.success(WidgetStore.command(this, call.arguments as String).toString())
                        "launchTarget" -> { result.success(intent.getStringExtra("widgetTarget")); intent.removeExtra("widgetTarget") }
                        "pin" -> {
                            val provider = HomeWidgetRenderer.providers[call.arguments as? String]
                            val manager = AppWidgetManager.getInstance(this)
                            result.success(provider != null && Build.VERSION.SDK_INT >= 26 && manager.isRequestPinAppWidgetSupported && manager.requestPinAppWidget(ComponentName(this, provider), null, null))
                        }
                        else -> result.notImplemented()
                    }
                } catch(e: Exception) { result.error("HOME_WIDGET", e.message, null) }
            }
            WidgetStore.changed = { runOnUiThread { channel.invokeMethod("changed", null) } }
        }
        MethodChannel(flutterEngine.dartExecutor.binaryMessenger, "com.tomatotodo/timezone")
            .setMethodCallHandler { call, result ->
                if (call.method == "getLocalTimezone") {
                    val timezone = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                        ZoneId.systemDefault().id
                    } else {
                        TimeZone.getDefault().id
                    }
                    result.success(timezone)
                } else {
                    result.notImplemented()
                }
            }
    }
}
