import SwiftUI

/// The iOS 18 zoom presentation, behind the one `#available` gate its callers share: a thumbnail (or
/// button) is the source, the presented content the destination, and the content grows out of the
/// source and shrinks back into it — with swipe-down to dismiss for free. Before iOS 18 both are no-ops
/// and the presentation stays the plain one it was.
extension View {
    /// A `nil` namespace leaves the view unchanged, for a host that presents nothing from it.
    @ViewBuilder
    func zoomSource(id: some Hashable, in namespace: Namespace.ID?) -> some View {
        if #available(iOS 18, *), let namespace {
            matchedTransitionSource(id: id, in: namespace)
        } else {
            self
        }
    }

    @ViewBuilder
    func zoomDestination(id: some Hashable, in namespace: Namespace.ID) -> some View {
        if #available(iOS 18, *) {
            navigationTransition(.zoom(sourceID: id, in: namespace))
        } else {
            self
        }
    }
}
