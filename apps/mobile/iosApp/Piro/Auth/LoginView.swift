import SwiftUI
import Shared

/// The sign-in screen: email/password unless the server is SSO-only, plus a button per enabled SSO
/// provider. Mirrors the Android `LoginScreen`, styled with the Piro flame and brand palette.
struct LoginView: View {
    @ObservedObject var session: SessionViewModel
    @Environment(\.colorScheme) private var scheme
    @FocusState private var focus: Field?
    @State private var authenticator = SSOAuthenticator()

    private enum Field { case server, email, password }

    var body: some View {
        ScrollView {
            VStack(spacing: 0) {
                PiroFlame(size: 56)
                    .padding(.bottom, 16)
                Text("Piro")
                    .font(.largeTitle.weight(.bold))
                    .foregroundStyle(PiroColors.onSurface(scheme))
                Text("On-call")
                    .font(.headline)
                    .foregroundStyle(PiroColors.muted(scheme))

                VStack(spacing: 12) {
                    serverField
                    if !session.ssoOnly {
                        credentialFields
                        signInButton
                    }
                    if !session.providers.isEmpty {
                        if !session.ssoOnly { orDivider }
                        ForEach(session.providers, id: \.id) { provider in
                            ssoButton(provider)
                        }
                    }
                    if session.isSubmitting {
                        ProgressView().padding(.top, 8)
                    }
                    if let error = session.error {
                        Text(error)
                            .font(.callout)
                            .foregroundStyle(PiroColors.down)
                            .frame(maxWidth: .infinity, alignment: .leading)
                            .padding(.top, 4)
                    }
                }
                .padding(.top, 32)
            }
            .padding(24)
            .frame(maxWidth: .infinity)
        }
        .background(PiroColors.background(scheme).ignoresSafeArea())
    }

    private var serverField: some View {
        VStack(alignment: .leading, spacing: 4) {
            TextField("Server URL (https://piro.example.com)", text: $session.serverURL)
                .textContentType(.URL)
                .keyboardType(.URL)
                .textInputAutocapitalization(.never)
                .autocorrectionDisabled()
                .focused($focus, equals: .server)
                .submitLabel(.next)
                .onSubmit {
                    session.applyServer()
                    focus = session.ssoOnly ? nil : .email
                }
                .fieldStyle(scheme)
                .disabled(session.isSubmitting)
            Text("The address of your self-hosted Piro server.")
                .font(.caption2)
                .foregroundStyle(PiroColors.muted(scheme))
        }
    }

    private var credentialFields: some View {
        VStack(spacing: 12) {
            TextField("Email", text: $session.email)
                .textContentType(.username)
                .keyboardType(.emailAddress)
                .textInputAutocapitalization(.never)
                .autocorrectionDisabled()
                .focused($focus, equals: .email)
                .submitLabel(.next)
                .onSubmit { focus = .password }
                .fieldStyle(scheme)

            SecureField("Password", text: $session.password)
                .textContentType(.password)
                .focused($focus, equals: .password)
                .submitLabel(.go)
                .onSubmit { session.signIn() }
                .fieldStyle(scheme)
        }
        .disabled(session.isSubmitting)
    }

    private var signInButton: some View {
        Button {
            focus = nil
            session.signIn()
        } label: {
            Text("Sign in")
                .font(.headline)
                .frame(maxWidth: .infinity)
                .padding(.vertical, 14)
                .background(PiroColors.brand, in: RoundedRectangle(cornerRadius: 12, style: .continuous))
                .foregroundStyle(.white)
        }
        .disabled(session.isSubmitting)
    }

    private func ssoButton(_ provider: OidcProvider) -> some View {
        Button {
            startSSO(provider)
        } label: {
            Text(provider.displayName.isEmpty ? provider.id : provider.displayName)
                .font(.headline)
                .frame(maxWidth: .infinity)
                .padding(.vertical, 14)
                .overlay(RoundedRectangle(cornerRadius: 12, style: .continuous)
                    .strokeBorder(PiroColors.muted(scheme).opacity(0.4)))
                .foregroundStyle(PiroColors.onSurface(scheme))
        }
        .disabled(session.isSubmitting)
    }

    private var orDivider: some View {
        HStack {
            Rectangle().fill(PiroColors.muted(scheme).opacity(0.3)).frame(height: 0.5)
            Text("Or continue with").font(.footnote).foregroundStyle(PiroColors.muted(scheme))
            Rectangle().fill(PiroColors.muted(scheme).opacity(0.3)).frame(height: 0.5)
        }
        .padding(.vertical, 4)
    }

    private func startSSO(_ provider: OidcProvider) {
        guard let url = session.oidcStartURL(provider) else { return }
        Task {
            do {
                let callback = try await authenticator.start(url: url)
                session.completeSso(code: callback.code, state: callback.state)
            } catch {
                // A user-cancelled SSO sheet is not an error worth surfacing.
            }
        }
    }
}

/// Shared text-field chrome for the login inputs.
private extension View {
    func fieldStyle(_ scheme: ColorScheme) -> some View {
        self
            .padding(14)
            .background(PiroColors.surfaceVariant(scheme), in: RoundedRectangle(cornerRadius: 12, style: .continuous))
            .overlay(RoundedRectangle(cornerRadius: 12, style: .continuous)
                .strokeBorder(PiroColors.muted(scheme).opacity(0.25)))
    }
}
