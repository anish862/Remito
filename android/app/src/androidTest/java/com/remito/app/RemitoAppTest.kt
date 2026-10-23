package com.remito.app

import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithText
import com.remito.core.network.OtpChannel
import com.remito.feature.auth.AuthActions
import com.remito.feature.auth.AuthForm
import com.remito.feature.auth.AuthUiState
import org.junit.Rule
import org.junit.Test

class RemitoAppTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun onboardingOffersOtpSignIn() {
        composeRule.setContent {
            AuthForm(
                state = AuthUiState(),
                actions = AuthActions({}, {}, {}, {}, {}),
            )
        }

        composeRule.onNodeWithText("Welcome to Remito").assertIsDisplayed()
        composeRule.onNodeWithText("Send verification code").assertIsDisplayed()
    }

    @Test
    fun otpVerificationShowsCodeEntryAndVerifyAction() {
        composeRule.setContent {
            AuthForm(
                state = AuthUiState(channel = OtpChannel.PHONE, codeSent = true),
                actions = AuthActions({}, {}, {}, {}, {}),
            )
        }

        composeRule.onNodeWithText("Verification code").assertIsDisplayed()
        composeRule.onNodeWithText("Verify and sign in").assertIsDisplayed()
    }
}
