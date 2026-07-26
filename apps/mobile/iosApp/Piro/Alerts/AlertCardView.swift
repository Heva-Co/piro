import SwiftUI
import Shared

/// One alert row in the Alerts list, styled after the Android `AlertCard`: a status dot + severity, the
/// check/service title, and an optional message. The whole card is tappable (the list wraps it in a
/// Button), so there's no separate "View details" affordance.
struct AlertCardView: View {
    let alert: AlertDetail
    @Environment(\.colorScheme) private var scheme

    private var dotColor: Color {
        if alert.isResolved { return PiroColors.up }
        if alert.severity?.caseInsensitiveCompare("Critical") == .orderedSame { return PiroColors.down }
        return PiroColors.degraded
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack(spacing: 6) {
                Circle().fill(dotColor).frame(width: 10, height: 10)
                Text((alert.severity ?? "Alert").uppercased())
                    .font(.caption.weight(.bold))
                    .foregroundStyle(dotColor)
                if alert.isAcknowledged {
                    Text("• ACK")
                        .font(.caption2)
                        .foregroundStyle(PiroColors.muted(scheme))
                }
            }
            Text("\(alert.checkName ?? "Check") on \(alert.serviceName ?? "service")")
                .font(.headline)
                .foregroundStyle(PiroColors.onSurface(scheme))
            if let message = alert.message, !message.isEmpty {
                Text(message)
                    .font(.footnote)
                    .foregroundStyle(PiroColors.muted(scheme))
                    .lineLimit(2)
            }
        }
        .padding(16)
        .frame(maxWidth: .infinity, alignment: .leading)
        .glassCard(cornerRadius: 12)
    }
}
