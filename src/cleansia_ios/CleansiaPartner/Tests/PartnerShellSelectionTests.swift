import XCTest
@testable import CleansiaPartner

@MainActor
final class PartnerShellSelectionTests: XCTestCase {
    func testDefaultSelectionIsDashboard() {
        XCTAssertEqual(ShellModel().selection, .dashboard)
    }

    func testSelectOrdersSwitchesToOrdersTab() {
        let model = ShellModel()
        model.selectOrders()
        XCTAssertEqual(model.selection, .orders)
    }

    func testTabOrderMirrorsAndroidMainTab() {
        XCTAssertEqual(ShellTab.allCases, [.dashboard, .orders, .invoices, .profile])
    }

    func testEachTabHasItsSystemImage() {
        XCTAssertEqual(ShellTab.dashboard.systemImage, "square.grid.2x2")
        XCTAssertEqual(ShellTab.orders.systemImage, "list.clipboard")
        XCTAssertEqual(ShellTab.invoices.systemImage, "dollarsign.circle")
        XCTAssertEqual(ShellTab.profile.systemImage, "person")
    }

    func testEachTabResolvesItsLabel() {
        XCTAssertEqual(ShellTab.dashboard.label, L10n.Shell.dashboard)
        XCTAssertEqual(ShellTab.orders.label, L10n.Shell.orders)
        XCTAssertEqual(ShellTab.invoices.label, L10n.Shell.invoices)
        XCTAssertEqual(ShellTab.profile.label, L10n.Shell.profile)
    }

    func testLabelsAreNonEmptyLocalizedStrings() {
        for tab in ShellTab.allCases {
            XCTAssertFalse(tab.label.isEmpty)
        }
    }
}
