package com.remito.feature.compare

import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.captureToImage
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.test.ext.junit.runners.AndroidJUnit4
import com.remito.core.network.ExchangeOfferDto
import com.remito.core.network.RedirectResponseDto
import com.remito.core.ui.theme.RemitoTheme
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import java.math.BigDecimal

@RunWith(AndroidJUnit4::class)
class CompareRedirectUiTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun confirmationShowsDisclosureAndContinues() {
        var confirmed = false
        composeRule.setContent {
            RemitoTheme {
                RedirectConfirmationSheet(
                    RedirectConfirmationState(
                        offer = offer(),
                        isLoading = false,
                        amount = "100",
                        error = null,
                        onCancel = {},
                        onConfirm = { confirmed = true },
                    ),
                )
            }
        }

        composeRule
            .onNodeWithText(
                "You are leaving Remito to complete payment with Gulf Exchange. Remito does not process payments.",
            ).assertIsDisplayed()
        composeRule.onNodeWithText("Continue").performClick()
        assertTrue(confirmed)
    }

    @Test
    fun confirmationScreenshotCanBeCaptured() {
        composeRule.setContent {
            RemitoTheme {
                RedirectConfirmationSheet(
                    RedirectConfirmationState(
                        offer = offer(),
                        isLoading = false,
                        amount = "100",
                        error = null,
                        onCancel = {},
                        onConfirm = {},
                    ),
                )
            }
        }

        val image = composeRule.onNodeWithText("Continue").captureToImage()
        assertTrue(image.width > 0)
        assertTrue(image.height > 0)
    }

    @Test
    fun failedAppAndStoreIntentsFallBackToGateway() {
        val launcher = RecordingLauncher()
        val launched =
            launcher.launchRedirect(
                RedirectResponseDto(
                    destinationUrl = "partner-app://pay",
                    fallbackUrl = "https://play.google.com/store/apps/details?id=partner",
                    gatewayUrl = "https://pay.example/redirect?click=opaque",
                    clickId = "opaque",
                    disclosureText = "Remito does not process payments.",
                ),
                hasApp = true,
            )

        assertTrue(launched)
        assertEquals(
            listOf(
                "app:partner-app://pay",
                "store:https://play.google.com/store/apps/details?id=partner",
                "gateway:https://pay.example/redirect?click=opaque",
            ),
            launcher.attempts,
        )
    }

    @Test
    fun unavailableAppOpensPlayStoreBeforeGateway() {
        val launcher = RecordingLauncher(storeOpens = true)
        val launched =
            launcher.launchRedirect(
                RedirectResponseDto(
                    destinationUrl = "https://pay.example/start",
                    fallbackUrl = "https://play.google.com/store/apps/details?id=partner",
                    gatewayUrl = "https://pay.example/start",
                    clickId = "opaque",
                    disclosureText = "Remito does not process payments.",
                ),
                hasApp = false,
            )

        assertTrue(launched)
        assertEquals(
            listOf("store:https://play.google.com/store/apps/details?id=partner"),
            launcher.attempts,
        )
    }

    private fun offer() =
        ExchangeOfferDto(
            id = "exchange-id",
            name = "Gulf Exchange",
            logo = null,
            rate = BigDecimal("216.7"),
            fee = null,
            amountReceived = BigDecimal("21670"),
            feeDisclosure = "Fee not provided",
            lastUpdated = "2026-10-23T10:00:00Z",
            isStale = false,
            hasApp = true,
            isSponsored = false,
            isBestRate = true,
        )

    private class RecordingLauncher(
        private val storeOpens: Boolean = false,
    ) : RedirectDestinationLauncher {
        val attempts = mutableListOf<String>()

        override fun openApp(url: String): Boolean {
            attempts += "app:$url"
            return false
        }

        override fun openStore(url: String): Boolean {
            attempts += "store:$url"
            return storeOpens
        }

        override fun openGateway(url: String): Boolean {
            attempts += "gateway:$url"
            return true
        }
    }
}
