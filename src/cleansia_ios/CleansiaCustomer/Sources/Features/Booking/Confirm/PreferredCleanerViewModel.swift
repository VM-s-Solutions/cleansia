import CleansiaCore
import Foundation

@MainActor
final class PreferredCleanerViewModel: ViewModel {
    @Published private(set) var isPlus = false
    @Published private(set) var cleaners: [ServingCleaner] = []
    @Published private(set) var cancellationPolicy = CancellationPolicyBuilder.make(membership: nil)

    private let cleanersClient: ServingCleanersClient
    private var loaded = false

    init(cleanersClient: ServingCleanersClient = LiveServingCleanersClient()) {
        self.cleanersClient = cleanersClient
        super.init()
    }

    var isVisible: Bool {
        isPlus && !cleaners.isEmpty
    }

    func load(membership: MembershipSnapshot?) async {
        if loaded { return }
        loaded = true
        guard let membership else { return }
        cancellationPolicy = CancellationPolicyBuilder.make(membership: membership)
        guard membership.hasMembership else { return }
        isPlus = true
        if case let .success(list) = await cleanersClient.myServingCleaners() {
            cleaners = list
        }
    }
}
