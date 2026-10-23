package com.remito.feature.profile

import android.content.Context
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.remito.core.common.UserSettings
import com.remito.core.network.ConsentRequest
import com.remito.core.network.ConsentType
import com.remito.core.network.NotificationPreferencesUpdate
import com.remito.core.network.ProblemDetailsMapper
import com.remito.core.network.RemitoApi
import com.remito.core.network.TokenStore
import dagger.hilt.android.lifecycle.HiltViewModel
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.launch
import retrofit2.Response
import java.io.IOException
import javax.inject.Inject

private const val MINUTES_PER_HOUR = 60
private const val FIRST_HOUR = 0
private const val LAST_HOUR = 23
private const val LAST_MINUTE = 59

data class ProfileUiState(
    val displayName: String = "",
    val email: String = "",
    val defaultCorridor: String = "OMR/INR",
    val language: String = "English",
    val rateAlertsEnabled: Boolean = true,
    val dailySummaryEnabled: Boolean = false,
    val newsEnabled: Boolean = true,
    val offersEnabled: Boolean = false,
    val marketingConsent: Boolean = false,
    val quietStart: String = "",
    val quietEnd: String = "",
    val isLoading: Boolean = true,
    val isSaving: Boolean = false,
    val accountDeleted: Boolean = false,
    val error: String? = null,
)

@HiltViewModel
class ProfileViewModel
    @Inject
    constructor(
        private val api: RemitoApi,
        private val tokenStore: TokenStore,
        private val userSettings: UserSettings,
        @ApplicationContext context: Context,
    ) : ViewModel() {
        private val problemDetailsMapper = ProblemDetailsMapper()
        private val preferences = context.getSharedPreferences("user-settings", Context.MODE_PRIVATE)
        private val _uiState =
            MutableStateFlow(
                ProfileUiState(
                    defaultCorridor = userSettings.defaultCorridor(),
                    language = userSettings.language(),
                    newsEnabled = preferences.getBoolean("news_enabled", true),
                    offersEnabled = preferences.getBoolean("offers_enabled", false),
                ),
            )
        val uiState: StateFlow<ProfileUiState> = _uiState

        init {
            refresh()
        }

        fun refresh() {
            viewModelScope.launch {
                try {
                    val profile = loadProfile(api, problemDetailsMapper)
                    _uiState.value =
                        _uiState.value.copy(
                            displayName = profile.profile.displayName.orEmpty(),
                            email = profile.profile.email ?: profile.profile.phoneNumber.orEmpty(),
                            rateAlertsEnabled = profile.preferences.rateAlertsEnabled,
                            dailySummaryEnabled = profile.preferences.dailySummaryEnabled,
                            marketingConsent = profile.consents.hasMarketingConsent(),
                            quietStart = formatMinute(profile.preferences.quietHoursStartMinute),
                            quietEnd = formatMinute(profile.preferences.quietHoursEndMinute),
                            isLoading = false,
                            error = null,
                        )
                } catch (error: CancellationException) {
                    throw error
                } catch (error: IOException) {
                    _uiState.value =
                        _uiState.value.copy(
                            isLoading = false,
                            error = error.message ?: "Could not load profile.",
                        )
                }
            }
        }

        fun setDefaultCorridor(corridor: String) {
            userSettings.setDefaultCorridor(corridor)
            _uiState.value = _uiState.value.copy(defaultCorridor = corridor)
        }

        fun setLanguage(language: String) {
            userSettings.setLanguage(language)
            _uiState.value = _uiState.value.copy(language = language)
        }

        fun setCategoryEnabled(
            category: String,
            enabled: Boolean,
        ) {
            preferences.edit().putBoolean("${category}_enabled", enabled).apply()
            _uiState.value =
                when (category) {
                    "news" -> _uiState.value.copy(newsEnabled = enabled)
                    else -> _uiState.value.copy(offersEnabled = enabled)
                }
        }

        fun setRateAlertsEnabled(enabled: Boolean) {
            saveNotificationPreferences(
                scope = viewModelScope,
                api = api,
                mapper = problemDetailsMapper,
                settings =
                    NotificationPreferenceSave(
                        state = _uiState.value.copy(rateAlertsEnabled = enabled),
                        alertsEnabled = enabled,
                    ),
                onUpdate = { next -> _uiState.value = next },
            )
        }

        fun setDailySummaryEnabled(enabled: Boolean) {
            saveNotificationPreferences(
                scope = viewModelScope,
                api = api,
                mapper = problemDetailsMapper,
                settings =
                    NotificationPreferenceSave(
                        state = _uiState.value.copy(dailySummaryEnabled = enabled),
                        alertsEnabled = _uiState.value.rateAlertsEnabled,
                        dailySummaryEnabled = enabled,
                    ),
                onUpdate = { next -> _uiState.value = next },
            )
        }

        fun setQuietHours(
            start: String,
            end: String,
        ) {
            val current = _uiState.value
            _uiState.value = current.copy(quietStart = start, quietEnd = end)
            val startMinute = parseMinute(start)
            val endMinute = parseMinute(end)
            if (invalidQuietHours(start, end, startMinute, endMinute)) {
                _uiState.value = _uiState.value.copy(error = "Use HH:mm times from 00:00 to 23:59.")
                return
            }
            val bothBlank = start.isBlank() && end.isBlank()
            val bothValid = startMinute != null && endMinute != null
            if (bothBlank || bothValid) {
                saveNotificationPreferences(
                    scope = viewModelScope,
                    api = api,
                    mapper = problemDetailsMapper,
                    settings =
                        NotificationPreferenceSave(
                            state = _uiState.value,
                            alertsEnabled = _uiState.value.rateAlertsEnabled,
                            quietStart = startMinute,
                            quietEnd = endMinute,
                        ),
                    onUpdate = { next -> _uiState.value = next },
                )
            } else if (start.isBlank() != end.isBlank()) {
                _uiState.value = _uiState.value.copy(error = "Enter both quiet-hours times, or clear both.")
            }
        }

        fun setMarketingConsent(enabled: Boolean) {
            viewModelScope.launch {
                _uiState.value = _uiState.value.copy(isSaving = true, error = null)
                try {
                    api
                        .updateMarketingConsent(ConsentRequest(enabled, CONSENT_VERSION))
                        .requireSuccess(problemDetailsMapper)
                    _uiState.value = _uiState.value.copy(marketingConsent = enabled, isSaving = false)
                } catch (error: CancellationException) {
                    throw error
                } catch (error: IOException) {
                    _uiState.value =
                        _uiState.value.copy(
                            isSaving = false,
                            error = error.message ?: "Could not update consent.",
                        )
                }
            }
        }

        fun deleteAccount() {
            viewModelScope.launch {
                _uiState.value = _uiState.value.copy(isSaving = true, error = null)
                try {
                    api.deleteMe().requireSuccess(problemDetailsMapper)
                    tokenStore.clear()
                    _uiState.value = _uiState.value.copy(isSaving = false, accountDeleted = true)
                } catch (error: CancellationException) {
                    throw error
                } catch (error: IOException) {
                    _uiState.value =
                        _uiState.value.copy(
                            isSaving = false,
                            error = error.message ?: "Could not delete account.",
                        )
                }
            }
        }

        companion object {
            private const val CONSENT_VERSION = "1"
        }
    }

private suspend fun loadProfile(
    api: RemitoApi,
    mapper: ProblemDetailsMapper,
): LoadedProfile =
    LoadedProfile(
        profile = api.getMe().requireBody(mapper, "The profile response was empty."),
        consents = api.getConsents().requireBody(mapper, "The consent response was empty."),
        preferences =
            api.notificationPreferences().requireBody(
                mapper,
                "The notification settings response was empty.",
            ),
    )

private fun saveNotificationPreferences(
    scope: CoroutineScope,
    api: RemitoApi,
    mapper: ProblemDetailsMapper,
    settings: NotificationPreferenceSave,
    onUpdate: (ProfileUiState) -> Unit,
) {
    if ((settings.quietStart == null) != (settings.quietEnd == null)) {
        onUpdate(settings.state.copy(error = "Enter both quiet-hours times in HH:mm format."))
        return
    }
    scope.launch {
        onUpdate(settings.state.copy(isSaving = true, error = null))
        try {
            val request =
                NotificationPreferencesUpdate(
                    rateAlertsEnabled = settings.alertsEnabled,
                    dailySummaryEnabled = settings.dailySummaryEnabled,
                    quietHoursStartMinute = settings.quietStart,
                    quietHoursEndMinute = settings.quietEnd,
                )
            val saved =
                api
                    .updateNotificationPreferences(request)
                    .requireBody(mapper, "The notification settings response was empty.")
            onUpdate(
                settings.state.copy(
                    rateAlertsEnabled = saved.rateAlertsEnabled,
                    dailySummaryEnabled = saved.dailySummaryEnabled,
                    quietStart = formatMinute(saved.quietHoursStartMinute),
                    quietEnd = formatMinute(saved.quietHoursEndMinute),
                    isSaving = false,
                ),
            )
        } catch (error: CancellationException) {
            throw error
        } catch (error: IOException) {
            onUpdate(
                settings.state.copy(
                    isSaving = false,
                    error = error.message ?: "Could not save settings.",
                ),
            )
        }
    }
}

private data class NotificationPreferenceSave(
    val state: ProfileUiState,
    val alertsEnabled: Boolean,
    val quietStart: Int? = parseMinute(state.quietStart),
    val quietEnd: Int? = parseMinute(state.quietEnd),
    val dailySummaryEnabled: Boolean = state.dailySummaryEnabled,
)

private data class LoadedProfile(
    val profile: com.remito.core.network.UserProfileDto,
    val consents: List<com.remito.core.network.ConsentStateDto>,
    val preferences: com.remito.core.network.NotificationPreferencesDto,
)

private fun List<com.remito.core.network.ConsentStateDto>.hasMarketingConsent(): Boolean =
    any { it.type == ConsentType.MARKETING && it.isGranted }

private fun invalidQuietHours(
    start: String,
    end: String,
    startMinute: Int?,
    endMinute: Int?,
): Boolean = (start.isNotBlank() && startMinute == null) || (end.isNotBlank() && endMinute == null)

private fun formatMinute(minute: Int?): String =
    minute
        ?.let {
            "%02d:%02d".format(it / MINUTES_PER_HOUR, it % MINUTES_PER_HOUR)
        }.orEmpty()

private fun parseMinute(value: String): Int? =
    value
        .takeIf(String::isNotBlank)
        ?.split(':')
        ?.takeIf { it.size == 2 }
        ?.mapNotNull(String::toIntOrNull)
        ?.takeIf { it.size == 2 }
        ?.let { (hour, minute) ->
            if (hour in FIRST_HOUR..LAST_HOUR && minute in FIRST_HOUR..LAST_MINUTE) {
                hour * MINUTES_PER_HOUR + minute
            } else {
                null
            }
        }

private fun <T> Response<T>.requireBody(
    mapper: ProblemDetailsMapper,
    emptyMessage: String,
): T {
    if (!isSuccessful) throw mapper.map(this).toIOException()
    return body() ?: throw IOException(emptyMessage)
}

private fun Response<*>.requireSuccess(mapper: ProblemDetailsMapper) {
    if (!isSuccessful) throw mapper.map(this).toIOException()
}

private fun com.remito.core.network.ApiFailure.toIOException() =
    IOException(
        detail
            ?.takeIf(String::isNotBlank)
            ?: title,
    )
