import Foundation
import Shared

/// Turns an error thrown across the KMP boundary into a clean, user-facing message — mirroring the
/// Android view models, which show the API's error title for a `PiroApiException` (e.g. "Invalid
/// credentials") and a friendly fallback for anything else (a transport/connection failure, whose raw
/// Ktor/NSURL description is never shown to the user).
///
/// A `@Throws` Kotlin call surfaces in Swift as an `NSError` whose `userInfo["KotlinException"]` holds
/// the original Kotlin exception instance, so we can recover the `PiroApiException` and its message.
enum PiroError {
    static func message(_ error: Error, networkFallback: String) -> String {
        if let api = apiException(error), let msg = api.message, !msg.isEmpty {
            return msg
        }
        return networkFallback
    }

    /// The `PiroApiException` behind this error, if the failure was an API (non-2xx) response rather than
    /// a transport error.
    static func apiException(_ error: Error) -> PiroApiException? {
        (error as NSError).userInfo["KotlinException"] as? PiroApiException
    }
}
