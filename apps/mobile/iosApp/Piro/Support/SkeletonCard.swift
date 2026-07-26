import SwiftUI

/// A single placeholder card that mirrors the shape of the real list rows (leading dot + two text lines
/// on a glass card) while their data loads, so the screen doesn't jump when content arrives. Reused by
/// Schedule, Alerts and Profile instead of a bare spinner. The bars pulse with a subtle opacity animation.
struct SkeletonCard: View {
    var lines: Int = 2
    @Environment(\.colorScheme) private var scheme
    @State private var pulse = false

    var body: some View {
        HStack(spacing: 12) {
            Circle().fill(fill).frame(width: 10, height: 10)
            VStack(alignment: .leading, spacing: 8) {
                bar(width: 160, height: 12)
                if lines > 1 { bar(width: 110, height: 10) }
                if lines > 2 { bar(width: 90, height: 10) }
            }
            Spacer()
        }
        .padding(12)
        .frame(maxWidth: .infinity, alignment: .leading)
        .glassCard(cornerRadius: 10)
        .opacity(pulse ? 0.55 : 1)
        .onAppear {
            withAnimation(.easeInOut(duration: 0.9).repeatForever(autoreverses: true)) { pulse = true }
        }
    }

    private var fill: Color { PiroColors.muted(scheme).opacity(0.25) }

    private func bar(width: CGFloat, height: CGFloat) -> some View {
        RoundedRectangle(cornerRadius: height / 2).fill(fill).frame(width: width, height: height)
    }
}

/// A stack of `SkeletonCard`s — the list-shaped loading state. `count` mirrors how many rows the real
/// list typically shows so the placeholder occupies a believable amount of space.
struct SkeletonList: View {
    var count: Int = 5
    var lines: Int = 2

    var body: some View {
        VStack(spacing: 8) {
            ForEach(0..<count, id: \.self) { _ in SkeletonCard(lines: lines) }
        }
    }
}
