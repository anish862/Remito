package com.remito.feature.news

import android.net.Uri
import androidx.browser.customtabs.CustomTabsIntent
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.remito.core.ui.EmptyState
import com.remito.core.ui.ErrorState
import com.remito.core.ui.SkeletonLoader

@Composable
fun NewsScreen(
    viewModel: NewsViewModel = hiltViewModel(),
    articleSlug: String? = null,
) {
    val state by viewModel.uiState.collectAsStateWithLifecycle()
    val error = state.error
    if (articleSlug != null && state.selectedSlug != articleSlug) viewModel.openArticle(articleSlug)
    val context = LocalContext.current
    val article = state.selected
    Column(
        modifier = Modifier.fillMaxSize().padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        if (article != null) {
            TextButton(onClick = viewModel::closeArticle) { Text(stringResource(R.string.news_back)) }
            Text(article.title, style = MaterialTheme.typography.headlineSmall)
            Text(article.summary, style = MaterialTheme.typography.titleMedium)
            Text(article.body, style = MaterialTheme.typography.bodyLarge)
            val source = article.sourceUrl?.let(::safeHttpsUri)
            if (source != null) {
                Button(onClick = { CustomTabsIntent.Builder().build().launchUrl(context, source) }) {
                    Text(stringResource(R.string.news_read_source))
                }
            } else {
                Text(stringResource(R.string.news_source_unavailable), color = MaterialTheme.colorScheme.error)
            }
        } else {
            Text(stringResource(R.string.news_title), style = MaterialTheme.typography.headlineMedium)
            when {
                state.isLoading && state.items.isEmpty() -> SkeletonLoader()
                state.items.isEmpty() && error != null ->
                    ErrorState(error, onRetry = viewModel::refresh)
                state.items.isEmpty() -> EmptyState(stringResource(R.string.news_empty))
                else -> {
                    state.error?.let { Text(it, color = MaterialTheme.colorScheme.error) }
                    LazyColumn(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                        items(state.items, key = { it.slug }) { item ->
                            Card(modifier = Modifier.fillMaxWidth().clickable { viewModel.openArticle(item.slug) }) {
                                Column(
                                    modifier = Modifier.fillMaxWidth().padding(16.dp),
                                    verticalArrangement = Arrangement.spacedBy(6.dp),
                                ) {
                                    Text(item.title, style = MaterialTheme.typography.titleMedium)
                                    Text(item.summary, style = MaterialTheme.typography.bodyMedium)
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}

private fun safeHttpsUri(url: String): Uri? =
    try {
        Uri.parse(url).takeIf { it.scheme == "https" && !it.host.isNullOrBlank() && it.userInfo == null }
    } catch (_: IllegalArgumentException) {
        null
    }
