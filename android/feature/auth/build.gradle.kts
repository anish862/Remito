plugins {
    alias(libs.plugins.android.library)
    alias(libs.plugins.kotlin.android)
    alias(libs.plugins.kotlin.compose)
    alias(libs.plugins.kotlin.kapt)
    alias(libs.plugins.hilt)
}

android {
    namespace = "com.remito.feature.auth"
    compileSdk = 36
    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }
    defaultConfig { minSdk = 26 }
    buildFeatures { compose = true }
}

dependencies {
    implementation(project(":core:ui"))
    implementation(project(":core:network"))
    implementation(platform(libs.compose.bom))
    implementation(libs.compose.material3)
    implementation(libs.hilt.android)
    implementation(libs.hilt.navigation.compose)
    implementation(libs.androidx.lifecycle.viewmodel.compose)
    implementation(libs.retrofit)
    kapt(libs.hilt.compiler)
}

kapt { correctErrorTypes = true }
