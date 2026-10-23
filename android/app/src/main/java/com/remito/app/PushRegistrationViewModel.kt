package com.remito.app

import android.content.Context
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.google.firebase.FirebaseApp
import com.google.firebase.messaging.FirebaseMessaging
import com.remito.core.network.DevicePlatform
import com.remito.core.network.DeviceRegistrationRequest
import com.remito.core.network.RemitoApi
import dagger.hilt.android.lifecycle.HiltViewModel
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.launch
import java.io.IOException
import javax.inject.Inject

@HiltViewModel
class PushRegistrationViewModel
    @Inject
    constructor(
        @ApplicationContext private val context: Context,
        private val api: RemitoApi,
    ) : ViewModel() {
        private val _registrationError = MutableStateFlow<String?>(null)
        val registrationError: StateFlow<String?> = _registrationError

        fun registerCurrentToken() {
            if (FirebaseApp.getApps(context).none { it.name == FirebaseApp.DEFAULT_APP_NAME }) {
                _registrationError.value = context.getString(R.string.push_configuration_missing)
                return
            }
            FirebaseMessaging
                .getInstance()
                .token
                .addOnSuccessListener { token ->
                    viewModelScope.launch {
                        try {
                            val response =
                                api.registerDevice(DeviceRegistrationRequest(token, DevicePlatform.ANDROID))
                            if (!response.isSuccessful) {
                                throw IOException()
                            }
                            _registrationError.value = null
                        } catch (error: IOException) {
                            _registrationError.value = context.getString(R.string.push_registration_failed)
                            if (BuildConfig.DEBUG) {
                                android.util.Log.e("RemitoPush", "Push token registration failed.", error)
                            }
                        }
                    }
                }.addOnFailureListener { error ->
                    _registrationError.value = context.getString(R.string.push_token_failed)
                    if (BuildConfig.DEBUG) android.util.Log.e("RemitoPush", "Could not obtain a push token.", error)
                }
        }
    }
