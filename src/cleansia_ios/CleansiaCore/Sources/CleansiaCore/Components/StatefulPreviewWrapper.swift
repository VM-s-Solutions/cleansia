#if DEBUG
    import SwiftUI

    struct StatefulPreviewWrapper<Value, Content: View>: View {
        @State private var value: Value
        private let content: (Binding<Value>) -> Content

        init(_ initialValue: Value, @ViewBuilder content: @escaping (Binding<Value>) -> Content) {
            _value = State(initialValue: initialValue)
            self.content = content
        }

        var body: some View {
            content($value)
        }
    }
#endif
