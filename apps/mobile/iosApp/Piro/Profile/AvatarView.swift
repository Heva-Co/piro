import SwiftUI

/// The user's avatar: a colored circle with their initials, matching the admin app's rendering (avatar
/// color from the profile, initials = first letter of the first two words of the name). A real photo can
/// replace this later; initials are the fallback everywhere until then.
struct AvatarView: View {
    let name: String
    let colorHex: String?
    var size: CGFloat = 64

    var body: some View {
        Circle()
            .fill(Color.fromHex(colorHex))
            .frame(width: size, height: size)
            .overlay(
                Text(initials)
                    .font(.system(size: size * 0.4, weight: .semibold))
                    .foregroundStyle(.white)
            )
    }

    private var initials: String {
        let letters = name
            .split(separator: " ")
            .prefix(2)
            .compactMap { $0.first }
        let text = String(letters).uppercased()
        return text.isEmpty ? "?" : text
    }
}
