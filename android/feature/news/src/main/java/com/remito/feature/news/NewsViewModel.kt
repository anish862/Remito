package com.remito.feature.news

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.remito.core.database.CachedNewsEntity
import com.remito.core.network.EngagementRepository
import com.remito.core.network.NewsItemDto
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.launch
import java.io.IOException
import javax.inject.Inject

data class NewsUiState(
    val items: List<CachedNewsEntity> = emptyList(),
    val selected: NewsItemDto? = null,
    val selectedSlug: String? = null,
    val isLoading: Boolean = true,
    val error: String? = null,
)

@HiltViewModel
class NewsViewModel
    @Inject
    constructor(
        private val repository: EngagementRepository,
        savedStateHandle: SavedStateHandle,
    ) : ViewModel() {
        private val initialSlug: String? = savedStateHandle["slug"]
        private val _uiState = MutableStateFlow(NewsUiState(selectedSlug = initialSlug))
        val uiState: StateFlow<NewsUiState> = _uiState

        init {
            viewModelScope.launch {
                repository.observeNews().collect { items ->
                    _uiState.value = _uiState.value.copy(items = items, isLoading = false)
                }
            }
            refresh()
            initialSlug?.let(::openArticle)
        }

        fun refresh() {
            viewModelScope.launch {
                try {
                    repository.refreshNews()
                    _uiState.value = _uiState.value.copy(isLoading = false, error = null)
                } catch (error: CancellationException) {
                    throw error
                } catch (error: IOException) {
                    _uiState.value =
                        _uiState.value.copy(
                            isLoading = false,
                            error = error.message ?: "Could not load news.",
                        )
                }
            }
        }

        fun openArticle(slug: String) {
            _uiState.value = _uiState.value.copy(selectedSlug = slug, selected = null)
            viewModelScope.launch {
                try {
                    val article = repository.getNews(slug)
                    _uiState.value = _uiState.value.copy(selected = article, error = null)
                } catch (error: CancellationException) {
                    throw error
                } catch (error: IOException) {
                    _uiState.value =
                        _uiState.value.copy(
                            selected = null,
                            error = error.message ?: "Could not load this article.",
                        )
                }
            }
        }

        fun closeArticle() {
            _uiState.value = _uiState.value.copy(selected = null, selectedSlug = null)
        }
    }
