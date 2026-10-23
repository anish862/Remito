package com.remito.app

import android.app.NotificationChannel
import android.app.NotificationManager
import android.content.Context
import android.os.Build

object NotificationChannels {
    const val RATE_ALERTS = "rate_alerts"
    const val NEWS = "news"
    const val OFFERS = "offers"

    fun create(context: Context) {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) return
        val manager = context.getSystemService(NotificationManager::class.java)
        manager.createNotificationChannels(
            listOf(
                NotificationChannel(
                    RATE_ALERTS,
                    context.getString(R.string.notification_channel_alerts),
                    NotificationManager.IMPORTANCE_DEFAULT,
                ),
                NotificationChannel(
                    NEWS,
                    context.getString(R.string.notification_channel_news),
                    NotificationManager.IMPORTANCE_DEFAULT,
                ),
                NotificationChannel(
                    OFFERS,
                    context.getString(R.string.notification_channel_offers),
                    NotificationManager.IMPORTANCE_DEFAULT,
                ),
            ),
        )
    }
}
