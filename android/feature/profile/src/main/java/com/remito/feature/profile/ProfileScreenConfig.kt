package com.remito.feature.profile

data class ProfileScreenConfig(
    val onLogout: () -> Unit = {},
    val onAccountDeleted: () -> Unit = {},
    val onPushSetup: () -> Unit = {},
    val pushRegistrationError: String? = null,
    val privacyPolicyUrl: String = "",
    val termsUrl: String = "",
)

data class ProfileSettingsActions(
    val permissionMessage: String?,
    val setPermissionMessage: (String?) -> Unit,
    val showPermissionRationale: () -> Unit,
    val showDeleteConfirmation: () -> Unit,
    val openSettings: () -> Unit,
)

data class ProfileDialogActions(
    val showPermissionRationale: Boolean,
    val showDeleteConfirmation: Boolean,
    val dismissPermissionRationale: () -> Unit,
    val dismissDeleteConfirmation: () -> Unit,
    val requestPermission: () -> Unit,
    val deleteAccount: () -> Unit,
)
