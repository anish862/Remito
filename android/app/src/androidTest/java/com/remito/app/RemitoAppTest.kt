package com.remito.app

import androidx.compose.ui.test.assertCountEquals
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onAllNodesWithText
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class RemitoAppTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun bottomNavigationShowsFiveTabsAndOpensCompare() {
        composeRule.setContent { RemitoApp() }

        listOf("Dashboard", "Compare", "Convert", "Alerts", "Profile").forEach { label ->
            composeRule.onNodeWithText(label).assertIsDisplayed()
        }

        composeRule.onAllNodesWithText("Compare").assertCountEquals(1)
        composeRule.onNodeWithText("Compare").performClick()
        composeRule.onAllNodesWithText("Compare").assertCountEquals(2)
    }
}
