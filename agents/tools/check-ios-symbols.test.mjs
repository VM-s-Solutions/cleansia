#!/usr/bin/env node
/**
 * Self-test for the iOS symbol gate (`check-ios-symbols.mjs`).
 *
 * It never touches the working tree: every scenario materialises a THREE-MODULE fixture under a
 * throwaway directory — CleansiaCore + CleansiaPartner + CleansiaCustomer, each with sources and a
 * string catalog — and runs the tool against it with `--root=`. A scenario is a set of edits to that
 * fixture plus the exit code and output it must produce.
 *
 * WHAT THIS HAS TO PROVE, in order of what it cost to learn:
 *
 *   1. THE GATE CAN STILL FAIL. Stub the tool's body to `process.exit(0)` and 32 of the 62 scenarios
 *      go red. That is the whole point of running this first in CI: the gate is the only iOS signal
 *      on a Windows machine, and a defanged gate reads exactly like a clean tree.
 *   2. IT CATCHES #215. Three scenarios replay the real defect — a case added to `ProfileRoute`, the
 *      arm that classified one taken away, and the same added case in a file that ALSO binds `route`
 *      somewhere unrelated — against a switch that lives in a file neither change touches. The third
 *      is there because a file-global binding map let one `func logRoute(route: String)` de-anchor
 *      the switch and the run then printed "clean" over a target that does not compile.
 *   3. IT DOES NOT CRY WOLF. Twenty-nine scenarios assert exit 0, most of them on shapes a reader
 *      reported: a same-named enum in the other app, an `extension L10n.Orders {` header, an
 *      interpolated key, a non-L10n `format(…)`, a bare call to a local `format(…)`, a type from the
 *      gitignored Api package, a FOREIGN qualified type whose last component matches a local enum, a
 *      nested type under a design namespace, a `static private(set) var`, `@unknown default`, a tuple
 *      subject, an `if case` inside an arm, a `#if`-conditional case list, an ambiguous bare name,
 *      and a typed parameter shadowed by each of the four inferred binding forms. Each of those,
 *      reported once, turns a gate into noise nobody reads — which is worse than no gate, because it
 *      also makes the true positives invisible.
 *   4. THE LEXER SURVIVES, AND SEES INTERPOLATION. Unbalanced braces in comments, in nested block
 *      comments, in plain, raw and multi-line strings — all in the same file as a switch whose
 *      missing case must still be found. If the depth tracking breaks, that switch silently stops
 *      being checked. And a symbol inside `\(…)` is code: four scenarios hold the line on both sides
 *      of that, since the literal TEXT around one is still text.
 *   5. AN EMPTY OR PARTIAL READ IS A FAILURE, not a pass.
 *
 *   node agents/tools/check-ios-symbols.test.mjs
 */
import { spawnSync } from "node:child_process";
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const HERE = dirname(fileURLToPath(import.meta.url));
const TOOL = join(HERE, "check-ios-symbols.mjs");

/** The fixture is tiny, so the shipped anti-vacuity floors are turned off for most scenarios. */
const NO_FLOORS = ["--min-files=0", "--min-l10n=0", "--min-keys=0", "--min-design=0", "--min-switches=0"];

const catalog = (...keys) =>
    JSON.stringify(
        {
            sourceLanguage: "en",
            strings: Object.fromEntries(
                keys.map((k) => [k, { localizations: { en: { stringUnit: { state: "translated", value: k } } } }])
            ),
        },
        null,
        2
    );

// ── The clean fixture ───────────────────────────────────────────────────────────────────────────
// Written with String.raw so `\(…)` reaches the file as Swift wrote it rather than as a JS escape.
const BASE = {
    // ── CleansiaCore — the package both apps see ──────────────────────────────────────────────
    "CleansiaCore/Sources/DesignSystem.swift": String.raw`
import SwiftUI

public enum CleansiaColors {
    public static let surface = Color.white
    public static let outline = Color.gray
}

public enum Spacing {
    public static let s: CGFloat = 12
    public static let m: CGFloat = 16
}

public enum CleansiaTypography {
    public static let headline = Font.headline
}
`,

    "CleansiaCore/Sources/CoreL10n.swift": String.raw`
import Foundation

public enum CoreL10n {
    static func localized(_ key: String) -> String {
        Bundle.module.localizedString(forKey: key, value: nil, table: nil)
    }
}

public enum SnapAnchor {
    case peek
    case expanded

    var label: String {
        switch self {
        case .peek: CoreL10n.localized("snap.peek")
        case .expanded: CoreL10n.localized("snap.expanded")
        }
    }
}

public enum UiState {
    case loading
    case ready
    case failed
}

/// Declared in CleansiaPartner too, with a third case. Inside the partner's visibility the bare name
/// resolves to two different enums, and Swift's shadowing rules are not something a text reader
/// implements — so nothing is anchored to it.
public enum Theme {
    case light
    case dark
}
`,

    "CleansiaCore/Resources/Localizable.xcstrings": catalog("snap.peek", "snap.expanded"),

    // ── CleansiaPartner ───────────────────────────────────────────────────────────────────────
    "CleansiaPartner/Sources/L10n.swift": String.raw`
import Foundation

enum L10n {
    enum Splash {
        static var tagline: String { localized("splash_tagline") }
    }

    static var login: String { localized("login") }

    static func localized(_ key: String) -> String { key }

    static func format(_ key: String, _ args: CVarArg...) -> String {
        String(format: localized(key), arguments: args)
    }
}
`,

    "CleansiaPartner/Sources/L10n+Orders.swift": String.raw`
import Foundation

extension L10n {
    enum Orders {
        static var title: String { localized("orders") }
    }
}

// The header 'extension L10n.Orders {' NAMES a namespace. The prototype read it as a REFERENCE to a
// member called Orders, found no such member, and reported it missing — three times.
extension L10n.Orders {
    static func kmAway(_ value: String) -> String { format("km_away", value) }
}
`,

    "CleansiaPartner/Sources/ProfileRoute.swift": String.raw`
import Foundation

enum ProfileRoute: Hashable {
    case personal(onboarding: Bool)
    case documents
    case language
    case deleteAccount
}

/// Declared in CleansiaCustomer too, with FEWER cases. Anchoring by bare name across modules matched
/// the two and reported every customer switch over it as incomplete.
enum SplashOutcome {
    case ok
    case unreachable
    case locked
}

/// Shadows CleansiaCore's Theme.
enum Theme {
    case light
    case dark
    case system
}
`,

    "CleansiaPartner/Sources/RegistrationLockView.swift": String.raw`
import CleansiaCore
import SwiftUI

struct RegistrationLockView: View {
    private let outcome: SplashOutcome

    var body: some View {
        VStack(spacing: Spacing.m) {
            Text(L10n.Splash.tagline)
                .font(CleansiaTypography.headline)
            Text(L10n.Orders.kmAway("2"))
        }
        .background(CleansiaColors.surface)
    }

    // The #215 shape: the subject's type comes from the parameter list, and the switch has no
    // default on purpose, so a new route must be classified rather than silently rendering nothing.
    @ViewBuilder
    private func sectionDestination(_ route: ProfileRoute) -> some View {
        switch route {
        case let .personal(onboarding):
            Text(onboarding ? L10n.login : L10n.Orders.title)
        case .documents:
            // An 'if case' is a pattern match, NOT a switch arm. Read as one, it adds .locked to
            // this switch's labels, the labels stop being a subset of ProfileRoute, and the whole
            // switch drops out of the check — taking any real missing case with it.
            if case .locked = outcome {
                EmptyView()
            } else {
                Text(L10n.Orders.title)
            }
        case .language, .deleteAccount:
            EmptyView()
        }
    }

    private func outcomeLabel(_ outcome: SplashOutcome) -> String {
        switch outcome {
        case .ok: "ok"
        case .unreachable: "unreachable"
        case .locked: "locked"
        }
    }

    // Covers two of the partner Theme's three cases. Ambiguous bare name -> no report.
    private func themeLabel(_ theme: Theme) -> String {
        switch theme {
        case .light: "light"
        case .dark: "dark"
        }
    }
}
`,

    "CleansiaPartner/Sources/LexTrap.swift": String.raw`
import Foundation

enum LexTrap {
    case alpha
    case beta

    // an unbalanced { in a line comment
    /* a nested /* block comment */ with an unbalanced } */
    static let openBrace = "{"
    static let rawJson = #"{"nested": "} case .gamma:"}"#
    static let paragraph = """
    a multi-line string with { braces } and "quotes"
    """

    var label: String {
        switch self {
        case .alpha: "a { b"
        case .beta: LexTrap.openBrace
        }
    }
}
`,

    "CleansiaPartner/Resources/Localizable.xcstrings": catalog("splash_tagline", "login", "orders", "km_away"),

    // ── CleansiaCustomer ──────────────────────────────────────────────────────────────────────
    "CleansiaCustomer/Sources/L10n.swift": String.raw`
import Foundation

enum L10n {
    static var home: String { localized("home") }

    static func localized(_ key: String) -> String { key }
    static func format(_ key: String, _ args: CVarArg...) -> String { key }
}
`,

    "CleansiaCustomer/Sources/CustomerHome.swift": String.raw`
import CleansiaCore
import Foundation

/// The partner declares SplashOutcome too, with a third case.
enum SplashOutcome {
    case ok
    case unreachable
}

enum StepDirection {
    case increment
    case decrement
    case sideways
}

enum RecurringTime {
    static func format(_ date: Date) -> String { "" }
}

struct CustomerOrders {
    let state: UiState

    var label: String {
        switch state {
        case .loading: "loading"
        case .ready: "ready"
        case .failed: "failed"
        }
    }
}

enum CustomerHome {
    static func splashLabel(_ outcome: SplashOutcome) -> String {
        switch outcome {
        case .ok: L10n.home
        case .unreachable: CoreL10n.localized("snap.peek")
        }
    }

    /// Assembled at runtime. There is no static key here to look up.
    static func pushTitle(eventKey: String) -> String {
        L10n.localized("push.\(eventKey).title")
    }

    /// A different format(), and its argument is not a catalog key.
    static func slot(_ date: Date) -> String {
        RecurringTime.format(date)
    }

    /// ContractStatus lives in the gitignored CleansiaCustomerApi package, which is not in the
    /// tree. Nothing can be said about this switch, so nothing is.
    static func contract(_ status: ContractStatus) -> String {
        switch status {
        case .active: "active"
        case .terminated: "terminated"
        }
    }

    /// An @unknown default IS a default.
    static func step(_ direction: StepDirection) -> Int {
        switch direction {
        case .increment: 1
        @unknown default: 0
        }
    }

    /// A tuple subject is not a plain identifier: skipped, never guessed at from the labels.
    static func pair(_ lhs: SplashOutcome, _ rhs: SplashOutcome) -> String {
        switch (lhs, rhs) {
        case (.ok, .ok): "both"
        case (.unreachable, _): "left"
        case (_, .unreachable): "right"
        }
    }
}
`,

    "CleansiaCustomer/Sources/Evidence.swift": String.raw`
import Foundation

/// The case list depends on the build configuration, so the case list a reader sees is not the one
/// the compiler sees. Never anchored, in either direction.
enum EvidenceSource {
    #if canImport(UIKit)
        case image(UIImage)
    #endif
    case pdf(Data)
}

enum EvidenceLabels {
    static func label(_ source: EvidenceSource) -> String {
        switch source {
        case .pdf: "pdf"
        }
    }
}
`,

    "CleansiaCustomer/Resources/Localizable.xcstrings": catalog("home"),
};

// ── Harness ─────────────────────────────────────────────────────────────────────────────────────
let failed = 0;
let ran = 0;
const workspace = mkdtempSync(join(tmpdir(), "ios-symbols-selftest-"));

/** Materialise `BASE` with `edits` applied (a null value deletes the file) under a fresh root. */
function materialise(edits = {}) {
    const root = mkdtempSync(join(workspace, "tree-"));
    const files = { ...BASE, ...edits };
    for (const [name, content] of Object.entries(files)) {
        if (content === null) continue;
        const path = join(root, "src", "cleansia_ios", ...name.split("/"));
        mkdirSync(dirname(path), { recursive: true });
        writeFileSync(path, content);
    }
    return root;
}

function scenario(name, { edits = {}, args = NO_FLOORS, expectExit, expectText = [], rejectText = [] }) {
    ran++;
    let root = null;
    try {
        root = materialise(edits);
        const r = spawnSync(process.execPath, [TOOL, `--root=${root}`, ...args], { encoding: "utf8" });
        const out = `${r.stdout}${r.stderr}`;
        const okExit = r.status === expectExit;
        const okText = expectText.every((t) => out.includes(t));
        const okReject = rejectText.every((t) => !out.includes(t));
        if (okExit && okText && okReject) {
            console.log(`  PASS  ${name}`);
        } else {
            failed++;
            console.log(`  FAIL  ${name}`);
            console.log(`        expected exit ${expectExit}, got ${r.status}`);
            if (!okText) console.log(`        expected output to contain: ${expectText.join(" | ")}`);
            if (!okReject) console.log(`        expected output NOT to contain: ${rejectText.join(" | ")}`);
            console.log(out.split("\n").map((l) => `        > ${l}`).join("\n"));
        }
    } catch (e) {
        failed++;
        console.log(`  FAIL  ${name} — ${e.message}`);
    } finally {
        if (root) rmSync(root, { recursive: true, force: true });
    }
}

console.log("check-ios-symbols self-test:");

// ── the clean fixture is clean, and the run states the corpus it read ───────────────────────────
scenario("a clean three-module tree passes, and says what it read", {
    expectExit: 0,
    expectText: ["3 module(s)", "CleansiaCore, CleansiaPartner, CleansiaCustomer", "ios-symbols: clean"],
});

// ── 1. exhaustive-switch — the check that would have caught #215 ────────────────────────────────
scenario("#215 REPLAYED: a case added to ProfileRoute, a default-less switch in an OLD file left behind", {
    edits: {
        "CleansiaPartner/Sources/ProfileRoute.swift": BASE["CleansiaPartner/Sources/ProfileRoute.swift"].replace(
            "    case deleteAccount\n",
            "    case deleteAccount\n    case exportData\n"
        ),
    },
    expectExit: 1,
    expectText: ["exhaustive-switch", "RegistrationLockView.swift", "ProfileRoute", "omits .exportData"],
});

scenario("#215 THE OTHER WAY ROUND: the arm that classified a case is dropped", {
    edits: {
        "CleansiaPartner/Sources/RegistrationLockView.swift": BASE[
            "CleansiaPartner/Sources/RegistrationLockView.swift"
        ].replace("case .language, .deleteAccount:", "case .language:"),
    },
    expectExit: 1,
    expectText: ["exhaustive-switch", "omits .deleteAccount"],
});

scenario("adding a `default` is a real fix, not a suppression — the same tree then passes", {
    edits: {
        "CleansiaPartner/Sources/RegistrationLockView.swift": BASE[
            "CleansiaPartner/Sources/RegistrationLockView.swift"
        ].replace("case .language, .deleteAccount:", "default:"),
    },
    expectExit: 0,
    rejectText: ["exhaustive-switch"],
});

scenario("a switch anchored through `self` inside its own enum -> RED when a case is added", {
    edits: {
        "CleansiaCore/Sources/CoreL10n.swift": BASE["CleansiaCore/Sources/CoreL10n.swift"].replace(
            "    case expanded\n",
            "    case expanded\n    case mapFocus\n"
        ),
    },
    expectExit: 1,
    expectText: ["exhaustive-switch", "SnapAnchor", "omits .mapFocus"],
});

scenario("more than one omitted case is reported as one line naming all of them", {
    edits: {
        "CleansiaPartner/Sources/ProfileRoute.swift": BASE["CleansiaPartner/Sources/ProfileRoute.swift"].replace(
            "    case deleteAccount\n",
            "    case deleteAccount\n    case exportData\n    case closeAccount\n"
        ),
    },
    expectExit: 1,
    expectText: ["omits .exportData, .closeAccount"],
});

scenario("a label the anchored enum does not declare drops the whole switch rather than mis-reporting it", {
    edits: {
        "CleansiaPartner/Sources/RegistrationLockView.swift": BASE[
            "CleansiaPartner/Sources/RegistrationLockView.swift"
        ]
            .replace("case .language, .deleteAccount:", "case .language:")
            .replace("        case .documents:", "        case .neverMind:"),
    },
    // The labels stop being a subset of ProfileRoute, so the parse is not trustworthy and the gate
    // says nothing — a miss, deliberately, rather than a guess.
    expectExit: 0,
    rejectText: ["omits"],
});

// ── the false positives that made the prototype unusable ────────────────────────────────────────
scenario("the SAME enum name in the other app, with fewer cases, is NOT matched across modules", {
    expectExit: 0,
    rejectText: ["exhaustive-switch", "SplashOutcome"],
});

scenario("...and the partner's own 3-case SplashOutcome is still checked, in its own module", {
    edits: {
        "CleansiaPartner/Sources/RegistrationLockView.swift": BASE[
            "CleansiaPartner/Sources/RegistrationLockView.swift"
        ].replace('        case .locked: "locked"\n', ""),
    },
    expectExit: 1,
    expectText: ["exhaustive-switch", "SplashOutcome", "omits .locked"],
});

scenario("a bare name declared in BOTH the app and Core is ambiguous -> nothing is said about it", {
    expectExit: 0,
    rejectText: ["Theme"],
});

scenario("a type from the gitignored Api package anchors to nothing, and is not reported", {
    expectExit: 0,
    rejectText: ["ContractStatus"],
});

scenario("`@unknown default` is a default", {
    expectExit: 0,
    rejectText: ["StepDirection"],
});

scenario("a tuple subject is skipped, not guessed at from its labels", {
    expectExit: 0,
    rejectText: ["pair", "(.ok, .ok)"],
});

scenario("an enum whose case list is behind `#if` is never anchored, in either direction", {
    expectExit: 0,
    rejectText: ["EvidenceSource"],
});

scenario("...and the `#if` guard is what keeps that quiet — a case OUTSIDE the `#if` is still not reported", {
    edits: {
        "CleansiaCustomer/Sources/Evidence.swift": BASE["CleansiaCustomer/Sources/Evidence.swift"].replace(
            "    case pdf(Data)\n",
            "    case pdf(Data)\n    case text(String)\n"
        ),
    },
    expectExit: 0,
    rejectText: ["EvidenceSource"],
});

scenario("`if case` inside an arm is a pattern match, not an arm — the real missing case still lands", {
    edits: {
        "CleansiaPartner/Sources/RegistrationLockView.swift": BASE[
            "CleansiaPartner/Sources/RegistrationLockView.swift"
        ].replace("case .language, .deleteAccount:", "case .language:"),
    },
    expectExit: 1,
    expectText: ["omits .deleteAccount"],
    // `if case .locked = outcome` must not have leaked `.locked` into this switch's labels.
    rejectText: [".locked"],
});

// ── shadowing: the guard has to see INFERRED bindings, and it has to be SCOPED ──────────────────
/**
 * A file that switches over `ProfileRoute` through a typed parameter — the #215 shape — and then
 * binds `route` a SECOND time, without writing a type, INSIDE that same function. All four shadow
 * forms are ordinary Swift, and in every one the inner switch is exhaustive over `SectionKind`, so
 * both switches compile and the gate must say nothing about either. A scanner that reads
 * annotations only never sees the shadow, goes on believing `route` is a `ProfileRoute`, and
 * reports missing cases against code that builds.
 *
 * The nesting is the whole point and is easy to get wrong: put the shadow in a SIBLING function and
 * the enclosing annotated binding is simply not live there, which the block scoping alone already
 * handles — the inferred-shape scanning would never run, and these scenarios would pass with
 * `INFERRED_RE`, `FOR_IN_RE`, `CLOSURE_PARAM_RE` and `CASE_LET_RE` deleted outright. They did,
 * until a reviewer deleted them and watched all 58 still go green.
 */
const shadowedRoute = (shadow) => String.raw`
import Foundation

enum SectionKind {
    case documents
    case language
}

struct ShadowedRoute {
    func destination(_ route: ProfileRoute, _ items: [SectionKind], _ item: SectionKind?) -> String {
${shadow}
        switch route {
        case .personal: return "p"
        case .documents: return "d"
        case .language: return "l"
        case .deleteAccount: return "x"
        }
    }
}
`;

scenario("a typed parameter shadowed by an inferred `let` does not anchor the shadowing switch", {
    edits: {
        "CleansiaPartner/Sources/ShadowedRoute.swift": shadowedRoute(String.raw`        if items.isEmpty {
            let route = SectionKind.documents
            switch route {
            case .documents: print("d")
            case .language: print("l")
            }
        }`),
    },
    expectExit: 0,
    rejectText: ["exhaustive-switch"],
});

scenario("...nor by a closure parameter", {
    edits: {
        "CleansiaPartner/Sources/ShadowedRoute.swift": shadowedRoute(String.raw`        items.forEach { route in
            switch route {
            case .documents: print("d")
            case .language: print("l")
            }
        }`),
    },
    expectExit: 0,
    rejectText: ["exhaustive-switch"],
});

scenario("...nor by a `for … in` loop variable", {
    edits: {
        "CleansiaPartner/Sources/ShadowedRoute.swift": shadowedRoute(String.raw`        for route in items {
            switch route {
            case .documents: print("d")
            case .language: print("l")
            }
        }`),
    },
    expectExit: 0,
    rejectText: ["exhaustive-switch"],
});

scenario("...nor by an `if let` unwrap", {
    edits: {
        "CleansiaPartner/Sources/ShadowedRoute.swift": shadowedRoute(String.raw`        if let route = item {
            switch route {
            case .documents: print("d")
            case .language: print("l")
            }
        }`),
    },
    expectExit: 0,
    rejectText: ["exhaustive-switch"],
});

scenario("...nor by a `case let` binding", {
    edits: {
        "CleansiaPartner/Sources/ShadowedRoute.swift": shadowedRoute(String.raw`        if case let .some(route) = item {
            switch route {
            case .documents: print("d")
            case .language: print("l")
            }
        }`),
    },
    expectExit: 0,
    rejectText: ["exhaustive-switch"],
});

// A closure parameter list has four legal spellings and the scanner used to match one. The other
// three shadow just as effectively, so each gets its own scenario rather than one representative:
// a regex that handles `{ x in }` tells you nothing about whether it handles `{ (x: T) in }`.
scenario("...nor by a closure parameter written with a return type", {
    edits: {
        "CleansiaPartner/Sources/ShadowedRoute.swift": shadowedRoute(String.raw`        _ = items.map { route -> String in
            switch route {
            case .documents: return "d"
            case .language: return "l"
            }
        }`),
    },
    expectExit: 0,
    rejectText: ["exhaustive-switch"],
});

scenario("...nor by a parenthesised, explicitly typed closure parameter", {
    edits: {
        "CleansiaPartner/Sources/ShadowedRoute.swift": shadowedRoute(String.raw`        items.forEach { (route: SectionKind) in
            switch route {
            case .documents: print("d")
            case .language: print("l")
            }
        }`),
    },
    expectExit: 0,
    rejectText: ["exhaustive-switch"],
});

scenario("...nor by a parenthesised closure parameter with a return type", {
    edits: {
        "CleansiaPartner/Sources/ShadowedRoute.swift": shadowedRoute(String.raw`        _ = items.map { (route) -> String in
            switch route {
            case .documents: return "d"
            case .language: return "l"
            }
        }`),
    },
    expectExit: 0,
    rejectText: ["exhaustive-switch"],
});

// The same root cause pointing the other way: dropping a name because it is bound twice ANYWHERE in
// the file took the #215 switch out of the check entirely, and the run then printed "clean" over a
// target that does not compile. A binding is live in a BLOCK, and `logRoute` is a different block.
scenario("#215 STILL CAUGHT when an unrelated function in the same file reuses the subject's name", {
    edits: {
        "CleansiaPartner/Sources/ProfileRoute.swift": BASE["CleansiaPartner/Sources/ProfileRoute.swift"].replace(
            "    case deleteAccount\n",
            "    case deleteAccount\n    case exportData\n"
        ),
        "CleansiaPartner/Sources/RegistrationLockView.swift": BASE[
            "CleansiaPartner/Sources/RegistrationLockView.swift"
        ].replace(
            "    // Covers two of the partner Theme's",
            "    private func logRoute(route: String) { print(route) }\n\n    // Covers two of the partner Theme's"
        ),
    },
    expectExit: 1,
    expectText: ["exhaustive-switch", "RegistrationLockView.swift", "ProfileRoute", "omits .exportData"],
});

// ── the annotation's QUALIFIER is part of the anchor, not decoration ─────────────────────────────
const SLIDE_TO_CONFIRM = String.raw`
import Foundation

public struct SlideToConfirm {
    public enum Style {
        case primary
        case destructive
    }
}
`;

scenario("a FOREIGN qualified type does not anchor to a local enum sharing its last component", {
    edits: {
        "CleansiaCore/Sources/SlideToConfirm.swift": SLIDE_TO_CONFIRM,
        "CleansiaPartner/Sources/BlurStyle.swift": String.raw`
import UIKit

struct BlurStyle {
    func name(_ style: UIBlurEffect.Style) -> String {
        switch style {
        case .primary: "p"
        }
    }
}
`,
    },
    expectExit: 0,
    rejectText: ["SlideToConfirm", "exhaustive-switch"],
});

scenario("...and the qualifier check does not just disable anchoring — the LOCAL nested enum still lands", {
    edits: {
        "CleansiaCore/Sources/SlideToConfirm.swift": SLIDE_TO_CONFIRM,
        "CleansiaPartner/Sources/BlurStyle.swift": String.raw`
import CleansiaCore

struct BlurStyle {
    func name(_ style: SlideToConfirm.Style) -> String {
        switch style {
        case .primary: "p"
        }
    }
}
`,
    },
    expectExit: 1,
    expectText: ["exhaustive-switch", "SlideToConfirm.Style", "omits .destructive"],
});

// ── the lexer ───────────────────────────────────────────────────────────────────────────────────
scenario("unbalanced braces in comments, raw, plain and multi-line strings do not hide a missing case", {
    edits: {
        "CleansiaPartner/Sources/LexTrap.swift": BASE["CleansiaPartner/Sources/LexTrap.swift"].replace(
            "        case .beta: LexTrap.openBrace\n",
            ""
        ),
    },
    expectExit: 1,
    expectText: ["exhaustive-switch", "LexTrap", "omits .beta"],
});

scenario("`case .gamma:` inside a raw string is not an enum case declaration", {
    expectExit: 0,
    rejectText: ["gamma"],
});

// ── `\(…)` interpolation is CODE. Blanking it with the literal was a hole, not a safety margin ───
// `L10n.Orders.servicesMore` is referenced three times in the real tree and all three sit inside an
// interpolation, so the member class this gate exists to guard was the one it could not see.
scenario("an L10n member referenced ONLY inside string interpolation is still checked", {
    edits: {
        "CleansiaPartner/Sources/InterpolatedUse.swift": String.raw`
import SwiftUI

struct InterpolatedUse: View {
    var body: some View {
        Text("you have \(L10n.Orders.servicesMore(3)) more")
    }
}
`,
    },
    expectExit: 1,
    expectText: ["l10n-members", "L10n.Orders.servicesMore", "no such declared member in CleansiaPartner"],
});

scenario("...and so is a design-system member referenced only there", {
    edits: {
        "CleansiaPartner/Sources/InterpolatedUse.swift": String.raw`
import CleansiaCore
import SwiftUI

struct InterpolatedUse: View {
    var body: some View {
        Text("gap \(Spacing.enormous)")
    }
}
`,
    },
    expectExit: 1,
    expectText: ["design-system", "Spacing.enormous is referenced but never declared"],
});

scenario("...and so is a catalog key looked up INSIDE an interpolation", {
    edits: {
        "CleansiaPartner/Sources/InterpolatedUse.swift": String.raw`
import SwiftUI

struct InterpolatedUse: View {
    var body: some View {
        Text("hello \(L10n.localized("greeting_missing")) there")
    }
}
`,
    },
    expectExit: 1,
    expectText: ["l10n-keys", '"greeting_missing" is not in', "CleansiaPartner/Resources/Localizable.xcstrings"],
});

// The other half of that fix: only the `\(…)` contents came back. The literal TEXT around them is
// still text — a symbol NAMED in prose is not a reference, and a `{` in prose still is not a brace.
scenario("the literal TEXT around an interpolation is still blanked", {
    edits: {
        "CleansiaPartner/Sources/InterpolatedUse.swift": String.raw`
import Foundation

enum InterpolatedUse {
    static func caption(_ count: Int) -> String {
        "Spacing.enormous and L10n.Nope.gone in { prose \(count) more"
    }
}
`,
    },
    expectExit: 0,
    rejectText: ["Spacing.enormous", "L10n.Nope.gone"],
});

// ── 2. l10n-members ─────────────────────────────────────────────────────────────────────────────
scenario("an L10n path with no declared member -> RED", {
    edits: {
        "CleansiaPartner/Sources/RegistrationLockView.swift": BASE[
            "CleansiaPartner/Sources/RegistrationLockView.swift"
        ].replace("L10n.Splash.tagline", "L10n.DeleteAccount.rowTitle"),
    },
    expectExit: 1,
    expectText: ["l10n-members", "L10n.DeleteAccount.rowTitle", "no such declared member in CleansiaPartner"],
});

scenario("a member declared in an `extension L10n.Orders` block resolves", {
    expectExit: 0,
    rejectText: ["l10n-members"],
});

scenario("the `extension L10n.Orders {` HEADER is not read as a usage of a member called `Orders`", {
    expectExit: 0,
    rejectText: ["L10n.Orders —"],
});

scenario("an L10n member the PARTNER declares is not visible to the CUSTOMER", {
    edits: {
        "CleansiaCustomer/Sources/CustomerHome.swift": BASE["CleansiaCustomer/Sources/CustomerHome.swift"].replace(
            "case .ok: L10n.home",
            "case .ok: L10n.Splash.tagline"
        ),
    },
    expectExit: 1,
    expectText: ["l10n-members", "L10n.Splash.tagline", "CleansiaCustomer"],
});

// ── 3. l10n-keys ────────────────────────────────────────────────────────────────────────────────
scenario("a key that is not in the app's catalog -> RED, naming the catalog it looked in", {
    edits: {
        "CleansiaPartner/Sources/L10n.swift": BASE["CleansiaPartner/Sources/L10n.swift"].replace(
            'localized("login")',
            'localized("login_v2")'
        ),
    },
    expectExit: 1,
    expectText: ["l10n-keys", '"login_v2" is not in', "CleansiaPartner/Resources/Localizable.xcstrings"],
});

scenario("`CoreL10n.localized` reads CleansiaCore's catalog even when called from an app", {
    edits: {
        "CleansiaCustomer/Sources/CustomerHome.swift": BASE["CleansiaCustomer/Sources/CustomerHome.swift"].replace(
            'CoreL10n.localized("snap.peek")',
            'CoreL10n.localized("snap.nope")'
        ),
    },
    expectExit: 1,
    expectText: ["l10n-keys", '"snap.nope" is not in', "CleansiaCore/Resources/Localizable.xcstrings"],
});

scenario("...and a Core key is NOT looked for in the customer catalog (the clean fixture proves it)", {
    expectExit: 0,
    rejectText: ["snap.peek"],
});

scenario("an INTERPOLATED key is not a key — 4 of these were reported as missing", {
    expectExit: 0,
    rejectText: ["push."],
});

scenario("a `format(…)` on some other type is not an L10n key", {
    edits: {
        "CleansiaCustomer/Sources/CustomerHome.swift": BASE["CleansiaCustomer/Sources/CustomerHome.swift"].replace(
            "RecurringTime.format(date)",
            'RecurringTime.format("18:30")'
        ),
    },
    expectExit: 0,
    rejectText: ["18:30"],
});

scenario("a key on the line AFTER the call still resolves (the multi-line call shape)", {
    edits: {
        "CleansiaPartner/Sources/L10n.swift": BASE["CleansiaPartner/Sources/L10n.swift"].replace(
            'static var login: String { localized("login") }',
            "static var login: String {\n        localized(\n            \"login_missing\"\n        )\n    }"
        ),
    },
    expectExit: 1,
    expectText: ["l10n-keys", '"login_missing"'],
});

scenario("a catalog that is not valid JSON is a problem, not a silent pass", {
    edits: { "CleansiaPartner/Resources/Localizable.xcstrings": "{ not json" },
    expectExit: 1,
    expectText: ["unreadable string catalog"],
});

// A BARE call is how the L10n extensions are written, so a bare call is only a lookup where that is
// what it means. Anywhere else it is somebody's own helper, and its argument is a format string —
// reported as a missing translation, it makes the whole module catalog that file's vocabulary.
scenario("a bare call to a LOCAL `format(…)` helper is not a catalog lookup", {
    edits: {
        "CleansiaPartner/Sources/ItemCount.swift": String.raw`
import Foundation

struct ItemCount {
    func format(_ pattern: String, _ value: Int) -> String { pattern }

    var summary: String { format("%d items", 3) }
}
`,
    },
    expectExit: 0,
    rejectText: ["%d items"],
});

// ── 4. design-system ────────────────────────────────────────────────────────────────────────────
scenario("a Spacing member that does not exist -> RED", {
    edits: {
        "CleansiaPartner/Sources/RegistrationLockView.swift": BASE[
            "CleansiaPartner/Sources/RegistrationLockView.swift"
        ].replace("Spacing.m", "Spacing.enormous"),
    },
    expectExit: 1,
    expectText: ["design-system", "Spacing.enormous is referenced but never declared"],
});

scenario("a CleansiaColors member that does not exist -> RED", {
    edits: {
        "CleansiaPartner/Sources/RegistrationLockView.swift": BASE[
            "CleansiaPartner/Sources/RegistrationLockView.swift"
        ].replace("CleansiaColors.surface", "CleansiaColors.surfaceTint"),
    },
    expectExit: 1,
    expectText: ["design-system", "CleansiaColors.surfaceTint"],
});

scenario("a CleansiaTypography member that does not exist -> RED", {
    edits: {
        "CleansiaPartner/Sources/RegistrationLockView.swift": BASE[
            "CleansiaPartner/Sources/RegistrationLockView.swift"
        ].replace("CleansiaTypography.headline", "CleansiaTypography.hero"),
    },
    expectExit: 1,
    expectText: ["design-system", "CleansiaTypography.hero"],
});

scenario("`.self` on a design-system namespace is metatype access, not a member", {
    edits: {
        "CleansiaPartner/Sources/RegistrationLockView.swift": BASE[
            "CleansiaPartner/Sources/RegistrationLockView.swift"
        ].replace("CleansiaColors.surface", "CleansiaColors.self.surface"),
    },
    expectExit: 0,
    rejectText: ["is referenced but never declared"],
});

// A NESTED TYPE resolves under the namespace exactly as a static member does, and the usage regex
// cannot tell them apart. The real `CleansiaTypography` already declares `public struct Scale`, so
// counting only `static var|let|func` made writing that type's name anywhere a finding.
scenario("a nested TYPE under a design namespace is a declaration, not a missing member", {
    edits: {
        "CleansiaCore/Sources/DesignSystem.swift": BASE["CleansiaCore/Sources/DesignSystem.swift"]
            .replace(
                "public enum CleansiaTypography {\n",
                "public enum CleansiaTypography {\n    public struct Scale {\n        public let titleMedium: Font\n    }\n\n"
            )
            .replace(
                "public enum CleansiaColors {\n",
                "public enum CleansiaColors {\n    public enum Brand {\n        public static let primary = Color.blue\n    }\n\n"
            ),
        "CleansiaPartner/Sources/ScaleUse.swift": String.raw`
import CleansiaCore
import SwiftUI

struct ScaleUse {
    let scale: CleansiaTypography.Scale
    let tint = CleansiaColors.Brand.primary
}
`,
    },
    expectExit: 0,
    rejectText: ["is referenced but never declared"],
});

// A word boundary also sits after a dot, so an unrelated type NAMED `Spacing` was read as THE
// `Spacing`, and its members reported as missing from a design system it has nothing to do with.
scenario("a QUALIFIED reference to an unrelated nested type is not a design-system reference", {
    edits: {
        "CleansiaPartner/Sources/Legacy.swift": String.raw`
import CoreGraphics

enum Legacy {
    enum Spacing {
        static let gutter: CGFloat = 8
    }
}

struct LegacyUse {
    let gutter = Legacy.Spacing.gutter
}
`,
    },
    expectExit: 0,
    rejectText: ["is referenced but never declared"],
});

// `public static private(set) var` is legal Swift. Requiring `var|let|func` IMMEDIATELY after
// `static` meant the member was never registered and every reference to it read as undeclared.
scenario("a `static private(set) var` member is a declaration", {
    edits: {
        "CleansiaCore/Sources/DesignSystem.swift": BASE["CleansiaCore/Sources/DesignSystem.swift"].replace(
            "    public static let outline = Color.gray\n",
            "    public static let outline = Color.gray\n    public static private(set) var accent = Color.orange\n"
        ),
        "CleansiaPartner/Sources/AccentUse.swift": String.raw`
import CleansiaCore
import SwiftUI

struct AccentUse {
    let tint = CleansiaColors.accent
}
`,
    },
    expectExit: 0,
    rejectText: ["is referenced but never declared"],
});

// ── anti-vacuity: an unread corpus must never print green ───────────────────────────────────────
scenario("a tree with only TWO of the three modules -> RED, and that floor is NOT overridable", {
    edits: {
        "CleansiaCustomer/Sources/L10n.swift": null,
        "CleansiaCustomer/Sources/CustomerHome.swift": null,
        "CleansiaCustomer/Sources/Evidence.swift": null,
        "CleansiaCustomer/Resources/Localizable.xcstrings": null,
    },
    expectExit: 1,
    expectText: ["REACH", "found only 2 of 3 modules"],
});

scenario("a file floor the corpus cannot clear -> RED (the reader is broken, not the tree)", {
    args: ["--min-files=5000", "--min-l10n=0", "--min-keys=0", "--min-design=0", "--min-switches=0"],
    expectExit: 1,
    expectText: ["REACH", "swift file(s) (floor 5000)", "the reader is broken, not the tree"],
});

scenario("a switch floor the corpus cannot clear -> RED (a lexer that finds no switches is not a pass)", {
    args: ["--min-files=0", "--min-l10n=0", "--min-keys=0", "--min-design=0", "--min-switches=999"],
    expectExit: 1,
    expectText: ["REACH", "anchored only", "switch(es) (floor 999)"],
});

scenario("an L10n floor the corpus cannot clear -> RED", {
    args: ["--min-files=0", "--min-l10n=9999", "--min-keys=0", "--min-design=0", "--min-switches=0"],
    expectExit: 1,
    expectText: ["REACH", "L10n reference(s) (floor 9999)"],
});

scenario("a catalog-key floor the corpus cannot clear -> RED", {
    args: ["--min-files=0", "--min-l10n=0", "--min-keys=9999", "--min-design=0", "--min-switches=0"],
    expectExit: 1,
    expectText: ["REACH", "catalog key(s) (floor 9999)"],
});

scenario("a design-system floor the corpus cannot clear -> RED", {
    args: ["--min-files=0", "--min-l10n=0", "--min-keys=0", "--min-design=9999", "--min-switches=0"],
    expectExit: 1,
    expectText: ["REACH", "design-system reference(s) (floor 9999)"],
});

// ── modes ───────────────────────────────────────────────────────────────────────────────────────
scenario("--warn reports the problem and still exits 0", {
    edits: {
        "CleansiaPartner/Sources/RegistrationLockView.swift": BASE[
            "CleansiaPartner/Sources/RegistrationLockView.swift"
        ].replace("case .language, .deleteAccount:", "case .language:"),
    },
    args: [...NO_FLOORS, "--warn"],
    expectExit: 0,
    expectText: ["exhaustive-switch", "omits .deleteAccount"],
});

// `--warn` downgrades findings ABOUT THE TREE. A run that did not read the tree has no findings to
// downgrade, so it must not be silenceable — the shape check-catalog-claims settled on.
scenario("a REACH failure exits 1 EVEN under --warn", {
    args: ["--min-files=5000", "--warn"],
    expectExit: 1,
    expectText: ["REACH", "the reader is broken, not the tree"],
});

scenario("--verbose lists what was anchored, so a reader can audit the anchoring", {
    args: [...NO_FLOORS, "--verbose"],
    expectExit: 0,
    expectText: ["anchored", "switch route -> ProfileRoute (4/4)", "switch self -> SnapAnchor (2/2)"],
});

// ── a tree that is not there is a broken run, not an empty one ──────────────────────────────────
{
    ran++;
    const r = spawnSync(process.execPath, [TOOL, `--ios=${join(workspace, "nope")}`], { encoding: "utf8" });
    const out = `${r.stdout}${r.stderr}`;
    if (r.status === 1 && out.includes("no iOS tree at")) {
        console.log("  PASS  an --ios path that is not on disk -> RED before anything is read");
    } else {
        failed++;
        console.log(`  FAIL  an --ios path that is not on disk -> RED (exit ${r.status})\n        > ${out}`);
    }
}

rmSync(workspace, { recursive: true, force: true });

if (failed > 0) {
    console.log(`\ncheck-ios-symbols self-test: ${failed} of ${ran} scenario(s) FAILED`);
    process.exit(1);
}
console.log(`\ncheck-ios-symbols self-test: all ${ran} scenarios passed`);
