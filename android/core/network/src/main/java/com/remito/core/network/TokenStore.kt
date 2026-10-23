package com.remito.core.network

import android.content.Context
import androidx.security.crypto.EncryptedSharedPreferences
import androidx.security.crypto.MasterKey

interface TokenStore {
    fun accessToken(): String?

    fun refreshToken(): String?

    fun save(
        accessToken: String,
        refreshToken: String,
    )

    fun clear()
}

class EncryptedTokenStore(
    context: Context,
) : TokenStore {
    private val preferences =
        EncryptedSharedPreferences.create(
            context,
            PREFERENCES_FILE,
            MasterKey.Builder(context).setKeyScheme(MasterKey.KeyScheme.AES256_GCM).build(),
            EncryptedSharedPreferences.PrefKeyEncryptionScheme.AES256_SIV,
            EncryptedSharedPreferences.PrefValueEncryptionScheme.AES256_GCM,
        )

    override fun accessToken(): String? = preferences.getString(ACCESS_TOKEN, null)

    override fun refreshToken(): String? = preferences.getString(REFRESH_TOKEN, null)

    override fun save(
        accessToken: String,
        refreshToken: String,
    ) {
        check(
            preferences
                .edit()
                .putString(ACCESS_TOKEN, accessToken)
                .putString(REFRESH_TOKEN, refreshToken)
                .commit(),
        ) {
            "Unable to persist the encrypted session."
        }
    }

    override fun clear() {
        check(preferences.edit().clear().commit()) { "Unable to clear the encrypted session." }
    }

    private companion object {
        const val PREFERENCES_FILE = "remito_encrypted_session"
        const val ACCESS_TOKEN = "access_token"
        const val REFRESH_TOKEN = "refresh_token"
    }
}
