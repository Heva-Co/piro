import SwiftUI

/// A read-only labelled value row (e.g. Email, Time zone, Roles) on the profile screen.
struct InfoRow: View {
    let label: String
    let value: String
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        HStack {
            Text(label)
                .font(.subheadline)
                .foregroundStyle(PiroColors.muted(scheme))
            Spacer(minLength: 12)
            Text(value)
                .font(.subheadline.weight(.medium))
                .foregroundStyle(PiroColors.onSurface(scheme))
                .multilineTextAlignment(.trailing)
        }
        .padding(.vertical, 10)
    }
}
