import SwiftUI
import Shared

/// One notification-delivery preference: the integration it goes through, the handle it reaches, and
/// whether it's verified / the account fallback. Read-only for now (managing channels — add, verify,
/// reorder — is a later flow).
struct NotificationPreferenceRow: View {
    let preference: UserNotificationPreferenceDto
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        HStack(spacing: 12) {
            Image(systemName: "bell.badge")
                .font(.body)
                .foregroundStyle(PiroColors.brand)
                .frame(width: 28)

            VStack(alignment: .leading, spacing: 2) {
                Text(preference.integrationName ?? preference.integrationId)
                    .font(.subheadline.weight(.medium))
                    .foregroundStyle(PiroColors.onSurface(scheme))
                Text(preference.handle)
                    .font(.footnote)
                    .foregroundStyle(PiroColors.muted(scheme))
                    .lineLimit(1)
            }

            Spacer(minLength: 8)

            VStack(alignment: .trailing, spacing: 4) {
                badge(preference.isVerified ? "Verified" : "Unverified",
                      color: preference.isVerified ? PiroColors.up : PiroColors.degraded)
                if preference.isAccountFallback {
                    badge("Fallback", color: PiroColors.muted(scheme))
                }
            }
        }
        .padding(14)
        .glassCard(cornerRadius: 12)
    }

    private func badge(_ text: String, color: Color) -> some View {
        Text(text.uppercased())
            .font(.caption2.weight(.bold))
            .foregroundStyle(color)
            .padding(.horizontal, 8)
            .padding(.vertical, 3)
            .background(color.opacity(0.12), in: Capsule())
    }
}
