import AuthenticationServices
import UIKit

/// Runs the SSO/OIDC round-trip with `ASWebAuthenticationSession`: opens the provider's `/oidc/start`
/// URL in a secure in-app browser and waits for the `piro://oauth/callback?code=…&state=…` redirect,
/// returning the parsed code+state. This is the iOS analogue of the Android Custom Tab + deep-link flow.
@MainActor
final class SSOAuthenticator: NSObject, ASWebAuthenticationPresentationContextProviding {
    private var session: ASWebAuthenticationSession?

    struct Callback {
        let code: String
        let state: String
    }

    func start(url: URL) async throws -> Callback {
        try await withCheckedThrowingContinuation { continuation in
            let session = ASWebAuthenticationSession(
                url: url,
                callbackURLScheme: "piro"
            ) { callbackURL, error in
                if let error {
                    continuation.resume(throwing: error)
                    return
                }
                guard
                    let callbackURL,
                    let components = URLComponents(url: callbackURL, resolvingAgainstBaseURL: false),
                    let code = components.queryItems?.first(where: { $0.name == "code" })?.value,
                    let state = components.queryItems?.first(where: { $0.name == "state" })?.value
                else {
                    continuation.resume(throwing: SSOError.missingParameters)
                    return
                }
                continuation.resume(returning: Callback(code: code, state: state))
            }
            session.presentationContextProvider = self
            session.prefersEphemeralWebBrowserSession = false
            self.session = session
            session.start()
        }
    }

    enum SSOError: Error { case missingParameters }

    func presentationAnchor(for session: ASWebAuthenticationSession) -> ASPresentationAnchor {
        UIApplication.shared.connectedScenes
            .compactMap { $0 as? UIWindowScene }
            .flatMap { $0.windows }
            .first { $0.isKeyWindow } ?? ASPresentationAnchor()
    }
}
