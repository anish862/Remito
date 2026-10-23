package com.remito.core.common

import java.math.BigDecimal
import java.text.DecimalFormat
import java.text.NumberFormat
import java.time.Duration
import java.time.Instant
import java.util.Currency
import java.util.Locale

object Formatters {
    private const val SECONDS_PER_MINUTE = 60
    private const val MINUTES_PER_HOUR = 60
    private const val HOURS_PER_DAY = 24
    private const val INDIAN_PRIMARY_GROUP_DIGITS = 3
    private const val INDIAN_SECONDARY_GROUP_DIGITS = 2

    fun number(
        value: BigDecimal,
        locale: Locale,
        maximumFractionDigits: Int = 2,
    ): String {
        val formatter =
            NumberFormat
                .getNumberInstance(locale)
                .apply {
                    minimumFractionDigits = 0
                    this.maximumFractionDigits = maximumFractionDigits
                }
        val decimalFormat = if (locale.country == "IN") formatter as? DecimalFormat else null
        return if (decimalFormat == null) {
            formatter.format(value)
        } else {
            val symbols = decimalFormat.decimalFormatSymbols
            decimalFormat.isGroupingUsed = false
            val formatted = decimalFormat.format(value)
            val decimalIndex = formatted.lastIndexOf(symbols.decimalSeparator)
            val integerPart = if (decimalIndex < 0) formatted else formatted.substring(0, decimalIndex)
            val fractionPart = if (decimalIndex < 0) "" else formatted.substring(decimalIndex)
            val sign = if (integerPart.startsWith(symbols.minusSign)) symbols.minusSign.toString() else ""
            val digits = integerPart.removePrefix(sign)
            sign + groupIndianDigits(digits, symbols.groupingSeparator) + fractionPart
        }
    }

    fun money(
        money: Money,
        locale: Locale,
    ): String =
        NumberFormat
            .getCurrencyInstance(locale)
            .apply {
                currency = Currency.getInstance(money.currency.value)
            }.format(money.amount)

    fun relativeTime(
        timestamp: Instant,
        now: Instant = Instant.now(),
        locale: Locale,
        templates: RelativeTimeTemplates,
    ): String {
        val seconds = Duration.between(timestamp, now).seconds.coerceAtLeast(0)
        val minutes = seconds / SECONDS_PER_MINUTE
        val hours = minutes / MINUTES_PER_HOUR
        val days = hours / HOURS_PER_DAY
        return when {
            seconds < SECONDS_PER_MINUTE -> templates.justNow
            minutes < MINUTES_PER_HOUR ->
                String.format(
                    locale,
                    if (minutes == 1L) templates.minuteSingular else templates.minutePlural,
                    number(BigDecimal.valueOf(minutes), locale, maximumFractionDigits = 0),
                )
            hours < HOURS_PER_DAY ->
                String.format(
                    locale,
                    if (hours == 1L) templates.hourSingular else templates.hourPlural,
                    number(BigDecimal.valueOf(hours), locale, maximumFractionDigits = 0),
                )
            else ->
                String.format(
                    locale,
                    if (days == 1L) templates.daySingular else templates.dayPlural,
                    number(BigDecimal.valueOf(days), locale, maximumFractionDigits = 0),
                )
        }
    }

    private fun groupIndianDigits(
        digits: String,
        separator: Char,
    ): String {
        if (digits.length <= INDIAN_PRIMARY_GROUP_DIGITS) return digits
        val groups = mutableListOf(digits.takeLast(INDIAN_PRIMARY_GROUP_DIGITS))
        var endIndex = digits.length - INDIAN_PRIMARY_GROUP_DIGITS
        while (endIndex > 0) {
            val startIndex = (endIndex - INDIAN_SECONDARY_GROUP_DIGITS).coerceAtLeast(0)
            groups += digits.substring(startIndex, endIndex)
            endIndex = startIndex
        }
        return groups.asReversed().joinToString(separator.toString())
    }
}

data class RelativeTimeTemplates(
    val justNow: String,
    val minuteSingular: String,
    val minutePlural: String,
    val hourSingular: String,
    val hourPlural: String,
    val daySingular: String,
    val dayPlural: String,
)
