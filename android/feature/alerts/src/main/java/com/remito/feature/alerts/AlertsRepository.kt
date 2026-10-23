package com.remito.feature.alerts

import com.remito.core.database.AlertEntity
import com.remito.core.database.RateDao
import com.remito.core.network.AlertDto
import com.remito.core.network.AlertRequest
import com.remito.core.network.ProblemDetailsMapper
import com.remito.core.network.RemitoApi
import java.io.IOException
import java.math.BigDecimal
import javax.inject.Inject

class AlertsRepository
    @Inject
    constructor(
        private val api: RemitoApi,
        private val dao: RateDao,
    ) {
        private val problemDetailsMapper = ProblemDetailsMapper()

        fun observeAlerts() = dao.observeAlerts()

        suspend fun refresh(): List<AlertDto> {
            val response = api.alerts()
            if (!response.isSuccessful) throw failure(response)
            val alerts = response.body() ?: throw IOException("The alerts response was empty.")
            dao.clearAlerts()
            dao.upsertAlerts(alerts.map(::toEntity))
            return alerts
        }

        suspend fun create(
            pair: String,
            direction: com.remito.core.network.AlertDirection,
            target: BigDecimal,
            isOneTime: Boolean,
        ) {
            val response = api.createAlert(AlertRequest(pair, direction, target, isOneTime = isOneTime))
            if (!response.isSuccessful) throw failure(response)
            val alert = response.body() ?: throw IOException("The created alert response was empty.")
            dao.upsertAlerts(listOf(toEntity(alert)))
        }

        suspend fun setActive(
            alert: AlertEntity,
            active: Boolean,
        ) {
            val request = alert.toRequest(isActive = active)
            val response = api.updateAlert(alert.id, request)
            if (!response.isSuccessful) throw failure(response)
            val updated = response.body() ?: throw IOException("The updated alert response was empty.")
            dao.upsertAlerts(listOf(toEntity(updated)))
        }

        suspend fun delete(alert: AlertEntity) {
            val response = api.deleteAlert(alert.id)
            if (!response.isSuccessful) throw failure(response)
            dao.deleteAlert(alert.id)
        }

        private fun toEntity(alert: AlertDto) =
            AlertEntity(
                id = alert.id,
                pair = alert.pair,
                direction = alert.direction.name.lowercase(),
                target = alert.target.toPlainString(),
                isActive = alert.isActive,
                isOneTime = alert.isOneTime,
                dailySummary = alert.dailySummary,
            )

        private fun AlertEntity.toRequest(isActive: Boolean) =
            AlertRequest(
                pair = pair,
                direction =
                    com.remito.core.network.AlertDirection
                        .valueOf(direction.uppercase()),
                target = target.toBigDecimal(),
                isActive = isActive,
                isOneTime = isOneTime,
                dailySummary = dailySummary,
            )

        private fun failure(response: retrofit2.Response<*>): IOException {
            val problem = problemDetailsMapper.map(response)
            return IOException(problem.detail?.takeIf(String::isNotBlank) ?: problem.title)
        }
    }
