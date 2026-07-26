import SwiftUI

/// A translucent "glass" surface used for the app's cards and banners.
///
/// On iOS 26+ this uses the real **Liquid Glass** (`.glassEffect(_:in:)`). On earlier systems it falls
/// back to `Material` (`.ultraThinMaterial`), which gives a comparable blurred-translucency read — so
/// the app keeps a single deployment target (iOS 17) and progressively enhances on newer devices. The
/// effect is isolated here, so the rest of the UI just calls `.glassCard(...)`.
struct GlassBackground: ViewModifier {
    var cornerRadius: CGFloat = 16
    var tint: Color? = nil

    func body(content: Content) -> some View {
        if #available(iOS 26.0, *) {
            content.glassEffect(glass, in: .rect(cornerRadius: cornerRadius))
        } else {
            content.background(materialSurface)
        }
    }

    @available(iOS 26.0, *)
    private var glass: Glass {
        // A tinted status banner (up/down) layers its color into the glass; plain cards stay neutral.
        if let tint {
            return .regular.tint(tint)
        }
        return .regular
    }

    private var materialSurface: some View {
        ZStack {
            RoundedRectangle(cornerRadius: cornerRadius, style: .continuous)
                .fill(.ultraThinMaterial)
            if let tint {
                RoundedRectangle(cornerRadius: cornerRadius, style: .continuous)
                    .fill(tint)
            }
            RoundedRectangle(cornerRadius: cornerRadius, style: .continuous)
                .strokeBorder(Color.primary.opacity(0.08), lineWidth: 0.5)
        }
    }
}

extension View {
    /// Wraps the view in a Piro glass card. `tint` layers a translucent status color over the glass
    /// (used by the on-call and alert banners).
    func glassCard(cornerRadius: CGFloat = 16, tint: Color? = nil) -> some View {
        modifier(GlassBackground(cornerRadius: cornerRadius, tint: tint))
    }
}
