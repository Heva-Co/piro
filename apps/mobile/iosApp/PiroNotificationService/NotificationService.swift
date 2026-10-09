import UserNotifications

/// Decrypts a sealed push before iOS shows it (RFC 0017).
///
/// The extension exists because the payload arrives encrypted: the app process is not running when a
/// notification lands, so nothing else gets a chance to touch it. APNs sets `mutable-content: 1` and
/// hands the notification here first; whatever this returns is what the user sees.
///
/// The private key comes from the shared Keychain access group, which is why the app and this target
/// must sit in the same App Group. Without that this process generates its own keypair and can never
/// decrypt anything the backend sealed for the app.
final class NotificationService: UNNotificationServiceExtension {

    private var contentHandler: ((UNNotificationContent) -> Void)?
    private var bestAttempt: UNMutableNotificationContent?

    override func didReceive(
        _ request: UNNotificationRequest,
        withContentHandler contentHandler: @escaping (UNNotificationContent) -> Void
    ) {
        self.contentHandler = contentHandler
        let content = request.content.mutableCopy() as? UNMutableNotificationContent
        bestAttempt = content

        guard let content else {
            contentHandler(request.content)
            return
        }

        // No ciphertext means a legacy cleartext push (a device registered before it published a key):
        // the title and body are already correct, so pass it through untouched.
        guard let envelope = content.userInfo["ciphertext"] as? String else {
            contentHandler(content)
            return
        }

        do {
            let payload = try PushPayloadUnsealer.unseal(envelope)

            content.title = payload.title
            content.body = payload.body

            // Rebuild the userInfo the app expects on tap. The deep-link router reads `url`, so it has
            // to survive decryption or tapping the notification stops opening the right alert.
            var info = content.userInfo
            info["eventKey"] = payload.eventKey
            info["alertId"] = payload.alertId
            if let url = payload.url { info["url"] = url }
            info.removeValue(forKey: "ciphertext")
            content.userInfo = info

            contentHandler(content)
        } catch {
            // Showing the placeholder beats showing nothing: a page the user cannot read still tells
            // them to open the app, whereas swallowing it loses the page entirely. The reason is worth
            // logging, since a decrypt failure here means the device's key and the server's copy have
            // diverged — usually a reinstall that re-keyed without re-registering.
            content.body = "Open Piro to view this alert."
            contentHandler(content)
        }
    }

    /// iOS gives the extension a few seconds. If it runs out, this fires and whatever has been built so
    /// far is delivered — hence keeping `bestAttempt` up to date rather than only mutating at the end.
    override func serviceExtensionTimeWillExpire() {
        if let contentHandler, let bestAttempt {
            contentHandler(bestAttempt)
        }
    }
}
