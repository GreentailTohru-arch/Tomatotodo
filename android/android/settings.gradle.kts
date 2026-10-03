pluginManagement {
    // Flutter 3.47 on this Windows host can write doubled separators to its
    // generated plugin paths. Gradle's File.exists then rejects valid plugins.
    val pluginMetadata = file("../.flutter-plugins-dependencies")
    if (pluginMetadata.isFile) {
        val original = pluginMetadata.readText()
        val normalized = original.replace("\\".repeat(4), "/")
        if (normalized != original) pluginMetadata.writeText(normalized)
    }
    val flutterSdkPath =
        run {
            val properties = java.util.Properties()
            file("local.properties").inputStream().use { properties.load(it) }
            val flutterSdkPath = properties.getProperty("flutter.sdk")
            require(flutterSdkPath != null) { "flutter.sdk not set in local.properties" }
            flutterSdkPath
        }

    includeBuild("$flutterSdkPath/packages/flutter_tools/gradle")

    repositories {
        google()
        mavenCentral()
        gradlePluginPortal()
    }
}

plugins {
    id("dev.flutter.flutter-plugin-loader") version "1.0.0"
    id("com.android.application") version "9.1.0" apply false
    id("org.jetbrains.kotlin.android") version "2.4.0" apply false
}

include(":app")
