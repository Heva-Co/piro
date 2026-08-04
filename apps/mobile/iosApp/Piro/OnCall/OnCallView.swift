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
                    .animation(.easeInOut(duration: 0.25), value: readiness)
                Spacer()
                Spacer()
            }
            .padding(.horizontal, 24)
            .frame(maxWidth: .infinity, maxHeight: .infinity)
        }
    }

    @ViewBuilder
    private var statusBanner: some View {
        if readiness == .registering {
            // While registering, the app genuinely does not know whether this device will be paged, so
            // it shows a placeholder instead of a claim. The card keeps its shape and position, so the
            // layout does not jump when the real answer arrives a moment later.
            VStack(spacing: 8) {
                SkeletonBar(width: 220)
                SkeletonBar(width: 150)
            }
            .padding(16)
            .frame(maxWidth: .infinity)
            .glassCard(cornerRadius: 12, tint: PiroColors.muted(scheme).opacity(0.08))
            .accessibilityElement(children: .ignore)
            .accessibilityLabel("Arming this device to receive pages")
            // VoiceOver should hear progress rather than silence, since the skeleton conveys nothing
            // to a screen reader on its own.
            .accessibilityAddTraits(.updatesFrequently)
            .transition(.opacity)
        } else {
            Text(bannerText)
                .font(.callout.weight(.medium))
                .multilineTextAlignment(.center)
                .foregroundStyle(bannerColor)
                .padding(16)
                .frame(maxWidth: .infinity)
                .glassCard(cornerRadius: 12, tint: bannerColor.opacity(0.12))
                .transition(.opacity)
        }
    }

    /// The banner only promises pages when the device is actually registered with the backend — otherwise
    /// it tells the truth (permission needed, still registering, or registration failed).
    private var bannerText: String {
        switch readiness {
        case .registered: return "This device will receive critical pages, even on silent."
        // Not rendered: .registering shows the skeleton above. Kept so the switch stays exhaustive
        // and the string exists if the banner is ever reused somewhere without one.
        case .registering: return "Arming this device to receive pages…"
        case .needsPermission: return "Enable notifications so pages can reach you."
        case .failed: return "This device isn't registered for pages yet — you may not be paged."
        case .unsupported: return "Push isn't available in the iOS Simulator — run on a real device to receive pages."
        }
    }

    private var bannerColor: Color {
        switch readiness {
        case .registered: return PiroColors.up
        case .registering: return PiroColors.muted(scheme)
        case .needsPermission: return PiroColors.down
        case .failed: return PiroColors.degraded
        case .unsupported: return PiroColors.muted(scheme)
        }
    }
}

// Previews for every readiness state, because the real ones are hard to catch: registration completes
// in well under a second, so the skeleton would otherwise only ever be seen by accident.
#Preview("Registering (skeleton)") {
    OnCallView(userName: "aespinosa@heva.co", readiness: .registering)
}

#Preview("Registered") {
    OnCallView(userName: "aespinosa@heva.co", readiness: .registered)
}

#Preview("Needs permission") {
    OnCallView(userName: "aespinosa@heva.co", readiness: .needsPermission)
}

#Preview("Failed") {
    OnCallView(userName: "aespinosa@heva.co", readiness: .failed)
}
