import SwiftUI

/// Piro's brand mark: the blue flame ("Piro" = pyro/fire), the same 24×24 SVG path the web/admin and
/// Android apps use, drawn as a SwiftUI `Shape` so it stays crisp at any size.
struct PiroFlame: View {
    var size: CGFloat = 48
    var color: Color = PiroColors.brand

    var body: some View {
        FlameShape()
            .fill(color)
            .frame(width: size, height: size)
            .accessibilityLabel("Piro")
    }
}

/// The flame outline. `path(in:)` parses the shared SVG path once and scales it to fit the given rect
/// (the path is authored on a 24×24 canvas), keeping it centered.
private struct FlameShape: Shape {
    static let data =
        "M12.832 21.801c3.126-.626 7.168-2.875 7.168-8.69c0-5.291-3.873-8.815-6.658-10.434" +
        "c-.619-.36-1.342.113-1.342.828v1.828c0 1.442-.606 4.074-2.29 5.169c-.86.559-1.79-.278-1.894-1.298" +
        "l-.086-.838c-.1-.974-1.092-1.565-1.87-.971C4.461 8.46 3 10.33 3 13.11C3 20.221 8.289 22 10.933 22" +
        "q.232 0 .484-.015C10.111 21.874 8 21.064 8 18.444c0-2.05 1.495-3.435 2.631-4.11c.306-.18.663.055.663.41" +
        "v.59c0 .45.175 1.155.59 1.637c.47.546 1.159-.026 1.214-.744c.018-.226.246-.37.442-.256" +
        "c.641.375 1.46 1.175 1.46 2.473c0 2.048-1.129 2.99-2.168 3.357"

    func path(in rect: CGRect) -> Path {
        let raw = SVGPathParser.parse(Self.data)
        let side = min(rect.width, rect.height)
        let scale = side / 24
        let dx = rect.minX + (rect.width - 24 * scale) / 2
        let dy = rect.minY + (rect.height - 24 * scale) / 2
        return raw.applying(CGAffineTransform(scaleX: scale, y: scale).concatenating(
            CGAffineTransform(translationX: dx, y: dy)))
    }
}

/// A minimal SVG path-data parser covering the commands the flame uses (M/m, L/l, H/h, V/v, C/c, S/s,
/// Q/q, T/t, Z/z), producing a SwiftUI `Path`. Not a full SVG implementation — just enough to render
/// the brand mark without shipping a raster asset.
enum SVGPathParser {
    static func parse(_ d: String) -> Path {
        var path = Path()
        var tokens = Tokenizer(d)
        var current = CGPoint.zero
        var start = CGPoint.zero
        var lastControl: CGPoint?
        var command: Character = " "

        func point(_ x: CGFloat, _ y: CGFloat, relative: Bool) -> CGPoint {
            relative ? CGPoint(x: current.x + x, y: current.y + y) : CGPoint(x: x, y: y)
        }

        while let cmd = tokens.nextCommand(previous: command) {
            command = cmd
            let rel = cmd.isLowercase
            switch Character(cmd.lowercased()) {
            case "m":
                current = point(tokens.number(), tokens.number(), relative: rel)
                path.move(to: current)
                start = current
                lastControl = nil
            case "l":
                current = point(tokens.number(), tokens.number(), relative: rel)
                path.addLine(to: current)
                lastControl = nil
            case "h":
                current = CGPoint(x: rel ? current.x + tokens.number() : tokens.number(), y: current.y)
                path.addLine(to: current)
                lastControl = nil
            case "v":
                current = CGPoint(x: current.x, y: rel ? current.y + tokens.number() : tokens.number())
                path.addLine(to: current)
                lastControl = nil
            case "c":
                let c1 = point(tokens.number(), tokens.number(), relative: rel)
                let c2 = point(tokens.number(), tokens.number(), relative: rel)
                let end = point(tokens.number(), tokens.number(), relative: rel)
                path.addCurve(to: end, control1: c1, control2: c2)
                lastControl = c2
                current = end
            case "s":
                let c1 = lastControl.map { CGPoint(x: 2 * current.x - $0.x, y: 2 * current.y - $0.y) } ?? current
                let c2 = point(tokens.number(), tokens.number(), relative: rel)
                let end = point(tokens.number(), tokens.number(), relative: rel)
                path.addCurve(to: end, control1: c1, control2: c2)
                lastControl = c2
                current = end
            case "q":
                let c = point(tokens.number(), tokens.number(), relative: rel)
                let end = point(tokens.number(), tokens.number(), relative: rel)
                path.addQuadCurve(to: end, control: c)
                lastControl = c
                current = end
            case "t":
                let c = lastControl.map { CGPoint(x: 2 * current.x - $0.x, y: 2 * current.y - $0.y) } ?? current
                let end = point(tokens.number(), tokens.number(), relative: rel)
                path.addQuadCurve(to: end, control: c)
                lastControl = c
                current = end
            case "z":
                path.closeSubpath()
                current = start
                lastControl = nil
            default:
                break
            }
        }
        return path
    }

    /// Streams numbers and command letters out of a path-data string, tolerating the compact SVG format
    /// (commas or spaces or sign changes as separators, repeated coordinate sets after one command).
    private struct Tokenizer {
        private let chars: [Character]
        private var i = 0

        init(_ s: String) { chars = Array(s) }

        mutating func nextCommand(previous: Character) -> Character? {
            skipSeparators()
            guard i < chars.count else { return nil }
            let c = chars[i]
            if c.isLetter {
                i += 1
                return c
            }
            // A bare coordinate set after a command repeats that command (m→l, M→L per SVG spec).
            switch previous {
            case "m": return "l"
            case "M": return "L"
            default: return previous
            }
        }

        mutating func number() -> CGFloat {
            skipSeparators()
            var s = ""
            if i < chars.count, chars[i] == "-" || chars[i] == "+" { s.append(chars[i]); i += 1 }
            var seenDot = false
            while i < chars.count {
                let c = chars[i]
                if c.isNumber {
                    s.append(c); i += 1
                } else if c == "." && !seenDot {
                    seenDot = true; s.append(c); i += 1
                } else {
                    break
                }
            }
            return CGFloat(Double(s) ?? 0)
        }

        private mutating func skipSeparators() {
            while i < chars.count, chars[i] == " " || chars[i] == "," || chars[i] == "\n" || chars[i] == "\t" {
                i += 1
            }
        }
    }
}
