import SwiftUI

/// One profile field rendered as a label above a Liquid Glass box — the single structure every field on
/// the Profile screen uses, editable (the display-name TextField) or read-only (email, time zone, roles).
/// The box is real Liquid Glass on iOS 26 (via `.glassCard`), with a Material fallback on older systems.
struct ProfileField<Content: View>: View {
    let label: String
    @ViewBuilder let content: () -> Content
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(label.uppercased())
                .font(.caption.weight(.bold))
                .foregroundStyle(PiroColors.muted(scheme))
            content()
                .frame(maxWidth: .infinity, alignment: .leading)
                .padding(14)
                .glassCard(cornerRadius: 12)
        }
    }
}

extension ProfileField where Content == Text {
    /// Convenience for a read-only value field (email, time zone, roles…), styled like the name field.
    init(_ label: String, value: String) {
        self.label = label
        self.content = {
            Text(value).font(.body).foregroundStyle(Color.primary)
        }
    }
}
