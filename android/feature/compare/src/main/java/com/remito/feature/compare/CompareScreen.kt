package com.remito.feature.compare

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FilterChip
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.runtime.staticCompositionLocalOf
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalLifecycleOwner
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.remito.core.network.ExchangeOfferDto
import com.remito.core.ui.CurrencyPicker
import com.remito.core.ui.EmptyState
import com.remito.core.ui.ErrorState
import com.remito.core.ui.SkeletonLoader
import com.remito.core.ui.StaleBanner
import kotlinx.coroutines.flow.collectLatest

internal val LocalRedirectDestinationLauncher =
    staticCompositionLocalOf<RedirectDestinationLauncher?> { null }

@Composable
@OptIn(ExperimentalMaterial3Api::class)
fun CompareScreen(viewModel: CompareViewModel = hiltViewModel()) {
    val state by viewModel.uiState.collectAsStateWithLifecycle()
    val content = state as? CompareUiState.Content ?: return
    val context = LocalContext.current
    val lifecycleOwner = LocalLifecycleOwner.current
    var lastRedirectOffer by remember { mutableStateOf<ExchangeOfferDto?>(null) }
    val launcher =
        LocalRedirectDestinationLauncher.current
            ?: remember(context) { AndroidRedirectDestinationLauncher(context) }
    val launchFailure = stringResource(R.string.compare_redirect_failed)

    DisposableEffect(lifecycleOwner, viewModel) {
        val observer =
            LifecycleEventObserver { _, event ->
                if (event == Lifecycle.Event.ON_RESUME) viewModel.onAppResumed()
            }
        lifecycleOwner.lifecycle.addObserver(observer)
        onDispose { lifecycleOwner.lifecycle.removeObserver(observer) }
    }

    LaunchedEffect(viewModel, launcher) {
        viewModel.effects.collectLatest { effect ->
            if (effect is CompareEffect.OpenRedirect) {
                val success = launcher.launchRedirect(effect.response, effect.hasApp)
                viewModel.onDestinationLaunched(success, launchFailure)
            }
        }
    }

    CompareScreenContent(
        content = content,
        viewModel = viewModel,
        retryOffer = lastRedirectOffer,
        onChoose = { offer ->
            lastRedirectOffer = offer
            viewModel.requestRedirect(offer)
        },
    )
}

@Composable
private fun CompareScreenContent(
    content: CompareUiState.Content,
    viewModel: CompareViewModel,
    retryOffer: ExchangeOfferDto?,
    onChoose: (ExchangeOfferDto) -> Unit,
) {
    val disclosure = content.disclaimer.ifBlank { stringResource(R.string.compare_disclaimer) }
    Column(
        modifier = Modifier.fillMaxSize().padding(horizontal = 16.dp, vertical = 12.dp),
        verticalArrangement = Arrangement.spacedBy(10.dp),
    ) {
        Text(stringResource(R.string.compare_title), style = MaterialTheme.typography.headlineMedium)
        CompareControls(content, viewModel)
        RedirectErrorMessage(content.redirectError, retryOffer, onChoose)
        content.favouriteError?.let { Text(it, color = MaterialTheme.colorScheme.error) }
        ComparisonResults(content, viewModel, onChoose)
        Text(
            text = disclosure,
            modifier = Modifier.fillMaxWidth().semantics { contentDescription = disclosure },
            style = MaterialTheme.typography.bodySmall,
        )
    }
    ComparisonBottomSheets(content, viewModel)
}

@Composable
private fun CompareControls(
    content: CompareUiState.Content,
    viewModel: CompareViewModel,
) {
    CorridorSelector(content, viewModel)
    OutlinedTextField(
        value = content.amount,
        onValueChange = { amount ->
            if (amount.isEmpty() || amount.matches(Regex("^\\d*(\\.\\d*)?$"))) viewModel.setAmount(amount)
        },
        modifier = Modifier.fillMaxWidth(),
        label = { Text(stringResource(R.string.compare_amount) + " (${content.from})") },
        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Decimal),
        supportingText = {
            if (!content.isAmountValid) Text(stringResource(R.string.compare_invalid_amount))
        },
        isError = !content.isAmountValid,
        singleLine = true,
    )
    CompareSortChips(content.sort, viewModel::setSort)
    OutlinedTextField(
        value = content.search,
        onValueChange = viewModel::setSearch,
        modifier = Modifier.fillMaxWidth(),
        label = { Text(stringResource(R.string.compare_search)) },
        singleLine = true,
    )
}

@Composable
private fun CorridorSelector(
    content: CompareUiState.Content,
    viewModel: CompareViewModel,
) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.spacedBy(8.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        CurrencyPicker(
            selected = content.from,
            currencies = content.currencies,
            onSelected = viewModel::setFrom,
            modifier = Modifier.weight(1f),
        )
        Text(stringResource(R.string.compare_to), style = MaterialTheme.typography.labelLarge)
        CurrencyPicker(
            selected = content.to,
            currencies = content.currencies,
            onSelected = viewModel::setTo,
            modifier = Modifier.weight(1f),
        )
    }
}

@Composable
private fun CompareSortChips(
    selected: CompareSort,
    onSelect: (CompareSort) -> Unit,
) {
    val bestAmountDescription = stringResource(R.string.compare_best_amount_description)
    val bestRateDescription = stringResource(R.string.compare_best_rate_description)
    val hasAppDescription = stringResource(R.string.compare_has_app_description)
    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
        FilterChip(
            selected = selected == CompareSort.BEST_AMOUNT,
            onClick = { onSelect(CompareSort.BEST_AMOUNT) },
            label = { Text(stringResource(R.string.compare_best_amount)) },
            modifier = Modifier.semantics { contentDescription = bestAmountDescription },
        )
        FilterChip(
            selected = selected == CompareSort.BEST_RATE,
            onClick = { onSelect(CompareSort.BEST_RATE) },
            label = { Text(stringResource(R.string.compare_best_rate)) },
            modifier = Modifier.semantics { contentDescription = bestRateDescription },
        )
        FilterChip(
            selected = selected == CompareSort.HAS_APP,
            onClick = {
                onSelect(if (selected == CompareSort.HAS_APP) CompareSort.BEST_AMOUNT else CompareSort.HAS_APP)
            },
            label = { Text(stringResource(R.string.compare_has_app)) },
            modifier = Modifier.semantics { contentDescription = hasAppDescription },
        )
    }
}

@Composable
private fun RedirectErrorMessage(
    message: String?,
    retryOffer: ExchangeOfferDto?,
    onChoose: (ExchangeOfferDto) -> Unit,
) {
    message ?: return
    Row(verticalAlignment = Alignment.CenterVertically) {
        Text(message, color = MaterialTheme.colorScheme.error, modifier = Modifier.weight(1f))
        retryOffer?.let { offer ->
            TextButton(onClick = { onChoose(offer) }) {
                Text(stringResource(R.string.compare_retry_redirect))
            }
        }
    }
}

@Composable
private fun ColumnScope.ComparisonResults(
    content: CompareUiState.Content,
    viewModel: CompareViewModel,
    onChoose: (ExchangeOfferDto) -> Unit,
) {
    if (content.allOffersStale) StaleBanner()
    when {
        content.error != null && content.offers.isNotEmpty() ->
            Text(content.error, color = MaterialTheme.colorScheme.error)
        content.error != null ->
            ErrorState(
                content.error.ifBlank { stringResource(R.string.compare_load_failed) },
                onRetry = viewModel::refresh,
                modifier = Modifier.weight(1f),
            )
        !content.hasValidQuery ->
            EmptyState(stringResource(R.string.compare_invalid_amount), modifier = Modifier.weight(1f))
        content.isLoading && content.visibleOffers.isEmpty() ->
            SkeletonLoader(modifier = Modifier.weight(1f))
        content.visibleOffers.isEmpty() ->
            EmptyState(stringResource(R.string.compare_no_exchanges), modifier = Modifier.weight(1f))
        else -> {
            if (content.isLoading) {
                CircularProgressIndicator(modifier = Modifier.align(Alignment.CenterHorizontally))
            }
            LazyColumn(
                modifier = Modifier.weight(1f),
                verticalArrangement = Arrangement.spacedBy(10.dp),
            ) {
                items(content.visibleOffers, key = ExchangeOfferDto::id) { offer ->
                    ExchangeOfferCard(
                        offer = offer,
                        from = content.from,
                        to = content.to,
                        isFavourite = offer.id in content.favouriteExchangeIds,
                        actions =
                            ExchangeOfferActions(
                                onFavouriteClick = { viewModel.toggleExchangeFavourite(offer.id) },
                                onDetails = { viewModel.showDetails(offer) },
                                onChoose = { onChoose(offer) },
                            ),
                    )
                }
            }
        }
    }
}

@Composable
@OptIn(ExperimentalMaterial3Api::class)
private fun ComparisonBottomSheets(
    content: CompareUiState.Content,
    viewModel: CompareViewModel,
) {
    content.selectedDetails?.let { offer ->
        ModalBottomSheet(onDismissRequest = viewModel::dismissDetails) {
            ExchangeDetailsSheet(offer, content.from, content.to, content.amount)
        }
    }
    content.confirmationOffer?.let { offer ->
        ModalBottomSheet(onDismissRequest = viewModel::dismissConfirmation) {
            RedirectConfirmationSheet(
                RedirectConfirmationState(
                    offer = offer,
                    isLoading = content.isRedirecting,
                    amount = content.amount,
                    error = content.redirectError,
                    onCancel = viewModel::dismissConfirmation,
                    onConfirm = viewModel::confirmRedirect,
                ),
            )
        }
    }
}
