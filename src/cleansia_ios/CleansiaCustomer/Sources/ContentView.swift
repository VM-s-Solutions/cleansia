import CleansiaCore
import SwiftUI

struct ContentView: View {
    var body: some View {
        NavigationStack {
            VStack(spacing: 8) {
                Image(systemName: "sparkles")
                    .font(.largeTitle)
                Text("Cleansia")
                    .font(.title2)
            }
            .navigationTitle("Cleansia")
        }
    }
}

#Preview {
    ContentView()
}
