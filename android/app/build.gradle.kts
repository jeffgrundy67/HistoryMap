plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
}

android {
    namespace = "com.jeffgrundy.historicplaces"
    compileSdk = 35

    defaultConfig {
        applicationId = "com.jeffgrundy.historicplaces"
        minSdk = 24
        targetSdk = 35
        versionCode = 1
        versionName = "1.0"
    }

    buildTypes {
        release {
            isMinifyEnabled = false
            proguardFiles(
                getDefaultProguardFile("proguard-android-optimize.txt"),
                "proguard-rules.pro"
            )
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }
    kotlinOptions {
        jvmTarget = "17"
    }

    buildFeatures {
        viewBinding = true
    }
}

dependencies {
    // Android UI basics
    implementation("androidx.core:core-ktx:1.15.0")
    implementation("androidx.appcompat:appcompat:1.7.0")
    implementation("com.google.android.material:material:1.12.0")
    implementation("androidx.constraintlayout:constraintlayout:2.2.0")
    implementation("androidx.activity:activity-ktx:1.9.3")

    // OpenStreetMap / offline-friendly map library (no Google API key)
    implementation("org.osmdroid:osmdroid-android:6.1.20")
    // Marker clustering for busy downtown areas
    implementation("com.github.MKergall:osmbonuspack:6.9.0")

    // Coroutines so network calls don't freeze the UI
    implementation("org.jetbrains.kotlinx:kotlinx-coroutines-android:1.9.0")
}
