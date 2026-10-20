package com.remito.app

import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.CompareArrows
import androidx.compose.material.icons.filled.Home
import androidx.compose.material.icons.filled.Notifications
import androidx.compose.material.icons.filled.Person
import androidx.compose.material.icons.filled.SwapHoriz
import androidx.compose.material3.Icon
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.res.stringResource
import com.remito.app.R
import com.remito.core.ui.theme.RemitoTheme
import com.remito.feature.alerts.AlertsScreen
import com.remito.feature.compare.CompareScreen
import com.remito.feature.convert.ConvertScreen
import com.remito.feature.dashboard.DashboardScreen
import com.remito.feature.profile.ProfileScreen

private data class TabItem(
    val label: Int,
    val contentDescription: Int,
    val icon: ImageVector,
    val content: @Composable () -> Unit,
)

@Composable
fun RemitoApp() {
    val tabs =
        listOf(
            TabItem(
                R.string.tab_dashboard,
                R.string.tab_dashboard_description,
                Icons.Filled.Home,
            ) { DashboardScreen() },
            TabItem(
                R.string.tab_compare,
                R.string.tab_compare_description,
                Icons.AutoMirrored.Filled.CompareArrows,
            ) { CompareScreen() },
            TabItem(
                R.string.tab_convert,
                R.string.tab_convert_description,
                Icons.Filled.SwapHoriz,
            ) { ConvertScreen() },
            TabItem(
                R.string.tab_alerts,
                R.string.tab_alerts_description,
                Icons.Filled.Notifications,
            ) { AlertsScreen() },
            TabItem(
                R.string.tab_profile,
                R.string.tab_profile_description,
                Icons.Filled.Person,
            ) { ProfileScreen() },
        )
    var selectedTab by remember { mutableStateOf(tabs.first()) }

    RemitoTheme {
        Scaffold(
            bottomBar = {
                NavigationBar {
                    tabs.forEach { tab ->
                        NavigationBarItem(
                            selected = selectedTab == tab,
                            onClick = { selectedTab = tab },
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
                selectedTab.content()
            }
        }
    }
}
