package com.remito.core.network

import android.content.Context
import retrofit2.Response
import java.util.UUID

class AnalyticsTracker(
    context: Context,
    private val api: RemitoApi,
) {
    private val preferences = context.getSharedPreferences(PREFERENCES_NAME, Context.MODE_PRIVATE)
    private val installationId: String by lazy(LazyThreadSafetyMode.SYNCHRONIZED) {
        preferences.getString(INSTALLATION_ID_KEY, null)
            ?: UUID.randomUUID().toString().also { generatedId ->
                preferences.edit().putString(INSTALLATION_ID_KEY, generatedId).apply()
            }
    }

    suspend fun track(event: AnalyticsEventName): Response<Unit> =
        api.ingestAnalyticsEvent(
            AnalyticsEventRequest(
                installationId = installationId,
                eventName = event.wireName,
            ),
        )

    private companion object {
        const val PREFERENCES_NAME = "analytics-installation"
        const val INSTALLATION_ID_KEY = "installation-id"
    }
}
