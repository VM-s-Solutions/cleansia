import PDFKit
import UIKit

/// The drawn tree of a captured window, layer by layer: each layer's class, its frame in the window, its fill, the
/// part of it its clipping ancestors leave visible, and the ink it draws. A text layer also gives the fonts it set
/// (read back from a PDF of the layer alone) and its glyph clusters; an image layer gives its ink box. The captures
/// are measured from it against the design's type and alignment rules; nothing here asserts.
enum CaptureRenderTree {
    static func json(of window: UIWindow) -> Data {
        var nodes: [[String: Any]] = []
        func visit(_ layer: CALayer, _ depth: Int, _ clip: CGRect) {
            guard depth < 160, !layer.isHidden, layer.opacity > 0.01 else { return }
            let frame = layer.convert(layer.bounds, to: window.layer)
            var node = describe(layer, frame: frame, in: window)
            node["depth"] = depth
            let visible = frame.intersection(clip)
            if visible.isNull || visible.width * visible.height < frame.width * frame.height - 0.5 {
                node["visible"] = visible.isNull ? [] : rounded(visible)
            }
            nodes.append(node)
            let inner = layer.masksToBounds ? clip.intersection(frame) : clip
            layer.sublayers?.forEach { visit($0, depth + 1, inner) }
        }
        visit(window.layer, 0, window.bounds)
        return (try? JSONSerialization.data(withJSONObject: nodes, options: [.sortedKeys])) ?? Data()
    }

    /// Each text the window draws, with its frame in the window.
    static func texts(in window: UIWindow) -> [(text: String, frame: CGRect)] {
        var out: [(text: String, frame: CGRect)] = []
        func visit(_ layer: CALayer) {
            guard !layer.isHidden else { return }
            if isText(layer) {
                out.append((drawing(of: layer).text, layer.convert(layer.bounds, to: window.layer)))
            }
            layer.sublayers?.forEach(visit)
        }
        visit(window.layer)
        return out
    }

    private static func isText(_ layer: CALayer) -> Bool {
        (layer.delegate as? UIView).map { String(describing: type(of: $0)) == "CGDrawingView" } ?? false
    }

    private static func describe(_ layer: CALayer, frame: CGRect, in window: UIWindow) -> [String: Any] {
        var node: [String: Any] = ["layer": String(describing: type(of: layer)), "frame": rounded(frame)]
        if let view = layer.delegate as? UIView { node["view"] = String(describing: type(of: view)) }
        if let fill = layer.backgroundColor, fill.alpha > 0.01 { node["fill"] = hex(fill) }
        if layer.cornerRadius > 0 { node["radius"] = layer.cornerRadius }
        if layer.borderWidth > 0, let border = layer.borderColor {
            node["border"] = [layer.borderWidth, hex(border)] as [Any]
        }
        if let shape = layer as? CAShapeLayer, let path = shape.path {
            node["path"] = rounded(layer.convert(path.boundingBoxOfPath, to: window.layer))
            if let fill = shape.fillColor, fill.alpha > 0.01 { node["shapeFill"] = hex(fill) }
        }
        if let contents = layer.contents.map({ $0 as CFTypeRef }), CFGetTypeID(contents) == CGImage.typeID {
            let image = unsafeBitCast(contents, to: CGImage.self)
            node["image"] = [image.width, image.height]
            if let ink = inkBox(of: image) {
                node["ink"] = rounded(CGRect(
                    x: frame.minX + ink.minX * frame.width,
                    y: frame.minY + ink.minY * frame.height,
                    width: ink.width * frame.width,
                    height: ink.height * frame.height
                ))
            }
        }
        if isText(layer) {
            let drawn = drawing(of: layer)
            node["fonts"] = drawn.fonts
            node["text"] = drawn.text
            node["glyphs"] = glyphs(of: layer)
        }
        return node
    }

    /// The fonts the layer sets, in drawing order, with their sizes, and the text it draws: the layer's drawing run
    /// alone into a PDF, whose font resources name each face.
    private static func drawing(of layer: CALayer) -> (fonts: [[String: Any]], text: String) {
        let data = NSMutableData()
        var box = CGRect(
            origin: .zero,
            size: CGSize(width: max(layer.bounds.width, 1), height: max(layer.bounds.height, 1))
        )
        guard let consumer = CGDataConsumer(data: data as CFMutableData),
              let context = CGContext(consumer: consumer, mediaBox: &box, nil) else { return ([], "") }
        context.beginPDFPage(nil)
        context.translateBy(x: 0, y: box.height)
        context.scaleBy(x: 1, y: -1)
        UIGraphicsPushContext(context)
        layer.draw(in: context)
        UIGraphicsPopContext()
        context.endPDFPage()
        context.closePDF()
        guard let provider = CGDataProvider(data: data),
              let page = CGPDFDocument(provider)?.page(at: 1) else { return ([], "") }
        let text = PDFDocument(data: data as Data)?.string ?? ""
        let names = fontNames(of: page)
        var out: [[String: Any]] = []
        for run in fontRuns(of: page) {
            let base = names[run.name] ?? run.name
            let face = base.split(separator: "+").last.map(String.init) ?? base
            let size = ((run.size * (run.scale > 0 ? run.scale : 1)) * 100).rounded() / 100
            if let last = out.last, last["font"] as? String == face, last["size"] as? CGFloat == size { continue }
            out.append(["font": face, "size": size])
        }
        return (out, text)
    }

    /// The page's font resources: resource name to the face's PostScript name.
    private static func fontNames(of page: CGPDFPage) -> [String: String] {
        let names = NameBox()
        var resources: CGPDFDictionaryRef?
        var fontTable: CGPDFDictionaryRef?
        guard let pageDictionary = page.dictionary,
              CGPDFDictionaryGetDictionary(pageDictionary, "Resources", &resources), let resources,
              CGPDFDictionaryGetDictionary(resources, "Font", &fontTable), let fontTable else { return [:] }
        CGPDFDictionaryApplyFunction(fontTable, { key, object, info in
            guard let info else { return }
            let names = Unmanaged<NameBox>.fromOpaque(info).takeUnretainedValue()
            var font: CGPDFDictionaryRef?
            var base: UnsafePointer<CChar>?
            guard CGPDFObjectGetValue(object, .dictionary, &font), let font,
                  CGPDFDictionaryGetName(font, "BaseFont", &base), let base else { return }
            names.values[String(cString: key)] = String(cString: base)
        }, Unmanaged.passUnretained(names).toOpaque())
        return names.values
    }

    /// The font of every run of text the page shows (`TJ`, `Tj`, `'`, `"`): the font set last (`Tf`) at the text
    /// matrix's vertical scale in force (`Tm`).
    private static func fontRuns(of page: CGPDFPage) -> [FontRun] {
        guard let table = CGPDFOperatorTableCreate() else { return [] }
        CGPDFOperatorTableSetCallback(table, "Tf") { scanner, info in
            guard let info else { return }
            let runs = Unmanaged<RunBox>.fromOpaque(info).takeUnretainedValue()
            var size: CGPDFReal = 0
            var name: UnsafePointer<CChar>?
            guard CGPDFScannerPopNumber(scanner, &size), CGPDFScannerPopName(scanner, &name), let name else { return }
            runs.font = (String(cString: name), size)
        }
        CGPDFOperatorTableSetCallback(table, "Tm") { scanner, info in
            guard let info else { return }
            let runs = Unmanaged<RunBox>.fromOpaque(info).takeUnretainedValue()
            var parts = [CGPDFReal](repeating: 0, count: 6)
            for index in (0 ..< 6).reversed() {
                guard CGPDFScannerPopNumber(scanner, &parts[index]) else { return }
            }
            runs.scale = abs(parts[3])
        }
        let shown: CGPDFOperatorCallback = { _, info in
            guard let info else { return }
            let runs = Unmanaged<RunBox>.fromOpaque(info).takeUnretainedValue()
            guard let font = runs.font else { return }
            runs.values.append(FontRun(name: font.name, size: font.size, scale: runs.scale))
        }
        for name in ["TJ", "Tj", "'", "\""] {
            CGPDFOperatorTableSetCallback(table, name, shown)
        }
        let runs = RunBox()
        let scanner = CGPDFScannerCreate(
            CGPDFContentStreamCreateWithPage(page),
            table,
            Unmanaged.passUnretained(runs).toOpaque()
        )
        CGPDFScannerScan(scanner)
        return runs.values
    }

    /// The layer drawn alone at 3x: each run of inked columns on a line (a glyph, or glyphs that touch) as [start,
    /// end, top, foot] in points from the layer's origin, and the line's index.
    private static func glyphs(of layer: CALayer) -> [[CGFloat]] {
        let scale: CGFloat = 3
        let width = Int((layer.bounds.width * scale).rounded(.up))
        let height = Int((layer.bounds.height * scale).rounded(.up))
        guard width > 0, height > 0, width * height < 4_000_000 else { return [] }
        var alpha = [UInt8](repeating: 0, count: width * height)
        let drawn: Bool = alpha.withUnsafeMutableBytes { buffer in
            guard let context = CGContext(
                data: buffer.baseAddress, width: width, height: height, bitsPerComponent: 8, bytesPerRow: width,
                space: CGColorSpaceCreateDeviceGray(), bitmapInfo: CGImageAlphaInfo.alphaOnly.rawValue
            ) else { return false }
            context.translateBy(x: 0, y: CGFloat(height))
            context.scaleBy(x: scale, y: -scale)
            layer.render(in: context)
            return true
        }
        guard drawn else { return [] }
        return runs(alpha, width: width, height: height).map { run in
            run.prefix(4).map { ($0 / scale * 100).rounded() / 100 } + [run[4]]
        }
    }

    /// The text's lines are the bands of inked rows; in each, every run of inked columns as [start, end, top, foot,
    /// line].
    private static func runs(_ alpha: [UInt8], width: Int, height: Int) -> [[CGFloat]] {
        var out: [[CGFloat]] = []
        for (index, line) in lines(alpha, width: width, height: height).enumerated() {
            var first = -1, top = Int.max, foot = -1
            for column in 0 ... width {
                let rows = column < width ? line.filter { alpha[$0 * width + column] > 110 } : []
                if let columnTop = rows.first, let columnFoot = rows.last {
                    if first < 0 { first = column }
                    top = min(top, columnTop)
                    foot = max(foot, columnFoot + 1)
                } else if first >= 0 {
                    out.append([CGFloat(first), CGFloat(column), CGFloat(top), CGFloat(foot), CGFloat(index)])
                    first = -1
                    top = .max
                    foot = -1
                }
            }
        }
        return out
    }

    /// The bands of inked rows; a band under two-fifths of the tallest (an accent, a dot) joins the band under it.
    private static func lines(_ alpha: [UInt8], width: Int, height: Int) -> [Range<Int>] {
        let inked = (0 ..< height).map { row in (0 ..< width).contains { alpha[row * width + $0] > 110 } }
        var bands: [Range<Int>] = []
        var start = -1
        for row in 0 ... height {
            let ink = row < height && inked[row]
            if ink, start < 0 { start = row }
            if !ink, start >= 0 {
                bands.append(start ..< row)
                start = -1
            }
        }
        let tallest = bands.map(\.count).max() ?? 0
        var lines: [Range<Int>] = []
        var carried: Int?
        for (index, band) in bands.enumerated() {
            let thin = band.count * 5 < tallest * 2
            if thin, index < bands.count - 1 {
                carried = carried ?? band.lowerBound
                continue
            }
            if thin, let last = lines.popLast() {
                lines.append(last.lowerBound ..< band.upperBound)
            } else {
                lines.append((carried ?? band.lowerBound) ..< band.upperBound)
            }
            carried = nil
        }
        return lines
    }

    /// The image's ink (alpha over half) as fractions of its size.
    private static func inkBox(of image: CGImage) -> CGRect? {
        let width = image.width, height = image.height
        guard width > 0, height > 0, width * height < 4_000_000 else { return nil }
        var alpha = [UInt8](repeating: 0, count: width * height)
        let drawn: Bool = alpha.withUnsafeMutableBytes { buffer in
            guard let context = CGContext(
                data: buffer.baseAddress, width: width, height: height, bitsPerComponent: 8, bytesPerRow: width,
                space: CGColorSpaceCreateDeviceGray(), bitmapInfo: CGImageAlphaInfo.alphaOnly.rawValue
            ) else { return false }
            context.draw(image, in: CGRect(x: 0, y: 0, width: width, height: height))
            return true
        }
        guard drawn else { return nil }
        var minX = width, minY = height, maxX = -1, maxY = -1
        for row in 0 ..< height {
            for column in 0 ..< width where alpha[row * width + column] > 127 {
                minX = min(minX, column)
                maxX = max(maxX, column)
                minY = min(minY, row)
                maxY = max(maxY, row)
            }
        }
        guard maxX >= 0 else { return nil }
        return CGRect(
            x: CGFloat(minX) / CGFloat(width),
            y: CGFloat(minY) / CGFloat(height),
            width: CGFloat(maxX + 1 - minX) / CGFloat(width),
            height: CGFloat(maxY + 1 - minY) / CGFloat(height)
        )
    }

    private struct FontRun {
        let name: String
        let size: CGFloat
        let scale: CGFloat
    }

    private final class NameBox {
        var values: [String: String] = [:]
    }

    private final class RunBox {
        var values: [FontRun] = []
        var font: (name: String, size: CGFloat)?
        var scale: CGFloat = 1
    }

    private static func rounded(_ rect: CGRect) -> [CGFloat] {
        [rect.minX, rect.minY, rect.width, rect.height].map { ($0 * 100).rounded() / 100 }
    }

    private static func hex(_ color: CGColor) -> String {
        guard let srgb = color.converted(
            to: CGColorSpace(name: CGColorSpace.sRGB) ?? CGColorSpaceCreateDeviceRGB(),
            intent: .defaultIntent,
            options: nil
        ), let parts = srgb.components, parts.count >= 3 else { return "?" }
        let alpha = parts.count > 3 ? parts[3] : 1
        return String(
            format: "#%02X%02X%02X%02X",
            Int((parts[0] * 255).rounded()),
            Int((parts[1] * 255).rounded()),
            Int((parts[2] * 255).rounded()),
            Int((alpha * 255).rounded())
        )
    }
}
