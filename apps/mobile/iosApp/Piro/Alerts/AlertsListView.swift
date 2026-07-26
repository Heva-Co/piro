import SwiftUI
import Shared

/// The Alerts tab: current active alerts as tappable cards, with a calm "all clear" empty state and
/// pull-to-refresh. Mirrors the Android `AlertsScreen`.
struct AlertsListView: View {
    @StateObject private var vm: AlertsViewModel
    let onOpenAlert: (Int) -> Void
    @Environment(\.colorScheme) private var scheme

    init(api: PiroApiClient, onOpenAlert: @escaping (Int) -> Void) {
        _vm = StateObject(wrappedValue: AlertsViewModel(api: api))
        self.onOpenAlert = onOpenAlert
    }

    var body: some View {
        PiroScreen(title: "Alerts") {
            content
                .task { await vm.refresh() }
                .refreshable { await vm.refresh() }
        }
    }

    @ViewBuilder private var content: some View {
        if vm.loading {
            ScrollView { SkeletonList(count: 5, lines: 3).padding(20) }
        } else if let error = vm.error {
            Text(error)
                .foregroundStyle(PiroColors.down)
                .frame(maxWidth: .infinity, maxHeight: .infinity)
        } else if vm.alerts.isEmpty {
            VStack(spacing: 8) {
                Image(systemName: "checkmark.shield")
                    .font(.system(size: 40))
                    .foregroundStyle(PiroColors.up)
                Text("All clear — no active alerts.")
                    .font(.headline)
                    .foregroundStyle(PiroColors.up)
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)
        } else {
            ScrollView {
                LazyVStack(spacing: 12) {
                    ForEach(vm.alerts, id: \.id) { alert in
                        Button {
                            onOpenAlert(Int(alert.id))
                        } label: {
                            AlertCardView(alert: alert)
                        }
                        .buttonStyle(.plain)
                    }
                }
                .padding(20)
            }
        }
    }
}
