import SwiftUI

public struct CodeInput: View {
    @Binding private var code: String
    private let length: Int
    @FocusState private var focused: Bool

    public init(code: Binding<String>, length: Int = 6) {
        _code = code
        self.length = length
    }

    public var body: some View {
        ZStack {
            TextField("", text: Binding(
                get: { code },
                set: { code = String($0.filter(\.isNumber).prefix(length)) }
            ))
            .keyboardType(.numberPad)
            .textContentType(.oneTimeCode)
            .focused($focused)
            .frame(width: 1, height: 1)
            .opacity(0.01)

            HStack(spacing: Spacing.xs) {
                ForEach(0 ..< length, id: \.self) { index in
                    box(at: index)
                }
            }
            .contentShape(Rectangle())
            .onTapGesture { focused = true }
        }
        .frame(maxWidth: .infinity)
        .onAppear { focused = true }
    }

    private func box(at index: Int) -> some View {
        let character = index < code.count
            ? String(Array(code)[index])
            : ""
        let isFocusedBox = index == code.count
        return Text(character)
            .font(CleansiaFont.poppins(.semibold, size: 24))
            .foregroundColor(CleansiaColors.onSurface)
            .frame(width: 44, height: 56)
            .background(CleansiaColors.surface)
            .clipShape(RoundedRectangle(cornerRadius: CornerRadius.small))
            .overlay(
                RoundedRectangle(cornerRadius: CornerRadius.small)
                    .stroke(
                        isFocusedBox ? CleansiaColors.primary : CleansiaColors.outline,
                        lineWidth: isFocusedBox ? 2 : 1
                    )
            )
    }
}

#if DEBUG
    struct CodeInput_Previews: PreviewProvider {
        static var previews: some View {
            StatefulPreviewWrapper("123") { binding in
                CodeInput(code: binding)
                    .padding()
            }
        }
    }
#endif
