package co.heva.piro.shared.api

import io.ktor.client.HttpClient

/** Provides the platform HTTP engine (OkHttp on Android, Darwin on iOS) for the shared API client. */
expect fun platformHttpClient(): HttpClient
