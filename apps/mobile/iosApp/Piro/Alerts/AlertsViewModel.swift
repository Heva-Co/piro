import Foundation
import Shared

/// Loads the active alerts for the Alerts tab — the SwiftUI counterpart of the Android `AlertsViewModel`.
@MainActor
final class AlertsViewModel: ObservableObject {
    @Published var loading = true
    @Published var alerts: [AlertDetail] = []
    @Published var error: String?

    private let api: PiroApiClient

    init(api: PiroApiClient) {
        self.api = api
    }

    func refresh() async {
        loading = true
        error = nil
        do {
            alerts = try await api.getAlerts()
        } catch {
            self.error = PiroError.message(error, networkFallback: "Could not load alerts.")
        }
        loading = false
    }
}
