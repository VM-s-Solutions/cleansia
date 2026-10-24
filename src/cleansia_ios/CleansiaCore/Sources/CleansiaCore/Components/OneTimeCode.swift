/// What a field holding a numeric one-time code keeps of an edit. Android's twin (`acceptOneTimeCode`) is
/// held to the same case list; keep the two in step.
public enum OneTimeCode {
    /// `raw` is the field's whole new text and `current` the code it replaces. A run of at least `length`
    /// digits arriving in one edit is a whole code — a paste, AutoFill, or iOS inserting an AutoFilled code
    /// twice — and replaces the code, so a code pasted onto a half-typed one is not appended to it. Anything
    /// shorter is typing, applied where it landed. Either way only ASCII digits survive, at most `length`.
    public static func accept(_ raw: String, replacing current: String, length: Int) -> String {
        let old = Array(current)
        let new = Array(raw)
        var prefix = 0
        while prefix < old.count, prefix < new.count, old[prefix] == new[prefix] {
            prefix += 1
        }
        var suffix = 0
        while suffix < old.count - prefix, suffix < new.count - prefix,
              old[old.count - 1 - suffix] == new[new.count - 1 - suffix]
        {
            suffix += 1
        }
        let inserted = digits(in: new[prefix ..< new.count - suffix])
        if inserted.count >= length {
            return String(inserted.prefix(length))
        }
        return String(digits(in: new[...]).prefix(length))
    }

    private static func digits(in characters: ArraySlice<Character>) -> [Character] {
        characters.filter { $0.isASCII && $0.isNumber }
    }
}
