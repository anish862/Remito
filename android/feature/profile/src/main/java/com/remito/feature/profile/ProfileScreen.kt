package com.remito.feature.profile

import android.Manifest
import android.net.Uri
import android.os.Build
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.browser.customtabs.CustomTabsIntent
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.remito.core.ui.ErrorState

@Composable
fun ProfileScreen(
    config: ProfileScreenConfig = ProfileScreenConfig(),
    viewModel: ProfileViewModel = hiltViewModel(),
) {
    val state by viewModel.uiState.collectAsStateWithLifecycle()
    val context = LocalContext.current
    var showPermissionRationale by remember { mutableStateOf(false) }
    var showDeleteConfirmation by remember { mutableStateOf(false) }
    var permissionMessage by remember { mutableStateOf<String?>(null) }
    val permissionLauncher =
        rememberLauncherForActivityResult(ActivityResultContracts.RequestPermission()) { granted ->
            permissionMessage =
                if (granted) {
                    context.getString(R.string.notification_permission_granted)
                } else {
                    context.getString(R.string.notification_permission_denied)
                }
            if (granted) config.onPushSetup()
        }

    LaunchedEffect(state.accountDeleted) {
        if (state.accountDeleted) config.onAccountDeleted()
    }
    val actions =
        ProfileSettingsActions(
            permissionMessage = permissionMessage,
            setPermissionMessage = { permissionMessage = it },
            showPermissionRationale = { showPermissionRationale = true },
            showDeleteConfirmation = { showDeleteConfirmation = true },
            openSettings = {
                context.startActivity(
                    android.content.Intent(
                        android.provider.Settings.ACTION_APPLICATION_DETAILS_SETTINGS,
                        Uri.parse("package:${context.packageName}"),
                    ),
                )
            },
        )

    ProfileScreenBody(state, config, viewModel, actions)

    ProfileDialogs(
        ProfileDialogActions(
            showPermissionRationale = showPermissionRationale,
            showDeleteConfirmation = showDeleteConfirmation,
            dismissPermissionRationale = { showPermissionRationale = false },
            dismissDeleteConfirmation = { showDeleteConfirmation = false },
            requestPermission = {
                if (Build.VERSION.SDK_INT >= 33) {
                    permissionLauncher.launch(Manifest.permission.POST_NOTIFICATIONS)
                } else {
                    permissionMessage = context.getString(R.string.notification_permission_automatic)
                }
            },
            deleteAccount = viewModel::deleteAccount,
        ),
    )
}

@Composable
private fun ProfileScreenBody(
    state: ProfileUiState,
    config: ProfileScreenConfig,
    viewModel: ProfileViewModel,
    actions: ProfileSettingsActions,
) {
    Column(
        modifier = Modifier.fillMaxSize().verticalScroll(rememberScrollState()).padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(10.dp),
    ) {
        Text(stringResource(R.string.profile_title), style = MaterialTheme.typography.headlineMedium)
        if (state.isLoading) {
            Text(stringResource(R.string.profile_loading))
        } else {
            ProfileSettingsContent(state, config, viewModel, actions)
        }
    }
}

@Composable
private fun ProfileSettingsContent(
    state: ProfileUiState,
    config: ProfileScreenConfig,
    viewModel: ProfileViewModel,
    actions: ProfileSettingsActions,
) {
    val context = LocalContext.current
    ProfileIdentitySection(state)
    DefaultCorridorSection(state, viewModel::setDefaultCorridor)
    LanguageSection(viewModel)
    NotificationPreferencesSection(state, viewModel)
    PreferenceSwitch(
        label = stringResource(R.string.marketing_consent),
        checked = state.marketingConsent,
        onCheckedChange = viewModel::setMarketingConsent,
    )
    NotificationSetupButton(
        onSetup = {
            config.onPushSetup()
            actions.showPermissionRationale()
        },
        permissionMessage = actions.permissionMessage,
        onOpenSettings = actions.openSettings,
    )
    config.pushRegistrationError?.let { Text(it, color = MaterialTheme.colorScheme.error) }
    state.error?.let { ErrorState(it, onRetry = viewModel::refresh) }
    TextButton(onClick = { actions.setPermissionMessage(openLegal(context, config.privacyPolicyUrl)) }) {
        Text(stringResource(R.string.privacy_policy))
    }
    TextButton(onClick = { actions.setPermissionMessage(openLegal(context, config.termsUrl)) }) {
        Text(stringResource(R.string.terms_of_service))
    }
    AccountActionsSection(
        onLogout = config.onLogout,
        onDelete = actions.showDeleteConfirmation,
        isSaving = state.isSaving,
    )
}

@Composable
private fun ProfileDialogs(actions: ProfileDialogActions) {
    if (actions.showPermissionRationale) {
        AlertDialog(
            onDismissRequest = actions.dismissPermissionRationale,
            title = { Text(stringResource(R.string.notification_permission_title)) },
            text = { Text(stringResource(R.string.notification_permission_rationale)) },
            confirmButton = {
                TextButton(onClick = {
                    actions.dismissPermissionRationale()
                    actions.requestPermission()
                }) {
                    Text(stringResource(R.string.continue_label))
                }
            },
            dismissButton = {
                TextButton(onClick = actions.dismissPermissionRationale) {
                    Text(stringResource(R.string.cancel))
                }
            },
        )
    }

    if (actions.showDeleteConfirmation) {
        AlertDialog(
            onDismissRequest = actions.dismissDeleteConfirmation,
            title = { Text(stringResource(R.string.delete_account)) },
            text = { Text(stringResource(R.string.delete_account_confirmation)) },
            confirmButton = {
                TextButton(
                    onClick = {
                        actions.dismissDeleteConfirmation()
                        actions.deleteAccount()
                    },
                ) { Text(stringResource(R.string.delete_account)) }
            },
            dismissButton = {
                TextButton(onClick = actions.dismissDeleteConfirmation) {
                    Text(stringResource(R.string.cancel))
                }
            },
        )
    }
}

@Composable
private fun DefaultCorridorSection(
    state: ProfileUiState,
    onSelect: (String) -> Unit,
) {
    var corridorMenuExpanded by remember { mutableStateOf(false) }
    Text(stringResource(R.string.default_corridor), style = MaterialTheme.typography.titleMedium)
    Box {
        TextButton(onClick = { corridorMenuExpanded = true }) { Text(state.defaultCorridor) }
        DropdownMenu(
            expanded = corridorMenuExpanded,
            onDismissRequest = { corridorMenuExpanded = false },
        ) {
            com.remito.core.common.UserSettingsStore.SUPPORTED_CORRIDORS.forEach { corridor ->
                DropdownMenuItem(
                    text = { Text(corridor) },
                    onClick = {
                        onSelect(corridor)
                        corridorMenuExpanded = false
                    },
                )
            }
        }
    }
}

@Composable
private fun LanguageSection(viewModel: ProfileViewModel) {
    Text(stringResource(R.string.language))
    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
        TextButton(onClick = { viewModel.setLanguage("English") }) { Text("English") }
        TextButton(onClick = {}, enabled = false) { Text(stringResource(R.string.arabic_phase_two)) }
    }
}

@Composable
private fun NotificationPreferencesSection(
    state: ProfileUiState,
    viewModel: ProfileViewModel,
) {
    Text(stringResource(R.string.notification_preferences), style = MaterialTheme.typography.titleMedium)
    PreferenceSwitch(
        label = stringResource(R.string.rate_alert_notifications),
        checked = state.rateAlertsEnabled,
        onCheckedChange = viewModel::setRateAlertsEnabled,
    )
    PreferenceSwitch(
        label = stringResource(R.string.daily_summary_notifications),
        checked = state.dailySummaryEnabled,
        onCheckedChange = viewModel::setDailySummaryEnabled,
    )
    PreferenceSwitch(
        label = stringResource(R.string.news_notifications),
        checked = state.newsEnabled,
        onCheckedChange = { viewModel.setCategoryEnabled("news", it) },
    )
    PreferenceSwitch(
        label = stringResource(R.string.offer_notifications),
        checked = state.offersEnabled,
        onCheckedChange = { viewModel.setCategoryEnabled("offers", it) },
    )
    OutlinedTextField(
        value = state.quietStart,
        onValueChange = { viewModel.setQuietHours(it, state.quietEnd) },
        label = { Text(stringResource(R.string.quiet_hours_start)) },
        supportingText = { Text(stringResource(R.string.quiet_hours_help)) },
        modifier = Modifier.fillMaxWidth(),
        singleLine = true,
    )
    OutlinedTextField(
        value = state.quietEnd,
        onValueChange = { viewModel.setQuietHours(state.quietStart, it) },
        label = { Text(stringResource(R.string.quiet_hours_end)) },
        modifier = Modifier.fillMaxWidth(),
        singleLine = true,
    )
}

@Composable
private fun NotificationSetupButton(
    onSetup: () -> Unit,
    permissionMessage: String?,
    onOpenSettings: () -> Unit,
) {
    Button(
        onClick = onSetup,
        modifier = Modifier.fillMaxWidth().heightIn(min = 48.dp),
    ) {
        Text(stringResource(R.string.enable_notifications))
    }
    permissionMessage?.let { Text(it, color = MaterialTheme.colorScheme.error) }
    if (permissionMessage == LocalContext.current.getString(R.string.notification_permission_denied)) {
        TextButton(onClick = onOpenSettings) {
            Text(stringResource(R.string.open_notification_settings))
        }
    }
}

@Composable
private fun PreferenceSwitch(
    label: String,
    checked: Boolean,
    onCheckedChange: (Boolean) -> Unit,
) {
    Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
        Text(label, modifier = Modifier.weight(1f).padding(end = 8.dp))
        Switch(checked = checked, onCheckedChange = onCheckedChange)
    }
}

private fun openLegal(
    context: android.content.Context,
    url: String,
): String? {
    val uri = Uri.parse(url)
    if (uri.scheme != "https" || uri.host.isNullOrBlank() || uri.userInfo != null) {
        return context.getString(R.string.legal_url_not_configured)
    }
    CustomTabsIntent.Builder().build().launchUrl(context, uri)
    return null
}
