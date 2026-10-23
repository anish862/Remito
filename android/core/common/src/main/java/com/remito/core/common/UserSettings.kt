package com.remito.core.common

import android.content.Context

interface UserSettings {
    fun defaultCorridor(): String

    fun setDefaultCorridor(corridor: String)

    fun language(): String

    fun setLanguage(language: String)
}

class UserSettingsStore(
    context: Context,
) : UserSettings {
    private val preferences = context.getSharedPreferences(PREFERENCES_NAME, Context.MODE_PRIVATE)

    override fun defaultCorridor(): String =
        preferences.getString(
            DEFAULT_CORRIDOR_KEY,
            DEFAULT_CORRIDOR,
        ) ?: DEFAULT_CORRIDOR

    override fun setDefaultCorridor(corridor: String) {
        require(corridor in SUPPORTED_CORRIDORS)
        preferences.edit().putString(DEFAULT_CORRIDOR_KEY, corridor).apply()
    }

    override fun language(): String =
        preferences.getString(
            LANGUAGE_KEY,
            DEFAULT_LANGUAGE,
        ) ?: DEFAULT_LANGUAGE

    override fun setLanguage(language: String) {
        require(language == DEFAULT_LANGUAGE)
        preferences.edit().putString(LANGUAGE_KEY, language).apply()
    }

    companion object {
        const val DEFAULT_CORRIDOR = "OMR/INR"
        val SUPPORTED_CORRIDORS = listOf("OMR/INR", "OMR/AED", "OMR/USD", "OMR/EUR", "OMR/GBP")

        private const val PREFERENCES_NAME = "user-settings"
        private const val DEFAULT_CORRIDOR_KEY = "default_corridor"
        private const val LANGUAGE_KEY = "language"
        private const val DEFAULT_LANGUAGE = "English"
    }
}
