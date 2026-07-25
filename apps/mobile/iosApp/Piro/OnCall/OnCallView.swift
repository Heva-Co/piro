import SwiftUI

/// The On-call home: a Piro-branded status card confirming the user is on call and this device is armed
/// to receive critical pages. Mirrors the Android `OnCallScreen`.
struct OnCallView: View {
    let userName: String
    let readiness: PushReadiness
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        PiroScreen(title: "On-call") {
            VStack(spacing: 0) {
                Spacer()
                PiroFlame(size: 64)
                Text("You're on call")
                    .font(.title.weight(.bold))
                    .foregroundStyle(PiroColors.onSurface(scheme))
                    .padding(.top, 20)
                Text("Signed in as \(userName)")
                    .font(.callout)
                    .foregroundStyle(PiroColors.muted(scheme))
                    .padding(.top, 6)

                statusBanner
                    .padding(.top, 28)
                Spacer()
                Spacer()
            }
            .padding(.horizontal, 24)
            .frame(maxWidth: .infinity, maxHeight: .infinity)
        }
    }

    private var statusBanner: some View {
        Text(bannerText)
            .font(.callout.weight(.medium))
            .multilineTextAlignment(.center)
            .foregroundStyle(bannerColor)
            .padding(16)
            .frame(maxWidth: .infinity)
            .glassCard(cornerRadius: 12, tint: bannerColor.opacity(0.12))
    }

    /// The banner only promises pages when the device is actually registered with the backend — otherwise
    /// it tells the truth (permission needed, still registering, or registration failed).
    private var bannerText: String {
        switch readiness {
        case .registered: return "This device will receive critical pages, even on silent."
        case .registering: return "Arming this device to receive pages…"
        case .needsPermission: return "Enable notifications so pages can reach you."
        case .failed: return "This device isn't registered for pages yet — you may not be paged."
        }
    }

    private var bannerColor: Color {
        switch readiness {
        case .registered: return PiroColors.up
        case .registering: return PiroColors.muted(scheme)
        case .needsPermission: return PiroColors.down
        case .failed: return PiroColors.degraded
        }
    }
}
