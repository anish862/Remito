package com.remito.core.common

import java.util.Currency
import java.util.Locale

private const val ISO_CURRENCY_CODE_LENGTH = 3

@JvmInline
value class CurrencyCode(
    val value: String,
) {
    init {
        require(value.length == ISO_CURRENCY_CODE_LENGTH && value == value.uppercase(Locale.ROOT)) {
            "Currency codes must be uppercase ISO 4217 codes."
        }
        Currency.getInstance(value)
    }
}
