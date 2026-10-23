package com.remito.feature.profile

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.ui.res.stringResource

@Composable
internal fun ProfileIdentitySection(state: ProfileUiState) {
    Text(state.displayName.ifBlank { state.email }, style = MaterialTheme.typography.titleMedium)
}

@Composable
internal fun AccountActionsSection(
    onLogout: () -> Unit,
    onDelete: () -> Unit,
    isSaving: Boolean,
) {
    TextButton(onClick = onLogout) { Text(stringResource(R.string.logout)) }
    TextButton(onClick = onDelete, enabled = !isSaving) {
        Text(stringResource(R.string.delete_account), color = MaterialTheme.colorScheme.error)
    }
}
