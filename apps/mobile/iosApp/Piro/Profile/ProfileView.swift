import SwiftUI
import Shared

/// The Profile tab: avatar (initials + color), name/email, editable display name and avatar color,
/// account info, notification preferences, and sign out. Replaces the old Settings placeholder.
struct ProfileView: View {
    @StateObject private var vm: ProfileViewModel
    let onSignOut: () -> Void
    @Environment(\.colorScheme) private var scheme

    init(api: PiroApiClient, onSignOut: @escaping () -> Void) {
        _vm = StateObject(wrappedValue: ProfileViewModel(api: api))
        self.onSignOut = onSignOut
    }

    var body: some View {
        PiroScreen(title: "Profile") {
            ScrollView {
                if vm.loading {
                    ProgressView().frame(maxWidth: .infinity).padding(.top, 60)
                } else {
                    VStack(alignment: .leading, spacing: 20) {
                        identityHeader
                        fields
                        if vm.isDirty { saveButton }
                        notificationsSection
                        signOutButton
                        if let error = vm.error {
                            Text(error).font(.callout).foregroundStyle(PiroColors.down)
                        }
                    }
                    .padding(20)
                }
            }
        }
        .task { await vm.load() }
    }

    // MARK: - Sections

    private var identityHeader: some View {
        HStack(spacing: 16) {
            AvatarView(name: vm.name, colorHex: vm.colorHex, size: 64)
            VStack(alignment: .leading, spacing: 3) {
                Text(vm.name.isEmpty ? "—" : vm.name)
                    .font(.title3.weight(.semibold))
                    .foregroundStyle(PiroColors.onSurface(scheme))
                if let email = vm.profile?.email {
                    Text(email)
                        .font(.subheadline)
                        .foregroundStyle(PiroColors.muted(scheme))
                }
            }
            Spacer()
        }
    }

    /// Every field shares the same structure: a label above a Liquid Glass box. Display name is editable;
    /// the rest are read-only values presented identically.
    @ViewBuilder private var fields: some View {
        ProfileField(label: "Display name") {
            TextField("Your name", text: $vm.name)
                .textInputAutocapitalization(.words)
        }
        if let profile = vm.profile {
            ProfileField("Email", value: profile.email)
            ProfileField("Time zone", value: profile.timeZone)
            if !profile.roles.isEmpty {
                ProfileField("Roles", value: profile.roles.joined(separator: ", "))
            }
            if profile.isOidc {
                ProfileField("Sign-in", value: "Single sign-on")
            }
        }
    }

    private var saveButton: some View {
        Button {
            Task { await vm.save() }
        } label: {
            Text(vm.saving ? "Saving…" : "Save changes")
                .font(.headline)
                .frame(maxWidth: .infinity)
                .padding(.vertical, 14)
                .background(PiroColors.brand, in: RoundedRectangle(cornerRadius: 12, style: .continuous))
                .foregroundStyle(.white)
        }
        .disabled(vm.saving)
    }

    private var notificationsSection: some View {
        VStack(alignment: .leading, spacing: 12) {
            sectionTitle("Notification preferences")
            if vm.preferences.isEmpty {
                Text("No notification channels configured yet.")
                    .font(.footnote)
                    .foregroundStyle(PiroColors.muted(scheme))
            } else {
                ForEach(vm.preferences, id: \.id) { preference in
                    NotificationPreferenceRow(preference: preference)
                }
            }
        }
    }

    private var signOutButton: some View {
        Button(role: .destructive, action: onSignOut) {
            Text("Sign out")
                .font(.headline)
                .frame(maxWidth: .infinity)
                .padding(.vertical, 14)
                .overlay(RoundedRectangle(cornerRadius: 12, style: .continuous)
                    .strokeBorder(PiroColors.down.opacity(0.5)))
                .foregroundStyle(PiroColors.down)
        }
        .padding(.top, 8)
    }

    private func sectionTitle(_ text: String) -> some View {
        Text(text.uppercased())
            .font(.caption.weight(.bold))
            .foregroundStyle(PiroColors.muted(scheme))
    }
}
