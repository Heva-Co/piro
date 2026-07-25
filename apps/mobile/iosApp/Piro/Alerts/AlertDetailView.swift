import SwiftUI
import Shared

/// The alert detail a page opens into: a severity banner, service/check/message metadata, and an
/// Acknowledge button that pauses escalation. Mirrors the Android `AlertDetailScreen`.
struct AlertDetailView: View {
    @StateObject private var vm: AlertDetailViewModel
    @Environment(\.colorScheme) private var scheme
    @Environment(\.dismiss) private var dismiss

    init(api: PiroApiClient, alertId: Int) {
        _vm = StateObject(wrappedValue: AlertDetailViewModel(api: api, alertId: alertId))
    }

    var body: some View {
        PiroScreen(title: "Alert", onBack: { dismiss() }) {
            ScrollView {
                VStack(alignment: .leading, spacing: 16) {
                    if vm.loading {
                        ProgressView().frame(maxWidth: .infinity).padding(.top, 48)
                    } else if let alert = vm.alert {
                        content(alert)
                    } else {
                        Text(vm.error ?? "Alert not found.")
                            .foregroundStyle(PiroColors.down)
                    }
                }
                .padding(20)
            }
            .task { await vm.load() }
        }
    }

    @ViewBuilder private func content(_ alert: AlertDetail) -> some View {
        banner(alert)

        if let message = alert.message, !message.isEmpty { field("Message", message) }
        if let impact = alert.impactAtFireTime, !impact.isEmpty { field("Impact", impact) }
        if let firedAt = alert.firedAt, !firedAt.isEmpty { field("Fired at", PiroDate.localDateTime(firedAt)) }
        field("Occurrences", String(alert.occurrenceCount))

        if alert.isAcknowledged {
            field("Acknowledged", "by \(alert.acknowledgedBy ?? "someone")")
        } else if alert.escalationExhaustedAt != nil {
            field("Escalation", "Halted — acknowledge to resume")
        }

        if !alert.isAcknowledged && !alert.isResolved {
            Button {
                Task { await vm.acknowledge() }
            } label: {
                Text(vm.acknowledging ? "Acknowledging…" : "Acknowledge")
                    .font(.headline)
                    .frame(maxWidth: .infinity)
                    .padding(.vertical, 14)
                    .background(PiroColors.brand, in: RoundedRectangle(cornerRadius: 12, style: .continuous))
                    .foregroundStyle(.white)
            }
            .disabled(vm.acknowledging)
            .padding(.top, 8)
        }

        if let error = vm.error {
            Text(error).foregroundStyle(PiroColors.down).font(.callout)
        }
    }

    private func banner(_ alert: AlertDetail) -> some View {
        let severity = alert.severity ?? "Alert"
        let isCritical = severity.caseInsensitiveCompare("Critical") == .orderedSame
        let color: Color = alert.isResolved ? PiroColors.up : (isCritical ? PiroColors.down : PiroColors.degraded)
        return VStack(alignment: .leading, spacing: 4) {
            Text(alert.isResolved ? "RESOLVED" : severity.uppercased())
                .font(.subheadline.weight(.bold))
                .foregroundStyle(.white)
            Text("\(alert.checkName ?? "Check") on \(alert.serviceName ?? "service")")
                .font(.title3.weight(.semibold))
                .foregroundStyle(.white)
        }
        .padding(16)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(color, in: RoundedRectangle(cornerRadius: 12, style: .continuous))
    }

    private func field(_ label: String, _ value: String) -> some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(label).font(.caption.weight(.medium)).foregroundStyle(PiroColors.muted(scheme))
            Text(value).font(.body).foregroundStyle(PiroColors.onSurface(scheme))
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }
}
