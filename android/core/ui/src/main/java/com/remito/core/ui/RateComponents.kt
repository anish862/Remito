package com.remito.core.ui

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.ArrowDownward
import androidx.compose.material.icons.filled.ArrowDropDown
import androidx.compose.material.icons.filled.ArrowUpward
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.LiveRegionMode
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.liveRegion
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import com.remito.core.common.Formatters
import com.remito.core.common.RelativeTimeTemplates
import com.remito.core.ui.R
import java.math.BigDecimal
import java.time.Instant
import java.util.Locale
import com.remito.core.common.R as CommonR

@Composable
fun relativeTimeText(
    timestamp: Instant,
    locale: Locale,
): String =
    Formatters.relativeTime(
        timestamp = timestamp,
        locale = locale,
        templates =
            RelativeTimeTemplates(
                justNow = stringResource(CommonR.string.relative_just_now),
                minuteSingular = stringResource(CommonR.string.relative_minute_singular),
                minutePlural = stringResource(CommonR.string.relative_minute_plural),
                hourSingular = stringResource(CommonR.string.relative_hour_singular),
                hourPlural = stringResource(CommonR.string.relative_hour_plural),
                daySingular = stringResource(CommonR.string.relative_day_singular),
                dayPlural = stringResource(CommonR.string.relative_day_plural),
            ),
    )

@Composable
fun StaleBanner(modifier: Modifier = Modifier) {
    Card(
        modifier = modifier.fillMaxWidth().semantics { liveRegion = LiveRegionMode.Polite },
    ) {
        Text(
            text = stringResource(R.string.rate_data_stale),
            modifier = Modifier.padding(16.dp),
            color = MaterialTheme.colorScheme.onSurface,
        )
    }
}

@Composable
@Suppress("LongParameterList")
fun RateCard(
    currency: String,
    rate: String,
    changePercent: BigDecimal,
    lastUpdated: String,
    isFavourite: Boolean,
    onFavouriteClick: () -> Unit,
    modifier: Modifier = Modifier,
) {
    val increased = changePercent.signum() >= 0
    val locale = LocalConfiguration.current.locales[0] ?: Locale.getDefault()
    val favouriteDescription =
        stringResource(
            if (isFavourite) R.string.remove_favourite_for else R.string.add_favourite_for,
            currency,
        )
    Card(modifier = modifier.fillMaxWidth()) {
        Row(
            modifier = Modifier.padding(16.dp).fillMaxWidth(),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.SpaceBetween,
        ) {
            Column(modifier = Modifier.weight(1f)) {
                Text(currency, style = MaterialTheme.typography.titleMedium)
                Text(
                    Formatters.number(BigDecimal(rate), locale, maximumFractionDigits = 6),
                    style = MaterialTheme.typography.headlineSmall,
                )
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Icon(
                        imageVector = if (increased) Icons.Filled.ArrowUpward else Icons.Filled.ArrowDownward,
                        contentDescription = null,
                        tint = if (increased) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.error,
                    )
                    Text(
                        text =
                            stringResource(
                                R.string.change_percent,
                                Formatters.number(changePercent, locale, maximumFractionDigits = 2),
                            ),
                        color = if (increased) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.error,
                    )
                }
                Text(lastUpdated, style = MaterialTheme.typography.bodySmall)
            }
            Button(
                onClick = onFavouriteClick,
                modifier =
                    Modifier.heightIn(min = 48.dp).semantics {
                        contentDescription = favouriteDescription
                    },
            ) {
                Text(stringResource(if (isFavourite) R.string.remove_favourite else R.string.add_favourite))
            }
        }
    }
}

@Composable
fun CurrencyPicker(
    selected: String,
    currencies: List<String>,
    onSelected: (String) -> Unit,
    modifier: Modifier = Modifier,
) {
    var expanded by remember { mutableStateOf(false) }
    Column(modifier = modifier) {
        Button(
            onClick = { expanded = true },
            modifier = Modifier.heightIn(min = 48.dp).semantics { contentDescription = selected },
        ) {
            Text(selected)
            Icon(Icons.Filled.ArrowDropDown, contentDescription = null)
        }
        DropdownMenu(expanded = expanded, onDismissRequest = { expanded = false }) {
            currencies.forEach { currency ->
                DropdownMenuItem(
                    text = { Text(currency) },
                    onClick = {
                        onSelected(currency)
                        expanded = false
                    },
                )
            }
        }
    }
}

@Composable
fun AmountField(
    amount: String,
    onAmountChanged: (String) -> Unit,
    modifier: Modifier = Modifier,
) {
    OutlinedTextField(
        value = amount,
        onValueChange = { value ->
            if (value.isEmpty() || value.matches(Regex("^\\d*(\\.\\d*)?$"))) onAmountChanged(value)
        },
        modifier = modifier.fillMaxWidth().heightIn(min = 56.dp),
        label = { Text(stringResource(R.string.amount_label)) },
        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Decimal),
        singleLine = true,
    )
}

@Composable
fun SponsoredLabel(modifier: Modifier = Modifier) {
    val label = stringResource(R.string.sponsored_label)
    Text(
        text = label,
        modifier = modifier.semantics { contentDescription = label },
        style = MaterialTheme.typography.labelMedium,
    )
}

@Composable
fun EmptyState(
    message: String,
    modifier: Modifier = Modifier,
) {
    Text(
        text = message,
        modifier = modifier.fillMaxWidth().padding(24.dp),
        style = MaterialTheme.typography.bodyLarge,
    )
}

@Composable
fun ErrorState(
    message: String,
    onRetry: () -> Unit,
    modifier: Modifier = Modifier,
) {
    Column(modifier = modifier.fillMaxWidth().padding(24.dp), horizontalAlignment = Alignment.CenterHorizontally) {
        Text(message, color = MaterialTheme.colorScheme.error)
        Button(onClick = onRetry, modifier = Modifier.heightIn(min = 48.dp)) {
            Text(stringResource(R.string.retry))
        }
    }
}

@Composable
fun SkeletonLoader(modifier: Modifier = Modifier) {
    val loadingLabel = stringResource(R.string.loading)
    Row(
        modifier = modifier.fillMaxWidth().heightIn(min = 88.dp).padding(16.dp),
        horizontalArrangement = Arrangement.Center,
        verticalAlignment = Alignment.CenterVertically,
    ) {
        CircularProgressIndicator(
            modifier = Modifier.semantics { contentDescription = loadingLabel },
            strokeWidth = 3.dp,
        )
        Text(loadingLabel, modifier = Modifier.padding(start = 12.dp))
    }
}
