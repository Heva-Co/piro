import SwiftUI
import Shared

/// The signed-in shell: a Piro bottom-tab bar across On-call, Alerts, Schedule and Settings. Tapping an
/// alert (or opening a page notification / deep link) pushes the alert detail inside the Alerts tab.
/// Mirrors the Android `MainScaffold` + `MainActivity` routing.
struct RootView: View {
    @ObservedObject var session: SessionViewModel
    let services: ServiceLocator
    @ObservedObject private var router = DeepLinkRouter.shared
    @ObservedObject private var push = PushManager.shared

    @State private var selectedTab: MainTab = .onCall
    @State private var alertsPath: [Int] = []

    var body: some View {
        TabView(selection: $selectedTab) {
            NavigationStack {
                OnCallView(userName: displayName, readiness: push.readiness)
            }
            .tabItem { Label(MainTab.onCall.title, systemImage: MainTab.onCall.systemImage) }
            .tag(MainTab.onCall)

            NavigationStack(path: $alertsPath) {
                AlertsListView(api: services.api) { id in alertsPath.append(id) }
                    .navigationDestination(for: Int.self) { id in
                        AlertDetailView(api: services.api, alertId: id)
                    }
            }
            .tabItem { Label(MainTab.alerts.title, systemImage: MainTab.alerts.systemImage) }
            .tag(MainTab.alerts)

            NavigationStack {
                PlaceholderView(title: "Schedule",
                                message: "Your on-call rotation and shifts will appear here.")
            }
            .tabItem { Label(MainTab.schedule.title, systemImage: MainTab.schedule.systemImage) }
            .tag(MainTab.schedule)

            NavigationStack {
                ProfileView(api: services.api, onSignOut: session.signOut)
            }
            .tabItem { Label(MainTab.profile.title, systemImage: MainTab.profile.systemImage) }
            .tag(MainTab.profile)
        }
        .tint(PiroColors.brand)
        .onChange(of: router.pendingAlertId) { _, id in
            guard let id else { return }
            selectedTab = .alerts
            if alertsPath.last != id { alertsPath.append(id) }
            router.pendingAlertId = nil
        }
    }

    private var displayName: String {
        session.email.isEmpty ? "you" : session.email
    }
}
