package com.tomatotodo.tomatotodo

import android.app.PendingIntent
import android.appwidget.AppWidgetManager
import android.appwidget.AppWidgetProvider
import android.content.ComponentName
import android.content.Context
import android.content.Intent
import android.content.res.Configuration
import android.graphics.*
import android.os.Build
import android.os.Bundle
import android.os.SystemClock
import android.net.Uri
import android.util.SizeF
import android.util.TypedValue
import android.view.View
import android.widget.RemoteViews
import org.json.JSONObject
import java.time.LocalDate
import java.time.temporal.ChronoUnit
import kotlin.math.*

open class BaseHomeWidget(private val kind: String) : AppWidgetProvider() {
    override fun onUpdate(context: Context, manager: AppWidgetManager, ids: IntArray) {
        val snapshot = WidgetStore.read(context).optJSONObject("snapshot")
        ids.forEach { HomeWidgetRenderer.update(context, manager, it, kind, snapshot) }
    }
    override fun onAppWidgetOptionsChanged(context: Context, manager: AppWidgetManager, id: Int, options: Bundle) {
        HomeWidgetRenderer.update(context, manager, id, kind, WidgetStore.read(context).optJSONObject("snapshot"))
    }
}
class TimerHomeWidget : BaseHomeWidget("timer")
class CountdownHomeWidget : BaseHomeWidget("countdown")
class QuoteHomeWidget : BaseHomeWidget("quote")
class TasksHomeWidget : BaseHomeWidget("tasks")
class CoursesHomeWidget : BaseHomeWidget("courses")

object HomeWidgetRenderer {
    val providers = linkedMapOf(
        "timer" to TimerHomeWidget::class.java, "countdown" to CountdownHomeWidget::class.java,
        "quote" to QuoteHomeWidget::class.java, "tasks" to TasksHomeWidget::class.java,
        "courses" to CoursesHomeWidget::class.java,
    )
    fun openIntent(context: Context, kind: String): PendingIntent = PendingIntent.getActivity(context, kind.hashCode(),
        Intent(context, MainActivity::class.java).setAction("com.tomatotodo.OPEN.$kind").putExtra("widgetTarget", kind).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_SINGLE_TOP),
        PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE)
    fun updateAll(context: Context, snapshot: JSONObject?) {
        val manager = AppWidgetManager.getInstance(context)
        providers.forEach { (kind, cls) -> manager.getAppWidgetIds(ComponentName(context, cls)).forEach { update(context, manager, it, kind, snapshot) } }
    }
    fun update(context: Context, manager: AppWidgetManager, id: Int, kind: String, snapshot: JSONObject?) {
        val options = manager.getAppWidgetOptions(id)
        if (Build.VERSION.SDK_INT >= 31) {
            @Suppress("DEPRECATION")
            val supplied = options.getParcelableArrayList<SizeF>(AppWidgetManager.OPTION_APPWIDGET_SIZES)
            val sizes = supplied?.filter { it.width > 0 && it.height > 0 }?.take(16)?.takeIf { it.isNotEmpty() } ?: when(kind) {
                "countdown" -> listOf(SizeF(110f, 60f), SizeF(110f, 150f))
                "quote" -> listOf(SizeF(250f, 140f))
                else -> listOf(SizeF(110f, 140f), SizeF(200f, 140f), SizeF(270f, 140f))
            }
            manager.updateAppWidget(id, RemoteViews(sizes.associateWith { render(context, kind, it, snapshot) }))
        } else {
            val width = options.getInt(AppWidgetManager.OPTION_APPWIDGET_MIN_WIDTH, 160).coerceAtLeast(110)
            val height = options.getInt(AppWidgetManager.OPTION_APPWIDGET_MAX_HEIGHT, 200).coerceAtLeast(60)
            manager.updateAppWidget(id, render(context, kind, SizeF(width.toFloat(), height.toFloat()), snapshot))
            if (kind == "tasks") {
                @Suppress("DEPRECATION")
                manager.notifyAppWidgetViewDataChanged(id, R.id.task_rows)
            }
        }
    }
    private fun render(context: Context, kind: String, size: SizeF, raw: JSONObject?): RemoteViews {
        val data = raw ?: JSONObject()
        val dark = when(data.optString("theme")) { "dark" -> true; "light" -> false; else -> context.resources.configuration.uiMode and Configuration.UI_MODE_NIGHT_MASK == Configuration.UI_MODE_NIGHT_YES }
        val colors = data.optJSONObject(if(dark) "dark" else "light") ?: JSONObject()
        fun color(key: String): Int = colors.optLong(key, when(key) {
            "surface" -> if(dark) 0xFF1C211B else 0xFFF0F2E9
            "onSurface" -> if(dark) 0xFFE1E4D9 else 0xFF191D17
            "primary" -> if(dark) 0xFFA8D29E else 0xFF386A31
            "container" -> if(dark) 0xFF20511D else 0xFFC3EFBA
            "onContainer" -> if(dark) 0xFFC3EFBA else 0xFF002201
            "tertiary" -> if(dark) 0xFF234F50 else 0xFFB6EBEB
            else -> if(dark) 0xFFBEC9B9 else 0xFF515C4B
        }).toInt()
        val variant = if(size.width >= 270) "wide" else if(size.width >= 200) "medium" else "small"
        val tall = size.height >= 150
        val layout = when(kind) {
            "timer" -> when { Build.VERSION.SDK_INT < 31 -> R.layout.widget_timer_small; variant == "wide" -> R.layout.widget_timer_wide; variant == "medium" -> R.layout.widget_timer_medium; else -> R.layout.widget_timer_small }
            "tasks" -> R.layout.widget_tasks
            "countdown" -> if(tall) R.layout.widget_countdown_tall else R.layout.widget_countdown_short
            "quote" -> R.layout.widget_quote
            else -> when(variant) { "wide" -> R.layout.widget_list_wide; "medium" -> R.layout.widget_list_medium; else -> R.layout.widget_list_small }
        }
        val views = RemoteViews(context.packageName, layout)
        views.setInt(R.id.widget_root, "setLayoutDirection", if(UiText.language(context) in listOf("ar", "he")) View.LAYOUT_DIRECTION_RTL else View.LAYOUT_DIRECTION_LTR)
        views.setImageViewBitmap(R.id.widget_background, background(size, color("surface"), color("container"), color("tertiary"), kind))
        views.setOnClickPendingIntent(R.id.widget_root, openIntent(context, kind))
        fun text(id: Int, value: String, role: String = "onSurface") { views.setTextViewText(id, value); views.setTextColor(id, color(role)) }
        text(R.id.heading, when(kind) { "timer" -> UiText.t(context, "专注计时"); "countdown" -> UiText.t(context, "倒数日"); "quote" -> UiText.t(context, "每日一句"); "tasks" -> data.optString("list", UiText.t(context, "任务清单")); else -> UiText.t(context, "今日课程") }, "secondary")
        if(kind != "countdown" || tall) views.setInt(R.id.brand, "setColorFilter", color("primary"))
        when(kind) {
            "timer" -> {
                val split = variant != "small" && Build.VERSION.SDK_INT >= 31
                val runtime = data.optJSONObject("runtime") ?: JSONObject()
                val running = runtime.optBoolean("running")
                val up = runtime.optBoolean("countUp")
                val elapsed = WidgetStore.elapsed(runtime)
                val total = WidgetStore.duration(runtime)
                val seconds = if(up) elapsed else max(0, total - elapsed)
                val phase = if(up) UiText.t(context, "正向计时") else if(runtime.optString("phase") == "shortBreak") UiText.t(context, "短休") else UiText.t(context, "专注")
                text(R.id.heading, phase, "secondary")
                text(R.id.time_static, if(!split) "%02d:%02d".format(seconds / 60, seconds % 60) else "%02d".format(seconds / 60), "primary")
                views.setTextColor(R.id.time_live, color("primary"))
                views.setViewVisibility(R.id.time_live, if(running) View.VISIBLE else View.GONE)
                views.setViewVisibility(R.id.time_static, if(running) View.GONE else View.VISIBLE)
                views.setChronometerCountDown(R.id.time_live, !up)
                views.setChronometer(R.id.time_live, SystemClock.elapsedRealtime() + (if(up) -elapsed else seconds) * 1000L, if(!split) null else "%.2s", running)
                text(R.id.list_name, data.optString("list", UiText.t(context, "我的一天")), "secondary")
                text(R.id.task_name, data.optString("task", UiText.t(context, "打开应用选择任务")))
                if(split) {
                    views.setViewVisibility(R.id.seconds_window, View.VISIBLE)
                    views.setViewVisibility(R.id.list_name, View.VISIBLE)
                    views.setViewVisibility(R.id.timer_detail, View.VISIBLE)
                    // Use actual glyph width rather than translating a full clock by a fixed offset.
                    // DP sizing deliberately avoids large accessibility fonts clipping the two rows.
                    val fontDp = min(if(variant == "wide") 56f else 48f, ((size.height - 64f) / 2.4f).coerceAtLeast(24f))
                    val metrics = Paint().apply { typeface = Typeface.MONOSPACE; textSize = fontDp }
                    val digitsWidth = ceil(metrics.measureText("00")) + 1f
                    views.setViewLayoutWidth(R.id.seconds_window, digitsWidth, TypedValue.COMPLEX_UNIT_DIP)
                    views.setViewLayoutWidth(R.id.time_column, digitsWidth + 12f, TypedValue.COMPLEX_UNIT_DIP)
                    for (viewId in listOf(R.id.time_static, R.id.time_live, R.id.time_seconds_static, R.id.time_seconds_live)) {
                        views.setTextViewTextSize(viewId, TypedValue.COMPLEX_UNIT_DIP, fontDp)
                    }
                    val subtitle = data.optString("subtitle").trim()
                    text(R.id.task_subtitle, subtitle, "secondary")
                    views.setViewVisibility(R.id.task_subtitle, if(subtitle.isEmpty()) View.GONE else View.VISIBLE)
                    text(R.id.timer_detail, if(running) UiText.t(context, "进行中") else if(elapsed > 0) UiText.t(context, "已暂停") else UiText.t(context, "准备就绪"), "secondary")
                    if(size.height < 180f) {
                        views.setViewVisibility(R.id.task_subtitle, View.GONE)
                        views.setViewVisibility(R.id.timer_detail, View.GONE)
                        views.setInt(R.id.task_name, "setMaxLines", 1)
                        if(size.height < 165f) views.setViewVisibility(R.id.list_name, View.GONE)
                    }
                    text(R.id.time_seconds_static, "%02d".format(seconds % 60), "primary")
                    views.setTextColor(R.id.time_seconds_live, color("primary"))
                    views.setViewVisibility(R.id.time_seconds_live, if(running) View.VISIBLE else View.GONE)
                    views.setViewVisibility(R.id.time_seconds_static, if(running) View.GONE else View.VISIBLE)
                    views.setChronometerCountDown(R.id.time_seconds_live, !up)
                    views.setChronometer(R.id.time_seconds_live, SystemClock.elapsedRealtime() + (if(up) -elapsed else seconds) * 1000L, "%s", running)
                    if(seconds >= 3600 || up) {
                        // Chronometer changes to H:MM:SS after an hour; use the complete clock
                        // rather than cropping hours into a misleading minute value.
                        views.setViewVisibility(R.id.seconds_window, View.GONE)
                        views.setChronometer(R.id.time_live, SystemClock.elapsedRealtime() + (if(up) -elapsed else seconds) * 1000L, null, running)
                        text(R.id.time_static, "%d:%02d:%02d".format(seconds / 3600, seconds / 60 % 60, seconds % 60), "primary")
                        views.setViewLayoutWidth(R.id.time_column, if(variant == "wide") 112f else 88f, TypedValue.COMPLEX_UNIT_DIP)
                        views.setTextViewTextSize(R.id.time_live, TypedValue.COMPLEX_UNIT_DIP, if(variant == "wide") 22f else 18f)
                        views.setTextViewTextSize(R.id.time_static, TypedValue.COMPLEX_UNIT_DIP, if(variant == "wide") 22f else 18f)
                    }
                } else if(size.height < 180) {
                    views.setViewVisibility(R.id.list_name, View.GONE)
                    views.setViewVisibility(R.id.task_name, View.GONE)
                    views.setTextViewTextSize(R.id.time_static, TypedValue.COMPLEX_UNIT_SP, 32f)
                    views.setTextViewTextSize(R.id.time_live, TypedValue.COMPLEX_UNIT_SP, 32f)
                }
                views.setImageViewBitmap(R.id.action, actionBitmap(context, color("container"), color("onContainer"), running))
                views.setContentDescription(R.id.action, if(running) UiText.t(context, "暂停计时") else UiText.t(context, "开始计时"))
                val toggle = PendingIntent.getBroadcast(context, 808, Intent(context, WidgetActionReceiver::class.java).setAction(WidgetStore.ACTION_TOGGLE), PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE)
                views.setOnClickPendingIntent(R.id.action, if(raw == null) openIntent(context, "timer") else toggle)
            }
            "countdown" -> {
                val target = runCatching { LocalDate.parse(data.optString("countdownDate").take(10)) }.getOrNull()
                val days = target?.let { ChronoUnit.DAYS.between(LocalDate.now(), it) }
                text(R.id.main_title, data.optString("countdownName").ifBlank { UiText.t(context, "期待下一站") })
                text(R.id.large_value, if(days == null) "—" else UiText.f(context, "{0} 天", abs(days)), "primary")
                text(R.id.footer, if(days == null) UiText.t(context, "点按设置日期") else if(tall) "${if(days < 0) "已过" else "距离"} · $target" else if(days < 0) UiText.t(context, "已过") else UiText.t(context, "还有"), "secondary")
            }
            "quote" -> {
                text(R.id.quote, "“${data.optString("quote", "把今天的一小步走稳，就是明天的起点。")}”")
                text(R.id.footer, data.optString("quoteSource", UiText.t(context, "Tomatotodo · 离线语录")), "secondary")
            }
            "tasks" -> {
                TaskWidgetViews.bind(context, views, data, variant == "small")
                val tasks = data.optJSONArray("tasks")
                val count = tasks?.length() ?: 0
                val done = (0 until count).count { tasks!!.getJSONObject(it).optBoolean("done") }
                text(R.id.tasks_empty, UiText.t(context, "暂无任务"), "secondary")
                text(R.id.footer, UiText.f(context, "{0}/{1} 已完成 · 管理清单", done, count), "secondary")
                views.setOnClickPendingIntent(R.id.footer, openIntent(context, "tasks"))
            }
            "courses" -> {
                views.removeAllViews(R.id.rows)
                val rows = mutableListOf<Pair<String,String>>()
                val leading = mutableListOf<String>()
                val trailing = mutableListOf<String>()
                run {
                    val today = LocalDate.now()
                    val weeks = data.optJSONArray("weeks")
                    for(w in 0 until (weeks?.length() ?: 0)) {
                        val week = weeks!!.getJSONObject(w)
                        val monday = runCatching { LocalDate.parse(week.optString("date")) }.getOrNull() ?: continue
                        val courses = week.optJSONArray("events") ?: continue
                        for(i in 0 until courses.length()) {
                            val course = courses.getJSONObject(i)
                            if(monday.plusDays(course.optLong("day")) != today) continue
                            leading.add(course.optString("startTime"))
                            trailing.add(course.optString("room"))
                            rows.add(course.optString("name") to "${course.optString("startTime")}–${course.optString("endTime")} ${course.optString("room")}")
                        }
                    }
                }
                val limit = ((size.height - 84) / (if(variant == "wide") 42 else 45)).toInt().coerceIn(1, 8)
                for((index, row) in rows.take(limit).withIndex()) {
                    val item = RemoteViews(context.packageName, if(variant == "wide") R.layout.widget_row_wide else R.layout.widget_row)
                    if(variant == "wide") {
                        item.setTextViewText(R.id.row_leading, leading[index])
                        item.setTextColor(R.id.row_leading, color("primary"))
                        item.setTextViewText(R.id.row_trailing, trailing[index])
                        item.setTextColor(R.id.row_trailing, color("secondary"))
                    }
                    item.setTextViewText(R.id.row_title, row.first)
                    item.setTextColor(R.id.row_title, if(index % 2 == 0) color("primary") else color("onSurface"))
                    item.setTextViewText(R.id.row_detail, row.second)
                    item.setTextColor(R.id.row_detail, color("secondary"))
                    if(row.second.isBlank()) item.setViewVisibility(R.id.row_detail, View.GONE)
                    views.addView(R.id.rows, item)
                }
                if(rows.isEmpty()) {
                    val item = RemoteViews(context.packageName, R.layout.widget_row)
                    item.setTextViewText(R.id.row_title, UiText.t(context, "今天没有课程"))
                    item.setTextColor(R.id.row_title, color("secondary"))
                    item.setViewVisibility(R.id.row_detail, View.GONE)
                    views.addView(R.id.rows, item)
                }
                text(R.id.footer, UiText.f(context, "{0}/{1} · {2} 门{3}", LocalDate.now().monthValue, LocalDate.now().dayOfMonth, rows.size, if(rows.size > limit) UiText.t(context, " · 点按查看全部") else ""), "secondary")
            }
        }
        return views
    }
    private fun background(size: SizeF, surface: Int, accent: Int, tertiary: Int, kind: String): Bitmap {
        val w = (size.width * 2).toInt().coerceIn(220, 1200)
        val h = (size.height * 2).toInt().coerceIn(120, 1000)
        val bitmap = Bitmap.createBitmap(w, h, Bitmap.Config.ARGB_8888)
        val canvas = Canvas(bitmap)
        val paint = Paint(Paint.ANTI_ALIAS_FLAG)
        val bounds = RectF(0f, 0f, w.toFloat(), h.toFloat())
        val clip = Path().apply { addRoundRect(bounds, 56f, 56f, Path.Direction.CW) }
        canvas.clipPath(clip)
        canvas.drawColor(surface)
        paint.color = accent; paint.alpha = 120
        if(kind == "timer") {
            // Quiet tonal sunrise and rolling hills stay at the outer edge of the content.
            paint.alpha = 60
            canvas.drawCircle(w * .94f, h * .17f, h * .24f, paint)
            paint.color = tertiary; paint.alpha = 55
            canvas.drawPath(Path().apply {
                moveTo(w * .38f, h.toFloat())
                cubicTo(w * .68f, h * .52f, w * .81f, h * .94f, w.toFloat(), h * .54f)
                lineTo(w.toFloat(), h.toFloat()); close()
            }, paint)
            paint.color = accent; paint.alpha = 90
            canvas.drawPath(Path().apply {
                moveTo(w * .57f, h.toFloat())
                cubicTo(w * .78f, h * .71f, w * .9f, h * .96f, w.toFloat(), h * .8f)
                lineTo(w.toFloat(), h.toFloat()); close()
            }, paint)
        }
        else if(kind == "countdown") {
            for(i in 0..7) { canvas.save(); canvas.translate(w * .96f, h * .45f); canvas.rotate(i * 45f); canvas.drawOval(RectF(-w*.15f,-h*.13f,w*.35f,h*.13f),paint); canvas.restore() }
        } else { canvas.drawCircle(w*.95f, h*.12f, h*.65f,paint); paint.color=tertiary; paint.alpha=95; canvas.drawCircle(w*.72f,h*1.2f,h*.5f,paint) }
        return bitmap
    }
    private fun actionBitmap(context: Context, fill: Int, foreground: Int, running: Boolean): Bitmap {
        val bitmap = Bitmap.createBitmap(216, 96, Bitmap.Config.ARGB_8888)
        val canvas = Canvas(bitmap)
        val paint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = fill }
        canvas.drawRoundRect(RectF(0f,0f,216f,96f),48f,48f,paint)
        paint.color = foreground
        paint.textSize = 28f; paint.typeface = Typeface.create("sans-serif-medium",Typeface.NORMAL)
        val label = if(running) UiText.t(context, "暂停") else UiText.t(context, "开始")
        val textWidth = paint.measureText(label)
        val groupLeft = (216f - (24f + 12f + textWidth)) / 2f
        if(running) {
            canvas.drawRoundRect(RectF(groupLeft,33f,groupLeft+8f,63f),2f,2f,paint)
            canvas.drawRoundRect(RectF(groupLeft+15f,33f,groupLeft+23f,63f),2f,2f,paint)
        } else {
            val path = Path().apply { moveTo(groupLeft,31f); lineTo(groupLeft+24f,48f); lineTo(groupLeft,65f); close() }
            canvas.drawPath(path,paint)
        }
        val baseline = 48f - (paint.fontMetrics.ascent + paint.fontMetrics.descent) / 2f
        canvas.drawText(label,groupLeft+36f,baseline,paint)
        return bitmap
    }
}
