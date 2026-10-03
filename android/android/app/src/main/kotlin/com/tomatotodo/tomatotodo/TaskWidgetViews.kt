package com.tomatotodo.tomatotodo

import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import android.content.res.Configuration
import android.graphics.*
import android.net.Uri
import android.os.Build
import android.view.View
import android.widget.RemoteViews
import android.widget.RemoteViewsService
import org.json.JSONObject

/** Native collection rows remain scrollable in the launcher, independent of Flutter. */
object TaskWidgetViews {
    fun bind(context: Context, views: RemoteViews, data: JSONObject, small: Boolean) {
        val tasks = data.optJSONArray("tasks")
        val count = tasks?.length() ?: 0
        if (Build.VERSION.SDK_INT >= 31) {
            val items = RemoteViews.RemoteCollectionItems.Builder().setHasStableIds(true).setViewTypeCount(1)
            for (i in 0 until count) {
                val task = tasks!!.getJSONObject(i)
                items.addItem(task.optString("id").hashCode().toLong(), row(context, data, task, small))
            }
            views.setRemoteAdapter(R.id.task_rows, items.build())
        } else {
            val adapter = Intent(context, TaskWidgetService::class.java)
                .putExtra("small", small).setData(Uri.parse("tomatotodo://task-list/$small"))
            views.setRemoteAdapter(R.id.task_rows, adapter)
        }
        val template = PendingIntent.getBroadcast(context, 811,
            Intent(context, WidgetActionReceiver::class.java).setAction(WidgetStore.ACTION_TASK),
            PendingIntent.FLAG_UPDATE_CURRENT or (if (Build.VERSION.SDK_INT >= 31) PendingIntent.FLAG_MUTABLE else 0))
        views.setPendingIntentTemplate(R.id.task_rows, template)
        views.setEmptyView(R.id.task_rows, R.id.tasks_empty)
    }

    fun row(context: Context, data: JSONObject, task: JSONObject, small: Boolean): RemoteViews {
        val dark = when(data.optString("theme")) {
            "dark" -> true; "light" -> false
            else -> context.resources.configuration.uiMode and Configuration.UI_MODE_NIGHT_MASK == Configuration.UI_MODE_NIGHT_YES
        }
        val colors = data.optJSONObject(if (dark) "dark" else "light") ?: JSONObject()
        fun color(key: String, fallback: Long) = colors.optLong(key, fallback).toInt()
        val primary = color("primary", if(dark) 0xFFA8D29E else 0xFF386A31)
        val onPrimary = color("surface", if(dark) 0xFF1C211B else 0xFFF0F2E9)
        val done = task.optBoolean("done")
        val title = task.optString("title")
        val estimate = task.optInt("estimate")
        val tomatoes = if (estimate > 0) "${task.optInt("tomatoes")}/$estimate" else ""
        val detail = listOf(task.optString("subtitle"), if(small && tomatoes.isNotEmpty()) UiText.f(context, "{0} 个番茄", tomatoes) else "")
            .filter { it.isNotBlank() }.joinToString(" · ")
        return RemoteViews(context.packageName, R.layout.widget_task_row).apply {
            setTextViewText(R.id.row_title, title)
            setTextColor(R.id.row_title, color("onSurface", if(dark) 0xFFE1E4D9 else 0xFF191D17))
            setInt(R.id.row_title, "setPaintFlags", Paint.ANTI_ALIAS_FLAG or (if(done) Paint.STRIKE_THRU_TEXT_FLAG else 0))
            setTextViewText(R.id.row_detail, detail)
            setTextColor(R.id.row_detail, color("secondary", if(dark) 0xFFBEC9B9 else 0xFF515C4B))
            setViewVisibility(R.id.row_detail, if(detail.isBlank()) View.GONE else View.VISIBLE)
            setTextViewText(R.id.row_trailing, tomatoes)
            setTextColor(R.id.row_trailing, primary)
            setViewVisibility(R.id.row_trailing, if(small || tomatoes.isEmpty()) View.GONE else View.VISIBLE)
            setImageViewBitmap(R.id.task_complete, checkbox(primary, onPrimary, done))
            setContentDescription(R.id.task_complete, "${if(done) "取消完成" else "完成"} $title")
            setOnClickFillInIntent(R.id.task_complete, Intent()
                .putExtra("taskId", task.optString("id")).putExtra("expectedDone", done))
        }
    }

    private fun checkbox(primary: Int, onPrimary: Int, done: Boolean): Bitmap {
        val bitmap = Bitmap.createBitmap(48, 48, Bitmap.Config.ARGB_8888)
        val canvas = Canvas(bitmap)
        val paint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
            color = primary; style = if(done) Paint.Style.FILL else Paint.Style.STROKE; strokeWidth = 4f
        }
        canvas.drawRoundRect(RectF(6f,6f,42f,42f),5f,5f,paint)
        if(done) {
            paint.color = onPrimary; paint.style = Paint.Style.STROKE
            paint.strokeCap = Paint.Cap.ROUND; paint.strokeJoin = Paint.Join.ROUND
            canvas.drawPath(Path().apply { moveTo(14f,24f); lineTo(21f,31f); lineTo(35f,17f) },paint)
        }
        return bitmap
    }
}

class TaskWidgetService : RemoteViewsService() {
    override fun onGetViewFactory(intent: Intent): RemoteViewsFactory = object : RemoteViewsFactory {
        private var data = JSONObject()
        override fun onCreate() { onDataSetChanged() }
        override fun onDataSetChanged() { data = WidgetStore.read(this@TaskWidgetService).optJSONObject("snapshot") ?: JSONObject() }
        override fun onDestroy() {}
        override fun getCount() = data.optJSONArray("tasks")?.length() ?: 0
        override fun getViewAt(position: Int): RemoteViews? = data.optJSONArray("tasks")?.optJSONObject(position)?.let {
            TaskWidgetViews.row(this@TaskWidgetService, data, it, intent.getBooleanExtra("small", false))
        }
        override fun getLoadingView(): RemoteViews? = null
        override fun getViewTypeCount() = 1
        override fun getItemId(position: Int) = data.optJSONArray("tasks")?.optJSONObject(position)?.optString("id")?.hashCode()?.toLong() ?: position.toLong()
        override fun hasStableIds() = true
    }
}
