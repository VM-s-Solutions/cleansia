import CleansiaPartnerApi
import Foundation

/// Renders a payout destination the way the cleaner's own bank statement does —
/// `prefix-number/bankCode` locally, the IBAN for everything else.
///
/// The server stores the local parts zero-padded to their fixed widths, so the padding
/// is stripped back off for display and for pre-filling the form; re-padding it is the
/// server's job and is idempotent.
enum PayoutAccountSummary {
    static func text(for details: MyPayoutDetails?) -> String? {
        guard let details else { return nil }

        let number = withoutPadding(details.accountNumber)
        let bankCode = details.bankCode?.trimmed ?? ""
        if !number.isEmpty, !bankCode.isEmpty {
            let prefix = withoutPadding(details.accountPrefix)
            return prefix.isEmpty ? "\(number)/\(bankCode)" : "\(prefix)-\(number)/\(bankCode)"
        }

        return details.iban?.trimmedOrNil ?? number.trimmedOrNil
    }

    static func withoutPadding(_ value: String?) -> String {
        String((value?.trimmed ?? "").drop { $0 == "0" })
    }
}
