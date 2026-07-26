import SwiftUI

/// The one screen chrome every view uses, so headers are identical everywhere: a large, bold, left-
/// aligned title, an optional back chevron (for pushed screens), an optional trailing accessory, and the
/// app background. It hides the native navigation bar and draws its own header, which keeps the header
/// look consistent across tab roots and pushed screens (the native large/inline titles were mismatched).
struct PiroScreen<Content: View>: View {
    let title: String
    var onBack: (() -> Void)? = nil
    var trailing: AnyView? = nil
    @ViewBuilder let content: () -> Content

    @Environment(\.colorScheme) private var scheme

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            header
            // Note: don't force the content's height here — a ScrollView given `maxHeight: .infinity`
            // grows to its content and stops scrolling. Scrollable content fills naturally; centered
            // screens (On-call, placeholders) request their own fill.
            content()
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
        .background(PiroColors.background(scheme).ignoresSafeArea())
        .navigationBarBackButtonHidden(true)
        .toolbar(.hidden, for: .navigationBar)
    }

    private var header: some View {
        HStack(spacing: 10) {
            if let onBack {
                Button(action: onBack) {
                    Image(systemName: "chevron.left")
                        .font(.title3.weight(.semibold))
                        .foregroundStyle(PiroColors.brand)
                }
            }
            Text(title)
                .font(.largeTitle.bold())
                .foregroundStyle(PiroColors.onSurface(scheme))
            Spacer(minLength: 8)
            trailing
        }
        .padding(.horizontal, 20)
        .padding(.top, 4)
        .padding(.bottom, 12)
    }
}
