package com.tomatotodo.tomatotodo

import android.app.*
import android.appwidget.AppWidgetManager
import android.content.*
import android.os.Build
import org.json.JSONArray
import org.json.JSONObject
import kotlin.math.max

/** A single atomic journal bridges launcher processes and the Flutter clock. */
object WidgetStore {
    const val ACTION_TICK = "com.tomatotodo.WIDGET_TICK"
    const val ACTION_TOGGLE = "com.tomatotodo.WIDGET_TOGGLE"
    const val ACTION_TASK = "com.tomatotodo.WIDGET_TASK"
    var changed: (() -> Unit)? = null
    private fun prefs(context: Context) = context.getSharedPreferences("tomatotodo_home_widgets", Context.MODE_PRIVATE)
    private fun load(context: Context): JSONObject = runCatching {
        JSONObject(prefs(context).getString("state", null) ?: "{}")
    }.getOrDefault(JSONObject())
    private fun save(context: Context, state: JSONObject) { prefs(context).edit().putString("state", state.toString()).commit() }
    private fun copy(value: JSONObject) = JSONObject(value.toString())
    fun elapsed(runtime: JSONObject, now: Long = System.currentTimeMillis()): Int {
        val live = if (runtime.optBoolean("running") && !runtime.isNull("startedAt")) max(0L, (now - runtime.optLong("startedAt")) / 1000) else 0
        return (runtime.optInt("elapsed") + live).coerceAtMost(8640000L).toInt()
    }
    fun duration(runtime: JSONObject) = (if (runtime.optString("phase") == "shortBreak") runtime.optInt("shortMinutes", 5) else runtime.optInt("focusMinutes", 25)) * 60
    private fun append(state: JSONObject, before: JSONObject, action: String, at: Long, taskId: String? = null) {
        val next = state.optLong("nextEvent", 0) + 1
        state.put("nextEvent", next)
        val events = state.optJSONArray("events") ?: JSONArray().also { state.put("events", it) }
        events.put(JSONObject().put("id", next).put("at", at).put("action", action).put("before", copy(before)).also { if (taskId != null) it.put("taskId", taskId) })
        state.put("revision", state.optInt("revision") + 1)
    }
    private fun advance(context: Context, state: JSONObject): Boolean {
        val runtime = state.optJSONObject("snapshot")?.optJSONObject("runtime") ?: return false
        val now = System.currentTimeMillis()
        var updated = false
        var rounds = 0
        while (runtime.optBoolean("running") && !runtime.optBoolean("countUp") && elapsed(runtime, now) >= duration(runtime) && rounds++ < 512) {
            val total = duration(runtime)
            val at = runtime.optLong("startedAt", now) + (total - runtime.optInt("elapsed")) * 1000L
            val before = copy(runtime).put("elapsed", total).put("startedAt", JSONObject.NULL)
            append(state, before, "complete", at)
            val focus = runtime.optString("phase") != "shortBreak"
            runtime.put("phase", if (focus && runtime.optBoolean("shortBreakEnabled", true)) "shortBreak" else "focus")
            val remaining = runtime.optInt("targetRemaining", -1)
            if (focus && remaining > 0) runtime.put("targetRemaining", remaining - 1)
            val reached = runtime.optInt("targetRemaining", -1) == 0
            val running = runtime.optBoolean("automaticCycle", true) && (if (focus) runtime.optBoolean("shortBreakEnabled", true) || !reached else !reached)
            runtime.put("running", running).put("elapsed", 0).put("loggedElapsed", 0).put("startedAt", if (running) at else JSONObject.NULL)
            if (focus && now - at < 60000) completionNotification(context)
            updated = true
        }
        return updated
    }
    @Synchronized fun read(context: Context): JSONObject {
        val state = load(context)
        if (advance(context, state)) { save(context, state); HomeWidgetRenderer.updateAll(context, state.optJSONObject("snapshot")) }
        schedule(context, state)
        return state
    }
    @Synchronized fun publish(context: Context, snapshot: String, expected: Int, ack: Long): JSONObject {
        val state = load(context)
        advance(context, state)
        if (state.optInt("revision") != expected) {
            save(context, state)
            return state.put("accepted", false)
        }
        val remaining = JSONArray()
        val events = state.optJSONArray("events") ?: JSONArray()
        for (i in 0 until events.length()) { val event = events.getJSONObject(i); if (event.optLong("id") > ack) remaining.put(event) }
        state.put("events", remaining).put("snapshot", JSONObject(snapshot))
        state.put("revision", state.optInt("revision") + 1)
        save(context, state)
        HomeWidgetRenderer.updateAll(context, state.optJSONObject("snapshot"))
        schedule(context, state)
        return state.put("accepted", true)
    }
    @Synchronized fun command(context: Context, action: String): JSONObject {
        val state = load(context)
        advance(context, state)
        val runtime = state.optJSONObject("snapshot")?.optJSONObject("runtime")
        if (runtime != null && action in listOf("toggle", "reset")) {
            val now = System.currentTimeMillis()
            val current = elapsed(runtime, now).let { if (runtime.optBoolean("countUp")) it else it.coerceAtMost(duration(runtime)) }
            val before = copy(runtime).put("elapsed", current).put("startedAt", JSONObject.NULL)
            append(state, before, action, now)
            if (action == "reset") {
                runtime.put("elapsed", 0).put("loggedElapsed", 0).put("running", false).put("startedAt", JSONObject.NULL)
            } else if (runtime.optBoolean("running")) {
                runtime.put("elapsed", current).put("running", false).put("startedAt", JSONObject.NULL)
                if (runtime.optString("phase") != "shortBreak") runtime.put("loggedElapsed", current)
            } else {
                runtime.put("running", true).put("startedAt", now)
            }
            save(context, state)
        }
        HomeWidgetRenderer.updateAll(context, state.optJSONObject("snapshot"))
        schedule(context, state)
        changed?.invoke()
        return state
    }
    @Synchronized fun toggleTask(context: Context, taskId: String, expectedDone: Boolean) {
        val state = load(context)
        advance(context, state)
        val snapshot = state.optJSONObject("snapshot") ?: return
        val tasks = snapshot.optJSONArray("tasks") ?: return
        for (i in 0 until tasks.length()) {
            val task = tasks.optJSONObject(i) ?: continue
            if (task.optString("id") != taskId) continue
            if (task.optBoolean("done") != expectedDone) return
            task.put("done", !task.optBoolean("done"))
            append(state, snapshot.optJSONObject("runtime") ?: JSONObject(), "task", System.currentTimeMillis(), taskId)
            save(context, state)
            HomeWidgetRenderer.updateAll(context, snapshot)
            changed?.invoke()
            return
        }
    }
    @Synchronized fun tick(context: Context) {
        val state = read(context)
        HomeWidgetRenderer.updateAll(context, state.optJSONObject("snapshot"))
        changed?.invoke()
    }
    private fun schedule(context: Context, state: JSONObject) {
        val runtime = state.optJSONObject("snapshot")?.optJSONObject("runtime") ?: return
        val manager = context.getSystemService(AlarmManager::class.java)
        val intent = PendingIntent.getBroadcast(context, 807, Intent(context, WidgetActionReceiver::class.java).setAction(ACTION_TICK), PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE)
        manager.cancel(intent)
        val midnight = java.time.LocalDate.now().plusDays(1).atStartOfDay(java.time.ZoneId.systemDefault()).toInstant().toEpochMilli()
        val deadline = if (runtime.optBoolean("running") && !runtime.optBoolean("countUp")) System.currentTimeMillis() + max(1, duration(runtime) - elapsed(runtime)) * 1000L else midnight
        val at = minOf(midnight, deadline)
        if (Build.VERSION.SDK_INT < 31 || manager.canScheduleExactAlarms()) manager.setExactAndAllowWhileIdle(AlarmManager.RTC_WAKEUP, at, intent)
        else manager.setAndAllowWhileIdle(AlarmManager.RTC_WAKEUP, at, intent)
    }
    private fun completionNotification(context: Context) {
        if (Build.VERSION.SDK_INT >= 33 && context.checkSelfPermission(android.Manifest.permission.POST_NOTIFICATIONS) != android.content.pm.PackageManager.PERMISSION_GRANTED) return
        val manager = context.getSystemService(NotificationManager::class.java)
        if (Build.VERSION.SDK_INT >= 26) manager.createNotificationChannel(NotificationChannel("widget_focus", UiText.t(context, "桌面计时完成"), NotificationManager.IMPORTANCE_DEFAULT))
        val builder = if (Build.VERSION.SDK_INT >= 26) Notification.Builder(context, "widget_focus") else Notification.Builder(context)
        manager.notify(809, builder.setSmallIcon(R.drawable.ic_stat_tomatotodo).setContentTitle(UiText.t(context, "完成一个番茄")).setContentText(UiText.t(context, "专注已记录，做得不错。"))
            .setContentIntent(HomeWidgetRenderer.openIntent(context, "timer")).setAutoCancel(true).build())
    }
}

class WidgetActionReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        when(intent.action) {
            WidgetStore.ACTION_TOGGLE -> WidgetStore.command(context, "toggle")
            WidgetStore.ACTION_TASK -> intent.getStringExtra("taskId")?.let {
                WidgetStore.toggleTask(context, it, intent.getBooleanExtra("expectedDone", false))
            }
            else -> WidgetStore.tick(context)
        }
    }
}
