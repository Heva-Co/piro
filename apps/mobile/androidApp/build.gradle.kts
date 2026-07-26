plugins {
    // AGP 9+ ships built-in Kotlin support, so no separate kotlin-android plugin is applied here.
    alias(libs.plugins.androidApplication)
    alias(libs.plugins.composeCompiler)
}

// The google-services plugin needs a real google-services.json (a Firebase project). It's deployment
// config, not committed, so we only apply the plugin when the file is present — the app still builds
// (and runs login + device registration) without it; only live FCM delivery needs it.
val hasGoogleServices = file("google-services.json").exists()
if (hasGoogleServices) {
    apply(plugin = libs.plugins.googleServices.get().pluginId)
}

android {
    namespace = "co.heva.piro.android"
    compileSdk = libs.versions.compileSdk.get().toInt()

    defaultConfig {
        applicationId = "co.heva.piro"
        minSdk = libs.versions.minSdk.get().toInt()
        targetSdk = libs.versions.targetSdk.get().toInt()
        versionCode = 1
        versionName = "0.1.0"
    }

    buildTypes {
        getByName("debug") {
            // localhost works for BOTH a physical device and the emulator when the API port is bridged
            // over USB with `adb reverse tcp:5117 tcp:5117`. (The emulator could also use 10.0.2.2, but
            // reverse+localhost is the one URL that works everywhere.)
            buildConfigField("String", "PIRO_API_BASE_URL", "\"http://localhost:5117\"")
        }
        getByName("release") {
            isMinifyEnabled = false
            buildConfigField("String", "PIRO_API_BASE_URL", "\"https://your-piro-host\"")
        }
    }

    buildFeatures {
        compose = true
        buildConfig = true
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    kotlin {
        jvmToolchain(17)
    }
}

dependencies {
    implementation(project(":shared"))

    implementation(libs.androidx.core.ktx)
    implementation(libs.androidx.activity.compose)
    implementation(libs.androidx.lifecycle.viewmodel.compose)
    implementation(libs.androidx.lifecycle.runtime.compose)
    implementation(libs.androidx.browser)

    implementation(platform(libs.compose.bom))
    implementation(libs.compose.runtime)
    implementation(libs.compose.foundation)
    implementation(libs.compose.material3)
    implementation(libs.compose.material.icons.extended)
    implementation(libs.compose.ui)
    implementation(libs.compose.ui.tooling.preview)

    implementation(libs.kotlinx.coroutines.android)
    implementation(libs.kotlinx.coroutines.play.services)

    implementation(platform(libs.firebase.bom))
    implementation(libs.firebase.messaging)
}
