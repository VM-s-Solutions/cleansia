import SwiftUI

/// A sheet sized by its measured content keeps that height where the system centres it, as in a regular-width
/// container, at the system's form width: fitted in both axes, a sheet whose content fills its width took the whole
/// container. On a phone the detent sizes the sheet and this changes nothing; before iOS 18 it is a no-op.
extension View {
    @ViewBuilder
    func fittedPresentationSizing() -> some View {
        if #available(iOS 18, *) {
            presentationSizing(.form.fitted(horizontal: false, vertical: true))
        } else {
            self
        }
    }
}
