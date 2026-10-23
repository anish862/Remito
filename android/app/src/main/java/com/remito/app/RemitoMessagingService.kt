package com.remito.app

import android.Manifest
import android.annotation.SuppressLint
import android.app.PendingIntent
import android.content.Intent
import android.content.pm.PackageManager
import android.os.Build
import androidx.core.app.NotificationCompat
import androidx.core.app.NotificationManagerCompat
import androidx.core.content.ContextCompat
import com.google.firebase.messaging.FirebaseMessagingService
import com.google.firebase.messaging.RemoteMessage
import com.remito.core.network.DevicePlatform
import com.remito.core.network.DeviceRegistrationRequest
import com.remito.core.network.RemitoApi
import com.remito.core.network.TokenStore
import dagger.hilt.android.AndroidEntryPoint
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.launch
import java.io.IOException
import javax.inject.Inject

@AndroidEntryPoint
class RemitoMessagingService : FirebaseMessagingService() {
    @Inject lateinit var api: RemitoApi

    @Inject lateinit var tokenStore: TokenStore

    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)

    override fun onNewToken(token: String) {
        registerToken(token)
    }

    @SuppressLint("MissingPermission")
    override fun onMessageReceived(message: RemoteMessage) {
        if (tokenStore.accessToken().isNullOrBlank()) return
        val type = message.data["type"] ?: "rate_alert"
        val channel = channelFor(type)
        if (!isCategoryEnabled(type) || !hasNotificationPermission()) return

        val target = targetFor(type)
        val intent =
            Intent(this, MainActivity::class.java).apply {
                flags = Intent.FLAG_ACTIVITY_CLEAR_TOP or Intent.FLAG_ACTIVITY_SINGLE_TOP
                putExtra(EXTRA_TARGET_SCREEN, target)
                message.data["slug"]?.let { putExtra(EXTRA_ARTICLE_SLUG, it) }
            }
        val pendingIntent =
            PendingIntent.getActivity(
                this,
                type.hashCode(),
                intent,
                PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE,
            )
        val notification =
            NotificationCompat
                .Builder(this, channel)
                .setSmallIcon(android.R.drawable.ic_dialog_info)
                .setContentTitle(message.notification?.title ?: getString(R.string.notification_default_title))
                .setContentText(message.notification?.body ?: getString(R.string.notification_default_body))
                .setAutoCancel(true)
                .setContentIntent(pendingIntent)
                .build()
        NotificationManagerCompat.from(this).notify(System.currentTimeMillis().toInt(), notification)
    }

    override fun onDestroy() {
        scope.cancel()
        super.onDestroy()
    }

    private fun registerToken(token: String) {
        if (tokenStore.accessToken().isNullOrBlank()) return
        scope.launch {
            try {
                val response = api.registerDevice(DeviceRegistrationRequest(token, DevicePlatform.ANDROID))
                if (!response.isSuccessful && BuildConfig.DEBUG) {
                    android.util.Log.e("RemitoPush", "Device registration failed with HTTP ${response.code()}.")
                }
            } catch (error: CancellationException) {
                throw error
            } catch (error: IOException) {
                if (BuildConfig.DEBUG) android.util.Log.e("RemitoPush", "Device token registration failed.", error)
            }
        }
    }

    private fun channelFor(type: String): String =
        when {
            type == "news" || type == "news_update" -> NotificationChannels.NEWS
            type == "offer" || type == "marketing" -> NotificationChannels.OFFERS
            else -> NotificationChannels.RATE_ALERTS
        }

    private fun targetFor(type: String): String =
        when (type) {
            "news", "news_update" -> "news"
            "offer", "marketing" -> "dashboard"
            else -> "alerts"
        }

    private fun isCategoryEnabled(type: String): Boolean {
        val prefs = getSharedPreferences("user-settings", MODE_PRIVATE)
        return when {
            type == "news" || type == "news_update" -> prefs.getBoolean("news_enabled", true)
            type == "offer" || type == "marketing" -> prefs.getBoolean("offers_enabled", false)
            else -> true
        }
    }

    private fun hasNotificationPermission(): Boolean =
        Build.VERSION.SDK_INT < Build.VERSION_CODES.TIRAMISU ||
            ContextCompat.checkSelfPermission(this, Manifest.permission.POST_NOTIFICATIONS) ==
            PackageManager.PERMISSION_GRANTED
}
