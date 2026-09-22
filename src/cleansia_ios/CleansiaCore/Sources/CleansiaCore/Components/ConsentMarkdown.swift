import Foundation
import SwiftUI

/// The link targets a legal sentence may carry. Translators write the
/// placeholder, never the real address — a URL in a string catalog is one the
/// five locale files would each have to be re-translated to change.
public enum ConsentLink: String, CaseIterable {
    case terms = "cleansia://terms"
    case privacy = "cleansia://privacy"
    case workContract = "cleansia://work-contract"

    public var url: URL {
        switch self {
        case .terms: CleansiaWeb.termsURL
        case .privacy: CleansiaWeb.privacyURL
        case .workContract: CleansiaWeb.workContractURL
        }
    }

    public static var targets: [String: URL] {
        Dictionary(uniqueKeysWithValues: allCases.map { ($0.rawValue, $0.url) })
    }
}

/// Turns a localized consent sentence carrying markdown links into a rendered
/// `AttributedString` whose placeholder targets point at the real web pages.
/// A sentence whose markup a translation broke or dropped still renders in
/// full, as plain text — the consent copy is legally load-bearing.
public enum ConsentMarkdown {
    public static func attributed(
        _ markdown: String,
        targets: [String: URL] = ConsentLink.targets
    ) -> AttributedString {
        guard var attributed = try? AttributedString(
            markdown: markdown,
            options: .init(interpretedSyntax: .inlineOnlyPreservingWhitespace)
        ) else {
            return AttributedString(markdown)
        }
        let rewrites: [(Range<AttributedString.Index>, URL?)] = attributed.runs.compactMap { run in
            run.link.map { (run.range, targets[$0.absoluteString]) }
        }
        for (range, url) in rewrites {
            attributed[range].link = url
        }
        return attributed
    }

    /// The rendered sentence with its links coloured and underlined on the runs
    /// themselves: a `Text`-level `foregroundColor` would otherwise flatten the
    /// links into body copy, and colour alone is not an accessible affordance.
    public static func styled(
        _ markdown: String,
        targets: [String: URL] = ConsentLink.targets
    ) -> AttributedString {
        var styled = attributed(markdown, targets: targets)
        let linked = styled.runs.compactMap { $0.link == nil ? nil : $0.range }
        for range in linked {
            styled[range].foregroundColor = CleansiaColors.primary
            styled[range].underlineStyle = .single
        }
        return styled
    }
}
