import SwiftUI

/// A branded placeholder for tabs not yet built (Schedule), and the frame for Settings. Mirrors the
/// Android `PlaceholderScreen`.
struct PlaceholderView: View {
    let title: String
    let message: String
    var actionLabel: String? = nil
    var action: (() -> Void)? = nil
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        PiroScreen(title: title) {
            VStack(spacing: 12) {
                Spacer()
                PiroFlame(size: 48, color: PiroColors.muted(scheme))
                Text(message)
                    .font(.callout)
                    .multilineTextAlignment(.center)
                    .foregroundStyle(PiroColors.muted(scheme))
                if let actionLabel, let action {
                    Button(actionLabel, action: action)
                        .font(.headline)
                        .padding(.vertical, 12)
                        .padding(.horizontal, 24)
                        .overlay(RoundedRectangle(cornerRadius: 12, style: .continuous)
                            .strokeBorder(PiroColors.muted(scheme).opacity(0.4)))
                        .foregroundStyle(PiroColors.down)
                        .padding(.top, 8)
                }
                Spacer()
                Spacer()
            }
            .padding(.horizontal, 24)
            .frame(maxWidth: .infinity, maxHeight: .infinity)
        }
    }
}
