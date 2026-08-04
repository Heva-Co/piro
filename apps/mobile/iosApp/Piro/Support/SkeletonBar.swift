import SwiftUI

/// A placeholder bar that pulses while real content is being determined.
///
/// Used instead of showing a verdict the app does not have yet: the On-call banner states whether this
/// device will be paged, and asserting that before registration completes would be a promise the app
/// cannot keep. A pulsing bar says "still working" without claiming anything.
struct SkeletonBar: View {
    var width: CGFloat? = nil
    var height: CGFloat = 14

    @Environment(\.colorScheme) private var scheme
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    @State private var pulsing = false

    var body: some View {
        RoundedRectangle(cornerRadius: height / 2, style: .continuous)
            .fill(PiroColors.muted(scheme).opacity(pulsing ? 0.28 : 0.14))
            .frame(width: width, height: height)
            .onAppear {
                // Reduce Motion turns the animation off but keeps the shape: the placeholder still
                // reads as "not content yet", which is the part that matters.
                guard !reduceMotion else { return }
                withAnimation(.easeInOut(duration: 0.9).repeatForever(autoreverses: true)) {
                    pulsing = true
                }
            }
    }
}
