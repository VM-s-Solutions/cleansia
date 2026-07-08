import Foundation

/// ISO 3166-1 alpha-3 → alpha-2 normalisation for the service-area seam
/// (parity with Android `:core` `IsoCountryCodes`).
///
/// The backend stores alpha-3 uppercase ("CZE") while everything
/// geocoder-facing is alpha-2 lowercase (`GeocodedAddress.countryIsoCode`
/// comes from `CLPlacemark.isoCountryCode`). Comparing the two forms directly
/// can never match, so call sites normalise to alpha-2 HERE, once.
///
/// Android builds its map at runtime from `Locale.getISOCountries()`;
/// Foundation has no alpha-3 table, so this is the full 249-entry ISO 3166-1
/// table generated from the same JDK ISO data. `IsoCountryCodesTests` pins the
/// mapping for every alpha-3 code in the backend Countries seed so the
/// constant cannot drift silently.
public enum IsoCountryCodes {
    /// "CZE"/"cze" → "cz"; "cz" stays "cz"; unknown codes pass through
    /// lowercased; nil → "".
    public static func toAlpha2(_ code: String?) -> String {
        let normalized = code?.trimmingCharacters(in: .whitespacesAndNewlines).lowercased() ?? ""
        return alpha3ToAlpha2[normalized] ?? normalized
    }

    private static let alpha3ToAlpha2: [String: String] = [
        "abw": "aw", "afg": "af", "ago": "ao", "aia": "ai", "ala": "ax", "alb": "al", "and": "ad",
        "are": "ae", "arg": "ar", "arm": "am", "asm": "as", "ata": "aq", "atf": "tf",
        "atg": "ag", "aus": "au", "aut": "at", "aze": "az", "bdi": "bi", "bel": "be",
        "ben": "bj", "bes": "bq", "bfa": "bf", "bgd": "bd", "bgr": "bg", "bhr": "bh",
        "bhs": "bs", "bih": "ba", "blm": "bl", "blr": "by", "blz": "bz", "bmu": "bm",
        "bol": "bo", "bra": "br", "brb": "bb", "brn": "bn", "btn": "bt", "bvt": "bv",
        "bwa": "bw", "caf": "cf", "can": "ca", "cck": "cc", "che": "ch", "chl": "cl",
        "chn": "cn", "civ": "ci", "cmr": "cm", "cod": "cd", "cog": "cg", "cok": "ck",
        "col": "co", "com": "km", "cpv": "cv", "cri": "cr", "cub": "cu", "cuw": "cw",
        "cxr": "cx", "cym": "ky", "cyp": "cy", "cze": "cz", "deu": "de", "dji": "dj",
        "dma": "dm", "dnk": "dk", "dom": "do", "dza": "dz", "ecu": "ec", "egy": "eg",
        "eri": "er", "esh": "eh", "esp": "es", "est": "ee", "eth": "et", "fin": "fi",
        "fji": "fj", "flk": "fk", "fra": "fr", "fro": "fo", "fsm": "fm", "gab": "ga",
        "gbr": "gb", "geo": "ge", "ggy": "gg", "gha": "gh", "gib": "gi", "gin": "gn",
        "glp": "gp", "gmb": "gm", "gnb": "gw", "gnq": "gq", "grc": "gr", "grd": "gd",
        "grl": "gl", "gtm": "gt", "guf": "gf", "gum": "gu", "guy": "gy", "hkg": "hk",
        "hmd": "hm", "hnd": "hn", "hrv": "hr", "hti": "ht", "hun": "hu", "idn": "id",
        "imn": "im", "ind": "in", "iot": "io", "irl": "ie", "irn": "ir", "irq": "iq",
        "isl": "is", "isr": "il", "ita": "it", "jam": "jm", "jey": "je", "jor": "jo",
        "jpn": "jp", "kaz": "kz", "ken": "ke", "kgz": "kg", "khm": "kh", "kir": "ki",
        "kna": "kn", "kor": "kr", "kwt": "kw", "lao": "la", "lbn": "lb", "lbr": "lr",
        "lby": "ly", "lca": "lc", "lie": "li", "lka": "lk", "lso": "ls", "ltu": "lt",
        "lux": "lu", "lva": "lv", "mac": "mo", "maf": "mf", "mar": "ma", "mco": "mc",
        "mda": "md", "mdg": "mg", "mdv": "mv", "mex": "mx", "mhl": "mh", "mkd": "mk",
        "mli": "ml", "mlt": "mt", "mmr": "mm", "mne": "me", "mng": "mn", "mnp": "mp",
        "moz": "mz", "mrt": "mr", "msr": "ms", "mtq": "mq", "mus": "mu", "mwi": "mw",
        "mys": "my", "myt": "yt", "nam": "na", "ncl": "nc", "ner": "ne", "nfk": "nf",
        "nga": "ng", "nic": "ni", "niu": "nu", "nld": "nl", "nor": "no", "npl": "np",
        "nru": "nr", "nzl": "nz", "omn": "om", "pak": "pk", "pan": "pa", "pcn": "pn",
        "per": "pe", "phl": "ph", "plw": "pw", "png": "pg", "pol": "pl", "pri": "pr",
        "prk": "kp", "prt": "pt", "pry": "py", "pse": "ps", "pyf": "pf", "qat": "qa",
        "reu": "re", "rou": "ro", "rus": "ru", "rwa": "rw", "sau": "sa", "sdn": "sd",
        "sen": "sn", "sgp": "sg", "sgs": "gs", "shn": "sh", "sjm": "sj", "slb": "sb",
        "sle": "sl", "slv": "sv", "smr": "sm", "som": "so", "spm": "pm", "srb": "rs",
        "ssd": "ss", "stp": "st", "sur": "sr", "svk": "sk", "svn": "si", "swe": "se",
        "swz": "sz", "sxm": "sx", "syc": "sc", "syr": "sy", "tca": "tc", "tcd": "td",
        "tgo": "tg", "tha": "th", "tjk": "tj", "tkl": "tk", "tkm": "tm", "tls": "tl",
        "ton": "to", "tto": "tt", "tun": "tn", "tur": "tr", "tuv": "tv", "twn": "tw",
        "tza": "tz", "uga": "ug", "ukr": "ua", "umi": "um", "ury": "uy", "usa": "us",
        "uzb": "uz", "vat": "va", "vct": "vc", "ven": "ve", "vgb": "vg", "vir": "vi",
        "vnm": "vn", "vut": "vu", "wlf": "wf", "wsm": "ws", "yem": "ye", "zaf": "za",
        "zmb": "zm", "zwe": "zw"
    ]
}
