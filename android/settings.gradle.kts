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
        // Needed for OSMBonusPack (marker clustering helper)
        maven { url = uri("https://jitpack.io") }
    }
}

rootProject.name = "HistoricPlaces"
include(":app")
