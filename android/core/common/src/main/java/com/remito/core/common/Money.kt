package com.remito.core.common

import java.math.BigDecimal

data class Money(
    val amount: BigDecimal,
    val currency: CurrencyCode,
)

@JvmInline
value class Rate(
    val value: BigDecimal,
) {
    init {
        require(value.signum() > 0) { "A rate must be greater than zero." }
    }
}
