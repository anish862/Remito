pluginManagement {
    repositories {
        google()
        mavenCentral()
        gradlePluginPortal()
    }
}

dependencyResolutionManagement {
    repositoriesMode.set(RepositoriesMode.FAIL_ON_PROJECT_REPOS)
    repositories {
        google()
        mavenCentral()
    }
}

rootProject.name = "Remito"
include(":app")
include(":core:ui", ":core:network", ":core:database", ":core:common")
include(":feature:dashboard", ":feature:convert", ":feature:history")
include(":feature:compare", ":feature:alerts", ":feature:profile")