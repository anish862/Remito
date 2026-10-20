package com.remito.core.common

import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertThrows
import org.junit.jupiter.api.Test

class CurrencyCodeTest {
    @Test
    fun `accepts supported ISO 4217 codes`() {
        assertEquals("OMR", CurrencyCode("OMR").value)
    }

    @Test
    fun `rejects invalid currency codes`() {
        assertThrows(IllegalArgumentException::class.java) { CurrencyCode("omr") }
        assertThrows(IllegalArgumentException::class.java) { CurrencyCode("XYZ") }
    }
}
