import Foundation

/// Formats API timestamps (ISO-8601 strings) for display, the iOS counterpart of the Android
/// `DateFormat.localDateTime`. Falls back to the raw string if it can't be parsed.
enum PiroDate {
    private static let iso: ISO8601DateFormatter = {
        let f = ISO8601DateFormatter()
        f.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        return f
    }()

    private static let isoNoFraction: ISO8601DateFormatter = {
        let f = ISO8601DateFormatter()
        f.formatOptions = [.withInternetDateTime]
        return f
    }()

    private static let display: DateFormatter = {
        let f = DateFormatter()
        f.dateStyle = .medium
        f.timeStyle = .short
        return f
    }()

    static func localDateTime(_ raw: String) -> String {
        let date = iso.date(from: raw) ?? isoNoFraction.date(from: raw)
        guard let date else { return raw }
        return display.string(from: date)
    }
}
