import Foundation
import Combine

/// Carries a requested navigation target (an alert to open) from outside the view tree — a tapped push
/// notification or a `piro://alert/{id}` deep link — into `RootView`, which observes it and pushes the
/// alert detail. The Android equivalent is `MainActivity.openAlertId`.
@MainActor
final class DeepLinkRouter: ObservableObject {
    static let shared = DeepLinkRouter()

    /// The alert to present. `RootView` clears it once consumed so the same page can be reopened later.
    @Published var pendingAlertId: Int?

    func openAlert(id: Int) {
        pendingAlertId = id
    }
}
