package com.remito.app

import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.Article
import androidx.compose.material.icons.automirrored.filled.CompareArrows
import androidx.compose.material.icons.automirrored.filled.ShowChart
import androidx.compose.material.icons.filled.Favorite
import androidx.compose.material.icons.filled.Home
import androidx.compose.material.icons.filled.Notifications
import androidx.compose.material.icons.filled.Person
import androidx.compose.material.icons.filled.SwapHoriz
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.remito.app.R
import com.remito.core.ui.theme.RemitoTheme
import com.remito.feature.alerts.AlertsScreen
import com.remito.feature.auth.AuthScreen
import com.remito.feature.auth.AuthViewModel
import com.remito.feature.compare.CompareScreen
import com.remito.feature.convert.ConvertScreen
import com.remito.feature.dashboard.DashboardScreen
import com.remito.feature.favourites.FavouritesScreen
import com.remito.feature.history.HistoryScreen
import com.remito.feature.news.NewsScreen
import com.remito.feature.profile.ProfileScreen
import com.remito.feature.profile.ProfileScreenConfig

private data class TabItem(
    val id: String,
    val label: Int,
    val contentDescription: Int,
    val icon: ImageVector,
    val content: @Composable () -> Unit,
)

private fun rateTabs(
    authViewModel: AuthViewModel,
    pushRegistration: PushRegistrationViewModel,
    pushRegistrationError: String?,
) = listOf(
    TabItem(
        "dashboard",
        R.string.tab_dashboard,
        R.string.tab_dashboard_description,
        Icons.Filled.Home,
    ) { DashboardScreen() },
    TabItem(
        "compare",
        R.string.tab_compare,
        R.string.tab_compare_description,
        Icons.AutoMirrored.Filled.CompareArrows,
    ) { CompareScreen() },
    TabItem(
        "convert",
        R.string.tab_convert,
        R.string.tab_convert_description,
        Icons.Filled.SwapHoriz,
    ) { ConvertScreen() },
    TabItem(
        "history",
        R.string.tab_history,
        R.string.tab_history_description,
        Icons.AutoMirrored.Filled.ShowChart,
    ) { HistoryScreen() },
    TabItem(
        "alerts",
        R.string.tab_alerts,
        R.string.tab_alerts_description,
        Icons.Filled.Notifications,
    ) { AlertsScreen() },
    profileTab(authViewModel, pushRegistration, pushRegistrationError),
    TabItem(
        "favourites",
        R.string.tab_favourites,
        R.string.tab_favourites_description,
        Icons.Filled.Favorite,
    ) { FavouritesScreen() },
    TabItem(
        "news",
        R.string.tab_news,
        R.string.tab_news_description,
        Icons.AutoMirrored.Filled.Article,
    ) { NewsScreen() },
)

private fun profileTab(
    authViewModel: AuthViewModel,
    pushRegistration: PushRegistrationViewModel,
    pushRegistrationError: String?,
) = TabItem(
    "profile",
    R.string.tab_profile,
    R.string.tab_profile_description,
    Icons.Filled.Person,
) {
    ProfileScreen(
        config =
            ProfileScreenConfig(
                onLogout = authViewModel::logout,
                onAccountDeleted = authViewModel::finishDeletedSession,
                onPushSetup = pushRegistration::registerCurrentToken,
                pushRegistrationError = pushRegistrationError,
                privacyPolicyUrl = BuildConfig.PRIVACY_POLICY_URL,
                termsUrl = BuildConfig.TERMS_URL,
            ),
    )
}

@Composable
fun RemitoApp(notificationTarget: NotificationTarget? = null) {
    val authViewModel: AuthViewModel = hiltViewModel()
    val pushRegistration: PushRegistrationViewModel = hiltViewModel()
    val authState by authViewModel.uiState.collectAsStateWithLifecycle()
    val pushRegistrationError by pushRegistration.registrationError.collectAsStateWithLifecycle()
    LaunchedEffect(authState.isAuthenticated) {
        if (authState.isAuthenticated) pushRegistration.registerCurrentToken()
    }
    RemitoTheme {
        if (!authState.isAuthenticated) {
            AuthScreen(authViewModel)
        } else {
            RemitoRateApp(authViewModel, notificationTarget, pushRegistration, pushRegistrationError)
        }
    }
}

@Composable
@OptIn(ExperimentalMaterial3Api::class)
private fun RemitoRateApp(
    authViewModel: AuthViewModel,
    notificationTarget: NotificationTarget?,
    pushRegistration: PushRegistrationViewModel,
    pushRegistrationError: String?,
) {
    val tabs = rateTabs(authViewModel, pushRegistration, pushRegistrationError)
    var selectedTab by remember { mutableStateOf(tabs.first().id) }
    LaunchedEffect(notificationTarget?.receivedAt) {
        notificationTarget?.let { selectedTab = it.tabId }
    }

    Scaffold(
        topBar = { TopAppBar(title = { Text(stringResource(R.string.app_name)) }) },
        bottomBar = {
            NavigationBar(modifier = Modifier.horizontalScroll(rememberScrollState())) {
                tabs.forEach { tab ->
                    NavigationBarItem(
                        modifier = Modifier.width(88.dp),
                        selected = selectedTab == tab.id,
                        onClick = { selectedTab = tab.id },
                        icon = {
                            Icon(
                                imageVector = tab.icon,
                                contentDescription = stringResource(tab.contentDescription),
                            )
                        },
                        label = { Text(stringResource(tab.label)) },
                    )
                }
            }
        },
    ) { contentPadding ->
        Box(Modifier.padding(contentPadding)) {
            when (selectedTab) {
                "news" -> NewsScreen(articleSlug = notificationTarget?.articleSlug)
                else -> tabs.firstOrNull { it.id == selectedTab }?.content?.invoke()
            }
        }
    }
}
