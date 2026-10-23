package com.remito.feature.favourites

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.remito.core.ui.EmptyState
import com.remito.core.ui.ErrorState
import com.remito.core.ui.RateCard
import com.remito.core.ui.SkeletonLoader
import java.math.BigDecimal
import java.time.Instant
import java.util.Locale

@Composable
fun FavouritesScreen(viewModel: FavouritesViewModel = hiltViewModel()) {
    val state by viewModel.uiState.collectAsStateWithLifecycle()
    val locale = androidx.compose.ui.platform.LocalConfiguration.current.locales[0] ?: Locale.getDefault()
    Column(
        modifier = Modifier.fillMaxSize().padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        Text(stringResource(R.string.favourites_title), style = MaterialTheme.typography.headlineMedium)
        Button(onClick = viewModel::refresh) { Text(stringResource(R.string.favourites_refresh)) }
        state.error?.let { ErrorState(it, onRetry = viewModel::refresh) }
        when {
            state.isLoading && state.rates.isEmpty() -> SkeletonLoader()
            state.rates.isEmpty() -> EmptyState(stringResource(R.string.favourites_empty))
            else ->
                LazyColumn(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    items(state.rates, key = { it.pair }) { rate ->
                        RateCard(
                            currency = rate.pair.substringAfter('/'),
                            rate = rate.rate,
                            changePercent = BigDecimal(rate.change24HoursPercent),
                            lastUpdated =
                                parseTimestamp(rate.lastUpdated)?.let {
                                    com.remito.core.ui
                                        .relativeTimeText(it, locale)
                                } ?: rate.lastUpdated,
                            isFavourite = true,
                            onFavouriteClick = { viewModel.removeCurrency(rate.pair.substringAfter('/')) },
                        )
                    }
                }
        }
    }
}

private fun parseTimestamp(value: String): Instant? =
    try {
        Instant.parse(value)
    } catch (_: java.time.format.DateTimeParseException) {
        null
    }
