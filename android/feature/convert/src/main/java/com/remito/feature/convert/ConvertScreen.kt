package com.remito.feature.convert

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.remito.core.common.CurrencyCode
import com.remito.core.common.Formatters
import com.remito.core.common.Money
import com.remito.core.ui.AmountField
import com.remito.core.ui.CurrencyPicker
import com.remito.core.ui.ErrorState
import com.remito.core.ui.SkeletonLoader
import com.remito.core.ui.StaleBanner
import com.remito.core.ui.relativeTimeText
import java.time.Instant
import java.util.Locale

@Composable
fun ConvertScreen(viewModel: ConvertViewModel = hiltViewModel()) {
    val state by viewModel.uiState.collectAsStateWithLifecycle()
    val content = state as ConvertUiState.Content
    val locale = LocalConfiguration.current.locales[0] ?: Locale.getDefault()
    Column(
        modifier = Modifier.fillMaxSize().padding(20.dp),
        verticalArrangement = Arrangement.spacedBy(18.dp),
    ) {
        Text(stringResource(R.string.convert_title), style = MaterialTheme.typography.headlineMedium)
        AmountField(amount = content.amount, onAmountChanged = viewModel::setAmount)
        Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
            CurrencyPicker(content.from, content.currencies, viewModel::selectFrom)
            Button(onClick = viewModel::swap, modifier = Modifier.padding(horizontal = 8.dp)) {
                Text(stringResource(R.string.swap_currencies))
            }
            CurrencyPicker(content.to, content.currencies, viewModel::selectTo)
        }
        if (content.isLoading) SkeletonLoader()
        content.error?.let { ErrorState(it, onRetry = viewModel::retry) }
        content.errorResource?.let { ErrorState(stringResource(it), onRetry = viewModel::retry) }
        content.result?.let { quote ->
            if (content.isOffline) StaleBanner()
            Text(
                text =
                    stringResource(
                        R.string.conversion_result,
                        Formatters.money(Money(quote.convertedAmount, CurrencyCode(quote.toCurrency)), locale),
                    ),
                style = MaterialTheme.typography.headlineSmall,
            )
            Text(
                stringResource(
                    R.string.rate_used,
                    Formatters.number(quote.rate, locale, maximumFractionDigits = 6),
                    quote.fromCurrency,
                    quote.toCurrency,
                ),
            )
            val time = runCatching { Instant.parse(quote.timestamp) }.getOrNull()
            Text(
                stringResource(
                    R.string.rate_timestamp,
                    time?.let { relativeTimeText(it, locale) } ?: quote.timestamp,
                ),
            )
            if (content.isOffline) Text(stringResource(R.string.offline_rate_label))
        }
    }
}
