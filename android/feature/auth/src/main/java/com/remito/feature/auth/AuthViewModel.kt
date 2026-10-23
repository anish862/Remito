package com.remito.feature.auth

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.remito.core.network.OtpChannel
import com.remito.core.network.TokenStore
import com.remito.feature.auth.R
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.launch
import java.io.IOException
import javax.inject.Inject

data class AuthUiState(
    val destination: String = "",
    val code: String = "",
    val channel: OtpChannel = OtpChannel.EMAIL,
    val codeSent: Boolean = false,
    val isLoading: Boolean = false,
    val isAuthenticated: Boolean = false,
    val error: String? = null,
    val errorResource: Int? = null,
)

@HiltViewModel
class AuthViewModel
    @Inject
    constructor(
        private val repository: AuthRepository,
        private val tokenStore: TokenStore,
        savedStateHandle: SavedStateHandle,
    ) : ViewModel() {
        private companion object {
            const val MAX_OTP_CODE_LENGTH = 8
        }

        private val _uiState =
            MutableStateFlow(
                AuthUiState(
                    destination = savedStateHandle["destination"] ?: "",
                    isAuthenticated = !tokenStore.accessToken().isNullOrBlank(),
                ),
            )
        val uiState: StateFlow<AuthUiState> = _uiState

        fun setDestination(value: String) {
            _uiState.value = _uiState.value.copy(destination = value, error = null, errorResource = null)
        }

        fun setCode(value: String) {
            _uiState.value =
                _uiState.value.copy(
                    code = value.filter(Char::isDigit).take(MAX_OTP_CODE_LENGTH),
                    error = null,
                    errorResource = null,
                )
        }

        fun setChannel(channel: OtpChannel) {
            _uiState.value =
                _uiState.value.copy(
                    channel = channel,
                    codeSent = false,
                    error = null,
                    errorResource = null,
                )
        }

        fun requestCode() {
            val current = _uiState.value
            if (current.destination.isBlank()) {
                _uiState.value = current.copy(error = null, errorResource = R.string.enter_destination)
                return
            }
            viewModelScope.launch {
                _uiState.value = _uiState.value.copy(isLoading = true, error = null, errorResource = null)
                try {
                    repository.requestCode(current.channel, current.destination.trim())
                    _uiState.value = _uiState.value.copy(codeSent = true, isLoading = false)
                } catch (error: CancellationException) {
                    throw error
                } catch (error: IOException) {
                    _uiState.value =
                        _uiState.value.copy(
                            isLoading = false,
                            error = error.message,
                            errorResource = if (error.message == null) R.string.authentication_error else null,
                        )
                } catch (error: IllegalStateException) {
                    _uiState.value =
                        _uiState.value.copy(
                            isLoading = false,
                            error = error.message,
                            errorResource = if (error.message == null) R.string.authentication_error else null,
                        )
                }
            }
        }

        fun verifyCode() {
            val current = _uiState.value
            if (current.code.isBlank()) {
                _uiState.value = current.copy(error = null, errorResource = R.string.enter_code)
                return
            }
            viewModelScope.launch {
                _uiState.value = _uiState.value.copy(isLoading = true, error = null, errorResource = null)
                try {
                    repository.verifyCode(current.channel, current.destination.trim(), current.code)
                    _uiState.value = _uiState.value.copy(isLoading = false, isAuthenticated = true)
                } catch (error: CancellationException) {
                    throw error
                } catch (error: IOException) {
                    _uiState.value =
                        _uiState.value.copy(
                            isLoading = false,
                            error = error.message,
                            errorResource = if (error.message == null) R.string.authentication_error else null,
                        )
                } catch (error: IllegalStateException) {
                    _uiState.value =
                        _uiState.value.copy(
                            isLoading = false,
                            error = error.message,
                            errorResource = if (error.message == null) R.string.authentication_error else null,
                        )
                }
            }
        }

        fun logout() {
            viewModelScope.launch {
                _uiState.value = _uiState.value.copy(isLoading = true, error = null, errorResource = null)
                try {
                    repository.logout()
                    _uiState.value = AuthUiState()
                } catch (error: CancellationException) {
                    throw error
                } catch (error: IOException) {
                    tokenStore.clear()
                    _uiState.value = AuthUiState(error = error.message)
                } catch (error: IllegalStateException) {
                    tokenStore.clear()
                    _uiState.value = AuthUiState(error = error.message)
                }
            }
        }
    }
