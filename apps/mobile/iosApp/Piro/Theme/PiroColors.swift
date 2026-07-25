import SwiftUI

/// Piro's brand palette, ported from the Android app's `PiroColors` (itself from the web/admin shadcn
/// "neutral" theme). The UI is monochrome/grayscale by design, with color reserved for meaning: the
/// brand blue flame (``brand``) is the one accent, and green/amber/red carry status semantics.
///
/// Surfaces are resolved through the environment color scheme so the app tracks light/dark like the
/// Android app's `PiroTheme` and Piro web's `system` default.
enum PiroColors {
    // Brand accent — the blue flame.
    static let brand = Color(hex: 0x3D96FE)

    // Status semantics (shared across themes).
    static let up = Color(hex: 0x22C55E)
    static let degraded = Color(hex: 0xF59E0B)
    static let down = Color(hex: 0xEF4444)
    static let identified = Color(hex: 0xF97316)
    static let criticalRed = Color(hex: 0xB91C1C)

    // Neutral surfaces — dark.
    static let backgroundDark = Color(hex: 0x252525)
    static let surfaceDark = Color(hex: 0x343434)
    static let surfaceVariantDark = Color(hex: 0x404040)
    static let onDark = Color(hex: 0xFBFBFB)
    static let mutedDark = Color(hex: 0xB4B4B4)

    // Neutral surfaces — light.
    static let backgroundLight = Color(hex: 0xFFFFFF)
    static let surfaceLight = Color(hex: 0xFFFFFF)
    static let surfaceVariantLight = Color(hex: 0xF6F6F6)
    static let onLight = Color(hex: 0x252525)
    static let mutedLight = Color(hex: 0x8D8D8D)

    static func background(_ scheme: ColorScheme) -> Color { scheme == .dark ? backgroundDark : backgroundLight }
    static func surface(_ scheme: ColorScheme) -> Color { scheme == .dark ? surfaceDark : surfaceLight }
    static func surfaceVariant(_ scheme: ColorScheme) -> Color { scheme == .dark ? surfaceVariantDark : surfaceVariantLight }
    static func onSurface(_ scheme: ColorScheme) -> Color { scheme == .dark ? onDark : onLight }
    static func muted(_ scheme: ColorScheme) -> Color { scheme == .dark ? mutedDark : mutedLight }
}

extension Color {
    /// Builds a `Color` from a 24-bit `0xRRGGBB` literal, so the palette above reads like the Android
    /// `Color(0xFF…)` constants it mirrors.
    init(hex: UInt32, opacity: Double = 1) {
        let r = Double((hex >> 16) & 0xFF) / 255
        let g = Double((hex >> 8) & 0xFF) / 255
        let b = Double(hex & 0xFF) / 255
        self.init(.sRGB, red: r, green: g, blue: b, opacity: opacity)
    }

    /// Parses a `#RRGGBB` / `RRGGBB` string (the format the API stores the profile avatar color in),
    /// falling back to the brand blue when it's empty or malformed.
    static func fromHex(_ string: String?) -> Color {
        guard var s = string?.trimmingCharacters(in: .whitespaces), !s.isEmpty else { return PiroColors.brand }
        if s.hasPrefix("#") { s.removeFirst() }
        guard s.count == 6, let value = UInt32(s, radix: 16) else { return PiroColors.brand }
        return Color(hex: value)
    }
}
