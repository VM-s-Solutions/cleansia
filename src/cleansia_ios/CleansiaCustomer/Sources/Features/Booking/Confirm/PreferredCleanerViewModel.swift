import CleansiaCore
import Foundation

@MainActor
final class PreferredCleanerViewModel: ViewModel {
    @Published private(set) var isPlus = false
    @Published private(set) var cleaners: [ServingCleaner] = []
    @Published private(set) var cancellationPolicy = CancellationPolicyBuilder.make(membership: nil)

    private let membershipClient: MembershipClient
    private let cleanersClient: ServingCleanersClient
    private var loaded = false

    init(
        membershipClient: MembershipClient = LiveMembershipClient(),
        cleanersClient: ServingCleanersClient = LiveServingCleanersClient()
    ) {
        self.membershipClient = membershipClient
        self.cleanersClient = cleanersClient
        super.init()
    }

    var isVisible: Bool {
        isPlus && !cleaners.isEmpty
    }

    func load() async {
        if loaded { return }
        loaded = true
        guard case let .success(membership) = await membershipClient.currentMembership() else {
            return
        }
        cancellationPolicy = CancellationPolicyBuilder.make(membership: membership)
        guard membership.hasMembership else { return }
        isPlus = true
        if case let .success(list) = await cleanersClient.myServingCleaners() {
            cleaners = list
        }
    }
}
