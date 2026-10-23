package com.remito.core.ui

import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.unit.Density
import org.junit.Rule
import org.junit.Test
import java.math.BigDecimal

class RateComponentsTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun rateCardShowsAccessibleRateChangeAndFavouriteAction() {
        composeRule.setContent {
            MaterialTheme {
                RateCard(
                    currency = "INR",
                    rate = "216.7",
                    changePercent = BigDecimal("0.4"),
                    lastUpdated = "Updated now",
                    isFavourite = false,
                    onFavouriteClick = {},
                )
            }
        }

        composeRule.onNodeWithText("INR").assertIsDisplayed()
        composeRule.onNodeWithText("0.4% in 24 hours").assertIsDisplayed()
        composeRule.onNodeWithText("Add favourite").assertIsDisplayed()
    }

    @Test
    fun rateCardRemainsVisibleAtDoubleFontScale() {
        composeRule.setContent {
            CompositionLocalProvider(LocalDensity provides Density(1f, 2f)) {
                MaterialTheme {
                    RateCard(
                        currency = "INR",
                        rate = "216.7",
                        changePercent = BigDecimal("0.4"),
                        lastUpdated = "Updated now",
                        isFavourite = false,
                        onFavouriteClick = {},
                    )
                }
            }
        }

        composeRule.onNodeWithText("INR").assertIsDisplayed()
        composeRule.onNodeWithText("Add favourite").assertIsDisplayed()
    }
}
