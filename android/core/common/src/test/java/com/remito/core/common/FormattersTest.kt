package com.remito.core.common

import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test
import java.math.BigDecimal
import java.time.Instant
import java.util.Locale

class FormattersTest {
    @Test
    fun numberUsesIndianGroupingForEnIn() {
        assertEquals(
            "12,34,567.89",
            Formatters.number(BigDecimal("1234567.89"), Locale.forLanguageTag("en-IN")),
        )
    }

    @Test
    fun currencyAndRelativeTimeSupportEnOm() {
        val locale = Locale.forLanguageTag("en-OM")
        val formattedMoney = Formatters.money(Money(BigDecimal("12.345"), CurrencyCode("OMR")), locale)

        assertTrue(formattedMoney.contains("12"))
        assertEquals(
            "2 hours ago",
            Formatters.relativeTime(
                timestamp = Instant.parse("2025-01-01T00:00:00Z"),
                now = Instant.parse("2025-01-01T02:00:00Z"),
                locale = locale,
                templates =
                    RelativeTimeTemplates(
                        "just now",
                        "%1\$s minute ago",
                        "%1\$s minutes ago",
                        "%1\$s hour ago",
                        "%1\$s hours ago",
                        "%1\$s day ago",
                        "%1\$s days ago",
                    ),
            ),
        )
    }
}
