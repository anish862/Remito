package com.remito.app

import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertNull
import org.junit.jupiter.api.Test

private const val ALERT_TIMESTAMP = 9L
private const val NEWS_TIMESTAMP = 3L

class NotificationTargetTest {
    @Test
    fun rateAlertOpensAlertsScreen() {
        assertEquals(
            NotificationTarget("alerts", null, ALERT_TIMESTAMP),
            resolveNotificationTarget("alerts", null, ALERT_TIMESTAMP),
        )
    }

    @Test
    fun newsNotificationKeepsOnlySafeArticleSlug() {
        assertEquals(
            NotificationTarget("news", "market-update", NEWS_TIMESTAMP),
            resolveNotificationTarget("news", "market-update", NEWS_TIMESTAMP),
        )
        assertEquals(
            NotificationTarget("news", null, NEWS_TIMESTAMP),
            resolveNotificationTarget("news", "unsafe?url", NEWS_TIMESTAMP),
        )
    }

    @Test
    fun unknownScreenDoesNotNavigate() {
        assertNull(resolveNotificationTarget("payment", null))
    }
}
