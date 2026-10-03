package com.tomatotodo.tomatotodo

import android.content.Context
import org.json.JSONObject
import java.util.Locale

/** Offline source-owned interface copy; never translates user content. */
object UiText {
    private var catalog: JSONObject? = null
    fun language(context: Context): String {
        val saved = context.getSharedPreferences("FlutterSharedPreferences", Context.MODE_PRIVATE)
            .getString("flutter.ui.language", "system") ?: "system"
        val tag = if(saved == "system") Locale.getDefault().toLanguageTag() else saved
        val parts = tag.lowercase().replace('_','-').split('-')
        val base = when(parts[0]) { "iw" -> "he"; "in" -> "id"; "tl" -> "fil"; "no" -> "nb"; else -> parts[0] }
        return when(base) {
            "zh" -> if(parts.any { it in listOf("hant","tw","hk","mo") }) "zh-TW" else "zh-CN"
            "en" -> if("gb" in parts.drop(1)) "en-GB" else "en-US"
            "pt" -> if("pt" in parts.drop(1)) "pt-PT" else "pt-BR"
            "es" -> if(parts.size == 1 || "es" in parts.drop(1)) "es-ES" else "es-419"
            "sr" -> if("latn" in parts) "sr-Latn" else "sr-Cyrl"
            else -> base
        }
    }
    @Synchronized fun t(context: Context, key: String): String {
        val code = language(context)
        if(code == "zh-CN") return key
        val data = catalog ?: runCatching {
            context.assets.open("localization/catalog.json").bufferedReader().use { JSONObject(it.readText()) }
        }.getOrDefault(JSONObject()).also { catalog = it }
        return data.optJSONObject(code)?.optString(key)?.takeIf { it.isNotEmpty() }
            ?: data.optJSONObject("en-US")?.optString(key)?.takeIf { it.isNotEmpty() } ?: key
    }
    fun f(context: Context, key: String, vararg arguments: Any?): String =
        Regex("\\{(\\d+)\\}").replace(t(context,key)) { match ->
            arguments.getOrNull(match.groupValues[1].toInt())?.toString() ?: match.value
        }
}
