package com.remito.feature.compare

import android.content.ActivityNotFoundException
import android.content.Context
import android.content.Intent
import android.net.Uri
import androidx.browser.customtabs.CustomTabsIntent
import com.remito.core.network.RedirectResponseDto

interface RedirectDestinationLauncher {
    fun openApp(url: String): Boolean

    fun openStore(url: String): Boolean

    fun openGateway(url: String): Boolean
}

class AndroidRedirectDestinationLauncher(
    private val context: Context,
) : RedirectDestinationLauncher {
    override fun openApp(url: String): Boolean {
        val uri = Uri.parse(url)
        if (uri.scheme.isNullOrBlank() || uri.scheme in UNSAFE_SCHEMES) return false
        return launch(Intent(Intent.ACTION_VIEW, uri))
    }

    override fun openStore(url: String): Boolean {
        val uri = Uri.parse(url)
        if (!uri.isStoreUrl()) return false
        return launch(Intent(Intent.ACTION_VIEW, uri))
    }

    override fun openGateway(url: String): Boolean {
        val uri = Uri.parse(url)
        if (uri.scheme != "https" || uri.host.isNullOrBlank()) return false
        return try {
            CustomTabsIntent.Builder().build().launchUrl(context, uri)
            true
        } catch (_: ActivityNotFoundException) {
            launch(Intent(Intent.ACTION_VIEW, uri))
        } catch (_: SecurityException) {
            false
        }
    }

    private fun launch(intent: Intent): Boolean =
        try {
            context.startActivity(intent)
            true
        } catch (_: ActivityNotFoundException) {
            false
        } catch (_: SecurityException) {
            false
        }

    private companion object {
        val UNSAFE_SCHEMES = setOf("javascript", "data", "file", "content")
    }
}

internal fun RedirectDestinationLauncher.launchRedirect(
    response: RedirectResponseDto,
    hasApp: Boolean,
): Boolean {
    val appOpened = hasApp && openApp(response.destinationUrl)
    val storeUrl = response.fallbackUrl?.takeIf { Uri.parse(it).isStoreUrl() }
    val storeOpened = !appOpened && storeUrl?.let(::openStore) == true
    val gatewayUrl =
        response.gatewayUrl
            ?: response.fallbackUrl?.takeUnless { Uri.parse(it).isStoreUrl() }
            ?: response.destinationUrl.takeUnless { Uri.parse(it).isStoreUrl() }
    return appOpened || storeOpened || gatewayUrl?.let(::openGateway) == true
}

internal fun Uri.isStoreUrl(): Boolean =
    scheme == "market" ||
        (scheme == "https" && host in setOf("play.google.com", "market.android.com"))
