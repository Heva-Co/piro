import SwiftUI
import UIKit
import Shared

/// App entry point. Wires the single `ServiceLocator`, the session, and push, then shows the login flow
/// until signed in and the tab shell after. Mirrors the Android `PiroApp` + `MainActivity`.
@main
struct PiroApp: App {
    @UIApplicationDelegateAdaptor(AppDelegate.self) private var appDelegate
    private let services: ServiceLocator
    @StateObject private var session: SessionViewModel

    init() {
        let services = ServiceLocator()
        PushManager.shared.configure(api: services.api)
        self.services = services
        _session = StateObject(wrappedValue: SessionViewModel(
            services: services,
            push: PushManager.shared
        ))
    }

    var body: some Scene {
        WindowGroup {
            ContentView(session: session, services: services)
                .preferredColorScheme(nil) // follow the system light/dark setting
        }
    }
}

/// Switches between the login screen and the signed-in shell, restores the session once on launch, and
/// routes `piro://alert/{id}` deep links to the alert detail.
private struct ContentView: View {
    @ObservedObject var session: SessionViewModel
    let services: ServiceLocator
    @State private var didStart = false

    var body: some View {
        Group {
            if session.signedIn {
                RootView(session: session, services: services)
            } else {
                LoginView(session: session)
            }
        }
        .onAppear {
            guard !didStart else { return }
            didStart = true
            session.onAppear()
            PushManager.shared.requestAuthorizationAndRegister()
        }
        .onOpenURL { url in handle(url) }
    }

    private func handle(_ url: URL) {
        guard url.scheme == "piro" else { return }
        // SSO (piro://oauth/callback) is captured by ASWebAuthenticationSession; here we only route pages.
        if url.host == "alert", let id = Int(url.lastPathComponent) {
            DeepLinkRouter.shared.openAlert(id: id)
        }
    }
}

/// Bridges UIKit-only callbacks (the APNs device token) into the app. SwiftUI has no scene hook for
/// remote-notification registration, so this thin delegate forwards the token to `PushManager`.
final class AppDelegate: NSObject, UIApplicationDelegate {
    func application(
        _ application: UIApplication,
        didRegisterForRemoteNotificationsWithDeviceToken deviceToken: Data
    ) {
        Task { @MainActor in PushManager.shared.didRegister(tokenData: deviceToken) }
    }

    func application(
        _ application: UIApplication,
        didFailToRegisterForRemoteNotificationsWithError error: Error
    ) {
        // On the Simulator (no APNs) registration fails — surface it so the on-call screen shows the
        // device isn't armed rather than falsely promising pages. Login is unaffected.
        Task { @MainActor in PushManager.shared.didFailToRegisterAPNs() }
    }
}
