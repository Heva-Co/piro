plugins {
    alias(libs.plugins.kotlinMultiplatform)
    // AGP 9's KMP-native library plugin (replaces com.android.library for multiplatform modules).
    alias(libs.plugins.androidKotlinMultiplatformLibrary)
    alias(libs.plugins.kotlinSerialization)
    // Generates the API wire models from the backend's OpenAPI spec (the Kotlin analogue of the admin
    // app's `generate:api-types`). See the openApiGenerate config at the bottom of this file.
    alias(libs.plugins.openapiGenerator)
}

kotlin {
    jvmToolchain(17)

    android {
        namespace = "co.heva.piro.shared"
        compileSdk = libs.versions.compileSdk.get().toInt()
        minSdk = libs.versions.minSdk.get().toInt()
    }

    listOf(iosArm64(), iosSimulatorArm64()).forEach { iosTarget ->
        iosTarget.binaries.framework {
            baseName = "Shared"
            isStatic = true
        }
    }

    sourceSets {
        commonMain.dependencies {
            implementation(libs.ktor.client.core)
            implementation(libs.ktor.client.content.negotiation)
            implementation(libs.ktor.serialization.kotlinx.json)
            implementation(libs.ktor.client.logging)
            implementation(libs.kotlinx.serialization.json)
            implementation(libs.kotlinx.coroutines.core)
        }
        androidMain.dependencies {
            implementation(libs.ktor.client.okhttp)
            implementation(libs.androidx.security.crypto)
        }
        iosMain.dependencies {
            implementation(libs.ktor.client.darwin)
        }

        // Compile the generated wire models (committed under generated/) as part of commonMain.
        commonMain {
            kotlin.srcDir("generated/src/commonMain/kotlin")
        }
    }
}

/**
 * Generates `@Serializable` Kotlin wire models from the backend's OpenAPI document — the Kotlin analogue
 * of `apps/admin`'s `pnpm run generate:api-types`. Run manually after the API contract changes:
 *
 *   1. Refresh the spec snapshot:  curl -s http://localhost:5117/openapi/v1.json -o shared/openapi/piro-openapi.v1.json
 *   2. Regenerate:                 ./gradlew :shared:openApiGenerate
 *
 * Output is committed under `generated/`, so ordinary builds never need the API running. Models land in
 * `co.heva.piro.shared.model.generated`; the app migrates onto them incrementally (auth/OIDC already
 * match, so hand-written models in `model/` stay until each is swapped over). `dateLibrary=string` keeps
 * date-time fields as `String` (commonMain-safe — no `java.time`).
 */
openApiGenerate {
    generatorName.set("kotlin")
    inputSpec.set("$projectDir/openapi/piro-openapi.v1.json")
    outputDir.set("$projectDir/generated")
    modelPackage.set("co.heva.piro.shared.model.generated")
    // Generate only the models the mobile app consumes (plus their enum dependencies), not the whole
    // 199-schema surface — the unused ones pull in `java.*` types (BigDecimal/URI) that don't exist in
    // commonMain. Add to this list as the app starts calling more endpoints.
    globalProperties.set(
        mapOf(
            "models" to listOf(
                "AlertPageDto", "AlertSummaryDto", "AlertDetailDto",
                "ServiceStatus", "AlertSeverity", "AlertSource",
                "UserProfileDto", "SignInResponse", "UserDto",
                "OidcProviderInfo", "SignInRequest", "OidcCallbackRequest",
                "UpdateProfileRequest", "UserNotificationPreferenceDto",
            ).joinToString(","),
            "modelDocs" to "false",
        ),
    )
    additionalProperties.set(
        mapOf(
            "serializationLibrary" to "kotlinx_serialization",
            "dateLibrary" to "string",
            "sourceFolder" to "src/commonMain/kotlin",
        ),
    )
    // `uuid` fields default to `java.util.UUID` (not available in commonMain) — treat them as String,
    // like `dateLibrary=string` does for date-time.
    typeMappings.set(mapOf("UUID" to "kotlin.String"))
    generateModelTests.set(false)
    generateModelDocumentation.set(false)
    generateApiTests.set(false)
    generateApiDocumentation.set(false)
}
