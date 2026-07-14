import os

/// Diagnostics for the push-registration path (Xcode console, subsystem
/// cz.cleansia.push). Logs counts/booleans only — never the token value.
public enum PushLog {
    public static let log = Logger(subsystem: "cz.cleansia.push", category: "registration")
}
