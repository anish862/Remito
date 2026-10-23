package com.remito.app

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import dagger.hilt.android.AndroidEntryPoint

@AndroidEntryPoint
class MainActivity : ComponentActivity() {
    private var notificationTarget by mutableStateOf<NotificationTarget?>(null)

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        notificationTarget = parseNotificationTarget(intent)
        setContent {
            RemitoApp(notificationTarget)
        }
    }

    override fun onNewIntent(intent: android.content.Intent) {
        super.onNewIntent(intent)
        setIntent(intent)
        notificationTarget = parseNotificationTarget(intent)
    }
}

data class NotificationTarget(
    val tabId: String,
    val articleSlug: String? = null,
    val receivedAt: Long,
)

internal fun parseNotificationTarget(intent: android.content.Intent?): NotificationTarget? =
    resolveNotificationTarget(
        intent?.getStringExtra(EXTRA_TARGET_SCREEN),
        intent?.getStringExtra(EXTRA_ARTICLE_SLUG),
    )

internal fun resolveNotificationTarget(
    tab: String?,
    articleSlug: String?,
    receivedAt: Long = System.nanoTime(),
): NotificationTarget? {
    val safeTab =
        tab?.takeIf { it in setOf("dashboard", "alerts", "news", "favourites", "profile") }
            ?: return null
    val slug =
        articleSlug?.takeIf {
            safeTab == "news" && it.matches(Regex("[a-zA-Z0-9-]{1,160}"))
        }
    return NotificationTarget(safeTab, slug, receivedAt)
}

const val EXTRA_TARGET_SCREEN = "com.remito.app.extra.TARGET_SCREEN"
const val EXTRA_ARTICLE_SLUG = "com.remito.app.extra.ARTICLE_SLUG"
