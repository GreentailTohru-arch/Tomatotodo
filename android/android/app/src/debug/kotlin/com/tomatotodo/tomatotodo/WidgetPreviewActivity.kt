package com.tomatotodo.tomatotodo

import android.app.Activity
import android.appwidget.AppWidgetHost
import android.appwidget.AppWidgetManager
import android.content.ComponentName
import android.os.Bundle
import android.os.Handler
import android.os.Looper
import android.util.SizeF
import android.widget.LinearLayout
import android.widget.ScrollView
import android.widget.TextView
import org.json.JSONObject

/** Debug-only real AppWidgetHost for responsive-layout and PendingIntent QA. */
class WidgetPreviewActivity : Activity() {
    override fun onNewIntent(intent: android.content.Intent) {
        super.onNewIntent(intent)
        setIntent(intent)
        recreate()
    }
    private lateinit var host: AppWidgetHost
    private val ids = mutableListOf<Int>()
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        host = AppWidgetHost(this, 9001)
        host.deleteHost()
        host.startListening()
        val manager = AppWidgetManager.getInstance(this)
        val list = LinearLayout(this).apply { orientation = LinearLayout.VERTICAL; setPadding(12,32,12,32) }
        val kind = intent.getStringExtra("kind") ?: "timer"
        val snapshot = WidgetStore.read(this).optJSONObject("snapshot")?.let { JSONObject(it.toString()) }
        if (intent.getBooleanExtra("previewRunning", false)) {
            snapshot?.optJSONObject("runtime")?.apply {
                put("running", true)
                put("elapsed", 30)
                put("startedAt", System.currentTimeMillis())
            }
        }
        val sizes = when(kind) {
            "countdown" -> listOf(SizeF(170f,100f), SizeF(170f,210f))
            "quote" -> listOf(SizeF(340f,210f))
            else -> listOf(SizeF(170f,210f), SizeF(250f,210f), SizeF(340f,210f))
        }
        for (size in sizes) {
            list.addView(TextView(this).apply { text = "$kind  ${size.width.toInt()} × ${size.height.toInt()} dp"; setTextColor(0xFFFFFFFF.toInt()) })
            val id = host.allocateAppWidgetId(); ids.add(id)
            val cls = HomeWidgetRenderer.providers.getValue(kind)
            val options = Bundle().apply {
                putInt(AppWidgetManager.OPTION_APPWIDGET_MIN_WIDTH, size.width.toInt())
                putInt(AppWidgetManager.OPTION_APPWIDGET_MAX_WIDTH, size.width.toInt())
                putInt(AppWidgetManager.OPTION_APPWIDGET_MIN_HEIGHT, size.height.toInt())
                putInt(AppWidgetManager.OPTION_APPWIDGET_MAX_HEIGHT, size.height.toInt())
                putParcelableArrayList(AppWidgetManager.OPTION_APPWIDGET_SIZES, arrayListOf(size))
            }
            if (!manager.bindAppWidgetIdIfAllowed(id, ComponentName(this, cls), options)) {
                list.addView(TextView(this).apply { text = "Widget bind permission required" }); continue
            }
            val view = host.createView(this, id, manager.getAppWidgetInfo(id))
            view.setPadding(0,0,0,0)
            list.addView(view, LinearLayout.LayoutParams((size.width*resources.displayMetrics.density).toInt(), (size.height*resources.displayMetrics.density).toInt()))
            HomeWidgetRenderer.update(this, manager, id, kind, snapshot)
            if (intent.getBooleanExtra("previewRunning", false)) {
                Handler(Looper.getMainLooper()).postDelayed({
                    HomeWidgetRenderer.update(this, manager, id, kind, snapshot)
                }, 1200)
            }
        }
        setContentView(ScrollView(this).apply { setBackgroundColor(0xFF252525.toInt()); addView(list) })
    }
    override fun onDestroy() { host.stopListening(); ids.forEach { host.deleteAppWidgetId(it) }; super.onDestroy() }
}
