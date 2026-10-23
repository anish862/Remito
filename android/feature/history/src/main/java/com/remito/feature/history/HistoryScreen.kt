package com.remito.feature.history

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.gestures.detectTapGestures
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.remito.core.common.Formatters
import com.remito.core.ui.EmptyState
import com.remito.core.ui.ErrorState
import com.remito.core.ui.SkeletonLoader
import java.math.BigDecimal
import java.util.Locale

private const val DISPLAY_DECIMAL_PLACES = 6

@Composable
@Suppress("LongMethod")
fun HistoryScreen(viewModel: HistoryViewModel = hiltViewModel()) {
    val state by viewModel.uiState.collectAsStateWithLifecycle()
    val locale = LocalConfiguration.current.locales[0] ?: Locale.getDefault()
    Column(
        modifier = Modifier.fillMaxSize().padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        Text(stringResource(R.string.history_title), style = MaterialTheme.typography.headlineMedium)
        Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
            HistoryRange.entries.forEach { range ->
                Button(
                    onClick = { viewModel.selectRange(range) },
                    modifier = Modifier.height(48.dp),
                ) {
                    Text(stringResource(range.label))
                }
            }
        }
        when (state) {
            HistoryUiState.Loading -> SkeletonLoader()
            is HistoryUiState.Content -> {
                val content = state as HistoryUiState.Content
                if (content.points.isEmpty()) {
                    EmptyState(stringResource(R.string.no_history))
                } else {
                    val summary = historySummary(content)
                    var selectedIndex by remember(content.points) { mutableIntStateOf(0) }
                    RateHistoryChart(
                        points = content.points.map { BigDecimal(it.rate) },
                        summary = summary,
                        selectedIndex = selectedIndex,
                        onSelectedIndex = { selectedIndex = it },
                        modifier = Modifier.fillMaxWidth().height(240.dp),
                    )
                    Text(summary)
                    Text(
                        stringResource(
                            R.string.selected_history_point,
                            content.points[selectedIndex.coerceIn(content.points.indices)].timestamp,
                            content.points[selectedIndex.coerceIn(content.points.indices)].rate,
                        ),
                    )
                    Text(
                        stringResource(
                            R.string.history_minimum,
                            Formatters.number(BigDecimal(content.minimum), locale, DISPLAY_DECIMAL_PLACES),
                        ),
                    )
                    Text(
                        stringResource(
                            R.string.history_maximum,
                            Formatters.number(BigDecimal(content.maximum), locale, DISPLAY_DECIMAL_PLACES),
                        ),
                    )
                    Text(
                        stringResource(
                            R.string.history_open,
                            Formatters.number(content.open, locale, DISPLAY_DECIMAL_PLACES),
                        ),
                    )
                    Text(
                        stringResource(
                            R.string.history_close,
                            Formatters.number(content.close, locale, DISPLAY_DECIMAL_PLACES),
                        ),
                    )
                    content.error?.let { ErrorState(it, onRetry = viewModel::refresh) }
                }
            }
            is HistoryUiState.Error ->
                ErrorState(
                    (state as HistoryUiState.Error).message
                        ?: stringResource(com.remito.core.ui.R.string.unexpected_error),
                    onRetry = viewModel::refresh,
                )
        }
    }
}

@Composable
private fun RateHistoryChart(
    points: List<BigDecimal>,
    summary: String,
    selectedIndex: Int,
    onSelectedIndex: (Int) -> Unit,
    modifier: Modifier = Modifier,
) {
    val lineColor = MaterialTheme.colorScheme.primary
    val selectedColor = MaterialTheme.colorScheme.error
    Canvas(
        modifier =
            modifier
                .semantics { contentDescription = summary }
                .pointerInput(points) {
                    detectTapGestures { position ->
                        onSelectedIndex((position.x / size.width * points.size).toInt().coerceIn(points.indices))
                    }
                },
    ) {
        if (points.isEmpty()) return@Canvas
        val values = points.map(BigDecimal::toFloat)
        val low = values.minOrNull() ?: 0f
        val high = values.maxOrNull() ?: low
        val spread = (high - low).takeIf { it > 0f } ?: 1f
        val stepX = size.width / (values.size - 1).coerceAtLeast(1)
        val path = Path()
        values.forEachIndexed { index, value ->
            val x = index * stepX
            val y = size.height - ((value - low) / spread * size.height)
            if (index == 0) path.moveTo(x, y) else path.lineTo(x, y)
        }
        drawPath(path, color = lineColor, style = Stroke(width = 4f))
        val inspected = values[selectedIndex.coerceIn(values.indices)]
        val selectedX = selectedIndex * stepX
        val selectedY = size.height - ((inspected - low) / spread * size.height)
        drawCircle(selectedColor, radius = 8f, center = Offset(selectedX, selectedY))
    }
}

@Composable
private fun historySummary(state: HistoryUiState.Content): String =
    stringResource(
        if (state.changePercent.signum() >= 0) R.string.history_summary_rose else R.string.history_summary_fell,
        state.pair.replace('-', ' '),
        Formatters.number(
            state.changePercent.abs(),
            LocalConfiguration.current.locales[0] ?: Locale.getDefault(),
            2,
        ),
        stringResource(state.range.periodLabel),
    )
