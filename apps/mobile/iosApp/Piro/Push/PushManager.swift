import Foundation
import UIKit
import UserNotifications
import Shared

/// Owns the push lifecycle: asks for notification permission, registers with APNs, and associates this
/// device's APNs token with the signed-in user via `POST /api/v1/devices` (platform `Ios`) — the iOS
/// counterpart to Android's `DeviceRegistrar` + `PiroMessagingService`.
///
/// Registration is best-effort and idempotent on the backend: a login triggers it, and a token that
/// only arrives from APNs afterwards registers as soon as it lands. On the Simulator a token may never
/// arrive (no APNs) — that is fine and never blocks login, exactly like the Android flow.
/// Whether this device will actually receive pages — the honest state the on-call screen shows, rather
/// than just "was notification permission granted?". A device is only truly armed once its APNs token is
/// registered with the backend; if that fails (e.g. no APNs token on the Simulator, or the API rejects
/// the registration), the UI must not promise pages it can't deliver.
enum PushReadiness: Equatable {
    case needsPermission   // the user hasn't allowed notifications
    case registering       // permission granted; waiting on the APNs token / backend registration
    case registered        // token registered with the backend — pages will arrive
    case failed            // permission granted but registration didn't complete — pages won't arrive
}

@MainActor
final class PushManager: NSObject, ObservableObject {
    static let shared = PushManager()

    @Published private(set) var readiness: PushReadiness = .needsPermission

    private var api: PiroApiClient?
    private var apnsToken: String?
    private var isSignedIn = false
    private var notificationsGranted = false

    /// Set once at app start so callbacks can register the device.
    func configure(api: PiroApiClient) {
        self.api = api
    }

    /// Requests notification authorization (alert + sound + badge) and, if granted, registers for remote
    /// notifications so `didRegisterForRemoteNotifications` delivers the APNs token.
    func requestAuthorizationAndRegister() {
        UNUserNotificationCenter.current().delegate = self
        UNUserNotificationCenter.current().requestAuthorization(options: [.alert, .sound, .badge]) { [weak self] granted, _ in
            Task { @MainActor in
                guard let self else { return }
                self.notificationsGranted = granted
                if granted {
                    if self.readiness != .registered { self.readiness = .registering }
                    UIApplication.shared.registerForRemoteNotifications()
                } else {
                    self.readiness = .needsPermission
                }
            }
        }
    }

    /// Refreshes the grant state (e.g. after returning from Settings) and re-arms if needed.
    func refreshAuthorizationState() {
        UNUserNotificationCenter.current().getNotificationSettings { [weak self] settings in
            Task { @MainActor in
                guard let self else { return }
                let granted = settings.authorizationStatus == .authorized ||
                    settings.authorizationStatus == .provisional
                self.notificationsGranted = granted
                if !granted {
                    self.readiness = .needsPermission
                } else if self.readiness == .needsPermission {
                    self.readiness = .registering
                    UIApplication.shared.registerForRemoteNotifications()
                }
            }
        }
    }

    // MARK: - Session hooks

    func onSignedIn() {
        isSignedIn = true
        registerCurrentDevice()
    }

    func onSignedOut() {
        isSignedIn = false
    }

    // MARK: - APNs callbacks (from the app delegate)

    func didRegister(tokenData: Data) {
        apnsToken = tokenData.map { String(format: "%02x", $0) }.joined()
        registerCurrentDevice()
    }

    /// APNs registration itself failed (common on the Simulator, which has no APNs) — no token will
    /// arrive, so the device can't be paged.
    func didFailToRegisterAPNs() {
        if notificationsGranted { readiness = .failed }
    }

    /// Registers the device once we have both an APNs token and a signed-in session; reflects the outcome
    /// in `readiness` so the on-call screen tells the truth. Called from the login hook and the token
    /// callback, whichever completes the pair last.
    private func registerCurrentDevice() {
        guard isSignedIn, notificationsGranted else { return }
        guard let token = apnsToken, let api else { return } // token not here yet → stay `.registering`
        let name = UIDevice.current.name
        Task {
            do {
                _ = try await api.registerDevice(platform: "Ios", token: token, deviceName: name)
                readiness = .registered
            } catch {
                readiness = .failed
            }
        }
    }
}

extension PushManager: UNUserNotificationCenterDelegate {
    /// Show pages while the app is foregrounded (banner + sound), matching the Android critical channel's
    /// intent that a page is never silently swallowed.
    func userNotificationCenter(
        _ center: UNUserNotificationCenter,
        willPresent notification: UNNotification,
        withCompletionHandler completionHandler: @escaping (UNNotificationPresentationOptions) -> Void
    ) {
        completionHandler([.banner, .sound, .list])
    }

    /// Tapping a page routes to its alert detail via the piro://alert/{id} deep link.
    func userNotificationCenter(
        _ center: UNUserNotificationCenter,
        didReceive response: UNNotificationResponse,
        withCompletionHandler completionHandler: @escaping () -> Void
    ) {
        let info = response.notification.request.content.userInfo
        if let alertId = (info["alertId"] as? String).flatMap(Int.init) ?? info["alertId"] as? Int {
            Task { @MainActor in DeepLinkRouter.shared.openAlert(id: alertId) }
        }
        completionHandler()
    }
}
