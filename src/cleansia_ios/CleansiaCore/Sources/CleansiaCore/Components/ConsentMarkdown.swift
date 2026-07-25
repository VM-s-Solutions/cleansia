import Foundation

/// The link targets a consent sentence may carry. Translators write the
/// placeholder, never the real address — a URL in a string catalog is one the
/// five locale files would each have to be re-translated to change.
public enum ConsentLink: String, CaseIterable {
    case terms = "cleansia://terms"
    case privacy = "cleansia://privacy"

    public var url: URL {
        switch self {
        case .terms: CleansiaWeb.termsURL
        case .privacy: CleansiaWeb.privacyURL
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
}
