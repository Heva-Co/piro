package co.heva.piro.shared.model

import kotlinx.serialization.Serializable

/** Push platform sent to POST /api/v1/devices. Serialized as its name ("Android"/"Ios"), which the API accepts. */
enum class DevicePlatform { Android, Ios }

/** Body of POST /api/v1/devices (RegisterDeviceRequest). */
@Serializable
data class RegisterDeviceRequest(
    val platform: String,
    val token: String,
    val deviceName: String? = null,
)

/** Response of POST /api/v1/devices and each item of GET /api/v1/devices (DeviceDto). */
@Serializable
data class DeviceDto(
    val id: Int,
    val platform: String,
    val deviceName: String? = null,
    val lastSeenAt: String,
)
