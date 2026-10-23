package com.remito.feature.auth

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.selected
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.remito.core.network.OtpChannel

@Composable
fun AuthScreen(viewModel: AuthViewModel = hiltViewModel()) {
    val state by viewModel.uiState.collectAsStateWithLifecycle()
    AuthForm(
        state = state,
        actions =
            AuthActions(
                onDestinationChanged = viewModel::setDestination,
                onCodeChanged = viewModel::setCode,
                onChannelSelected = viewModel::setChannel,
                onRequestCode = viewModel::requestCode,
                onVerifyCode = viewModel::verifyCode,
            ),
    )
}

data class AuthActions(
    val onDestinationChanged: (String) -> Unit,
    val onCodeChanged: (String) -> Unit,
    val onChannelSelected: (OtpChannel) -> Unit,
    val onRequestCode: () -> Unit,
    val onVerifyCode: () -> Unit,
)

@Composable
@Suppress("LongMethod")
fun AuthForm(
    state: AuthUiState,
    actions: AuthActions,
) {
    Column(
        modifier = Modifier.fillMaxSize().padding(24.dp),
        verticalArrangement = Arrangement.spacedBy(16.dp),
    ) {
        Text(stringResource(R.string.welcome_title), style = MaterialTheme.typography.headlineMedium)
        Text(stringResource(R.string.welcome_description), style = MaterialTheme.typography.bodyLarge)
        OutlinedTextField(
            value = state.destination,
            onValueChange = actions.onDestinationChanged,
            modifier = Modifier.fillMaxWidth(),
            label = {
                Text(
                    stringResource(
                        if (state.channel == OtpChannel.EMAIL) R.string.email_address else R.string.phone_number,
                    ),
                )
            },
            keyboardOptions =
                KeyboardOptions(
                    keyboardType =
                        if (state.channel == OtpChannel.EMAIL) KeyboardType.Email else KeyboardType.Phone,
                ),
            singleLine = true,
        )
        Button(
            onClick = { actions.onChannelSelected(OtpChannel.EMAIL) },
            modifier = Modifier.heightIn(min = 48.dp).semantics { selected = state.channel == OtpChannel.EMAIL },
        ) {
            Text(stringResource(R.string.use_email))
        }
        Button(
            onClick = { actions.onChannelSelected(OtpChannel.PHONE) },
            modifier = Modifier.heightIn(min = 48.dp).semantics { selected = state.channel == OtpChannel.PHONE },
        ) {
            Text(stringResource(R.string.use_phone))
        }
        if (!state.codeSent) {
            Button(
                onClick = actions.onRequestCode,
                enabled = !state.isLoading,
                modifier = Modifier.fillMaxWidth().heightIn(min = 48.dp),
            ) {
                Text(stringResource(R.string.send_code))
            }
        } else {
            Text(stringResource(R.string.code_sent))
            OutlinedTextField(
                value = state.code,
                onValueChange = actions.onCodeChanged,
                modifier = Modifier.fillMaxWidth(),
                label = { Text(stringResource(R.string.verification_code)) },
                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.NumberPassword),
                singleLine = true,
            )
            Button(
                onClick = actions.onVerifyCode,
                enabled = !state.isLoading,
                modifier = Modifier.fillMaxWidth().heightIn(min = 48.dp),
            ) {
                Text(stringResource(R.string.verify_code))
            }
        }
        if (state.isLoading) Text(stringResource(R.string.auth_loading))
        state.error?.let { Text(it, color = MaterialTheme.colorScheme.error) }
        state.errorResource?.let { Text(stringResource(it), color = MaterialTheme.colorScheme.error) }
    }
}
