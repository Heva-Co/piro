package co.heva.piro.shared.model

import kotlinx.serialization.Serializable

/** Paged envelope of GET /api/v1/alerts (AlertPageDto). */
@Serializable
data class AlertListEnvelope(
    val items: List<AlertDetail> = emptyList(),
    val totalCount: Int = 0,
)

/**
 * Alert detail (AlertDetailDto) returned by GET /api/v1/alerts/{id} and POST .../acknowledge. Only the
 * fields the mobile detail screen renders are mapped; unknown fields are ignored by the JSON config.
 */
@Serializable
data class AlertDetail(
    val id: Int,
    val checkName: String? = null,
    val serviceName: String? = null,
    val message: String? = null,
    val severity: String? = null,
    val impactAtFireTime: String? = null,
    val alertValue: String? = null,
    val firedAt: String? = null,
    val resolvedAt: String? = null,
    val occurrenceCount: Int = 0,
    val acknowledgedAt: String? = null,
    val acknowledgedBy: String? = null,
    val escalationExhaustedAt: String? = null,
    val sourceUrl: String? = null,
) {
    val isResolved: Boolean get() = resolvedAt != null
    val isAcknowledged: Boolean get() = acknowledgedAt != null
}
