import Foundation
import Shared

/// Loads one alert's detail and acknowledges it — the SwiftUI counterpart of the Android
/// `AlertDetailViewModel`.
@MainActor
final class AlertDetailViewModel: ObservableObject {
    @Published var loading = true
    @Published var alert: AlertDetail?
    @Published var error: String?
    @Published var acknowledging = false

    private let api: PiroApiClient
    private let alertId: Int32

    init(api: PiroApiClient, alertId: Int) {
        self.api = api
        self.alertId = Int32(alertId)
    }

    func load() async {
        loading = true
        error = nil
        do {
            alert = try await api.getAlert(id: alertId)
        } catch {
            self.error = "Could not load the alert."
        }
        loading = false
    }

    func acknowledge() async {
        guard !acknowledging else { return }
        acknowledging = true
        error = nil
        do {
            alert = try await api.acknowledgeAlert(id: alertId)
        } catch {
            self.error = "Acknowledge failed."
        }
        acknowledging = false
    }
}
