package com.remito.feature.compare

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material3.AssistChip
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import coil.compose.AsyncImage
import com.remito.core.common.Formatters
import com.remito.core.network.ExchangeOfferDto
import com.remito.core.ui.SponsoredLabel
import com.remito.core.ui.relativeTimeText
import java.math.BigDecimal
import java.time.Instant
import java.time.format.DateTimeParseException
import java.util.Locale

private const val RATE_DECIMAL_PLACES = 6
private const val MONEY_DECIMAL_PLACES = 2
private const val DETAILS_PADDING_DP = 24
private const val OFFER_PADDING_DP = 14

@Composable
internal fun ExchangeOfferCard(
    offer: ExchangeOfferDto,
    from: String,
    to: String,
    onDetails: () -> Unit,
    onChoose: () -> Unit,
) {
    val locale = LocalContext.current.resources.configuration.locales[0] ?: Locale.getDefault()
    Card(modifier = Modifier.fillMaxWidth()) {
        Column(
            modifier = Modifier.fillMaxWidth().padding(OFFER_PADDING_DP.dp),
            verticalArrangement = Arrangement.spacedBy(7.dp),
        ) {
            OfferHeader(offer)
            Text(
                stringResource(
                    R.string.compare_rate,
                    from,
                    Formatters.number(offer.rate, locale, RATE_DECIMAL_PLACES),
                    to,
                ),
                style = MaterialTheme.typography.bodyLarge,
            )
            val fee = offer.fee
            Text(
                fee?.let {
                    stringResource(
                        R.string.compare_fee_provided,
                        Formatters.number(it, locale, MONEY_DECIMAL_PLACES),
                        from,
                    )
                } ?: stringResource(R.string.compare_fee_not_provided),
            )
            offer.amountReceived?.let { amountReceived ->
                Text(
                    stringResource(
                        R.string.compare_received_amount,
                        Formatters.number(amountReceived, locale, MONEY_DECIMAL_PLACES),
                        to,
                    ),
                    style = MaterialTheme.typography.titleMedium,
                )
            }
            OfferFreshness(offer)
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.End,
                verticalAlignment = Alignment.CenterVertically,
            ) {
                TextButton(onClick = onDetails, modifier = Modifier.heightIn(min = 48.dp)) {
                    Text(stringResource(R.string.compare_details))
                }
                Button(onClick = onChoose, modifier = Modifier.heightIn(min = 48.dp)) {
                    Text(stringResource(R.string.compare_choose))
                }
            }
        }
    }
}

@Composable
private fun OfferHeader(offer: ExchangeOfferDto) {
    Row(verticalAlignment = Alignment.CenterVertically) {
        ExchangeLogo(offer.logo, offer.name, Modifier.size(42.dp))
        Column(modifier = Modifier.weight(1f).padding(start = 10.dp)) {
            Text(offer.name, style = MaterialTheme.typography.titleMedium)
            Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                if (offer.isSponsored) SponsoredLabel()
                if (offer.isBestRate) {
                    AssistChip(
                        onClick = {},
                        label = { Text(stringResource(R.string.compare_best_rate_badge)) },
                        modifier = Modifier.heightIn(min = 40.dp),
                    )
                }
                if (offer.hasApp) {
                    AssistChip(
                        onClick = {},
                        label = { Text(stringResource(R.string.compare_has_app_chip)) },
                        modifier = Modifier.heightIn(min = 40.dp),
                    )
                }
            }
        }
    }
}

@Composable
private fun OfferFreshness(offer: ExchangeOfferDto) {
    val locale = LocalContext.current.resources.configuration.locales[0] ?: Locale.getDefault()
    val timestamp = parseTimestamp(offer.lastUpdated)
    Text(
        stringResource(
            R.string.compare_updated,
            timestamp?.let { relativeTimeText(it, locale) } ?: offer.lastUpdated,
        ),
        style = MaterialTheme.typography.bodySmall,
    )
    if (offer.isStale) {
        Text(
            stringResource(R.string.compare_stale_offer),
            color = MaterialTheme.colorScheme.error,
        )
    }
}

@Composable
private fun ExchangeLogo(
    url: String?,
    name: String,
    modifier: Modifier = Modifier,
) {
    if (url.isNullOrBlank()) {
        Card(shape = CircleShape, modifier = modifier) {
            Box(contentAlignment = Alignment.Center) {
                Text(name.take(1).uppercase(Locale.getDefault()))
            }
        }
    } else {
        AsyncImage(
            model = url,
            contentDescription = stringResource(R.string.compare_exchange_logo, name),
            modifier = modifier,
        )
    }
}

@Composable
internal fun ExchangeDetailsSheet(
    offer: ExchangeOfferDto,
    from: String,
    to: String,
    amount: String,
) {
    val locale = LocalContext.current.resources.configuration.locales[0] ?: Locale.getDefault()
    Column(
        modifier = Modifier.fillMaxWidth().padding(DETAILS_PADDING_DP.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        Text(offer.name, style = MaterialTheme.typography.headlineSmall)
        Text(
            stringResource(
                R.string.compare_rate,
                from,
                Formatters.number(offer.rate, locale, RATE_DECIMAL_PLACES),
                to,
            ),
        )
        offer.amountReceived?.let { amountReceived ->
            Text(
                stringResource(
                    R.string.compare_amount_received_for,
                    Formatters.number(amount.toBigDecimalOrNull() ?: BigDecimal.ZERO, locale, MONEY_DECIMAL_PLACES),
                    from,
                    Formatters.number(amountReceived, locale, MONEY_DECIMAL_PLACES),
                    to,
                ),
            )
        }
        Text(stringResource(R.string.compare_fee_breakdown), style = MaterialTheme.typography.titleMedium)
        Text(
            offer.fee?.let { fee ->
                stringResource(
                    R.string.compare_fee_provided,
                    Formatters.number(fee, locale, MONEY_DECIMAL_PLACES),
                    from,
                )
            } ?: stringResource(R.string.compare_fee_not_provided),
        )
        Text(offer.feeDisclosure)
        Text(stringResource(R.string.compare_disclaimer), style = MaterialTheme.typography.bodySmall)
        Spacer(Modifier.heightIn(min = 8.dp))
    }
}

internal data class RedirectConfirmationState(
    val offer: ExchangeOfferDto,
    val isLoading: Boolean,
    val amount: String,
    val error: String?,
    val onCancel: () -> Unit,
    val onConfirm: () -> Unit,
)

@Composable
internal fun RedirectConfirmationSheet(state: RedirectConfirmationState) {
    Column(
        modifier = Modifier.fillMaxWidth().padding(DETAILS_PADDING_DP.dp),
        verticalArrangement = Arrangement.spacedBy(14.dp),
    ) {
        Text(stringResource(R.string.compare_confirmation_title), style = MaterialTheme.typography.headlineSmall)
        Text(stringResource(R.string.compare_confirmation_message, state.offer.name))
        Text(stringResource(R.string.compare_disclaimer), style = MaterialTheme.typography.bodySmall)
        state.error?.let { Text(it, color = MaterialTheme.colorScheme.error) }
        Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
            TextButton(
                onClick = state.onCancel,
                enabled = !state.isLoading,
                modifier = Modifier.heightIn(min = 48.dp),
            ) {
                Text(stringResource(R.string.compare_cancel))
            }
            Button(
                onClick = state.onConfirm,
                enabled = !state.isLoading && state.amount.toBigDecimalOrNull()?.signum() == 1,
                modifier = Modifier.heightIn(min = 48.dp),
            ) {
                if (state.isLoading) {
                    CircularProgressIndicator(modifier = Modifier.size(18.dp))
                } else {
                    Text(stringResource(R.string.compare_continue))
                }
            }
        }
    }
}

private fun parseTimestamp(timestamp: String): Instant? =
    try {
        Instant.parse(timestamp)
    } catch (_: DateTimeParseException) {
        null
    }
