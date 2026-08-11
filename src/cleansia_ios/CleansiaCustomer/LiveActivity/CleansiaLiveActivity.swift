import ActivityKit
import CleansiaCore
import SwiftUI
import WidgetKit

// The CleansiaCustomerLiveActivity widget extension entry point: it binds ActivityKit to the card, and
// nothing else. The card itself — the four legs the in-app order screen also walks — lives in CleansiaCore
// (LiveActivityCardViews), which this target links and the app target links too, so a glance at the lock
// screen and a look at the app cannot drift apart.
//
// The whole extension deploys at iOS 16.1, so no @available guards are needed inside it.

@main
struct CleansiaLiveActivityBundle: WidgetBundle {
    var body: some Widget {
        CleanOrderLiveActivity()
    }
}

struct CleanOrderLiveActivity: Widget {
    var body: some WidgetConfiguration {
        ActivityConfiguration(for: CleanOrderAttributes.self) { context in
            LiveActivityCleanCard(model: context.state.cardModel())
                .activityBackgroundTint(CleansiaColors.primary.opacity(0.10))
                .activitySystemActionForegroundColor(CleansiaColors.primary)
        } dynamicIsland: { context in
            let model = context.state.cardModel()
            return DynamicIsland {
                DynamicIslandExpandedRegion(.leading) {
                    VStack(alignment: .leading, spacing: 2) {
                        LiveActivityBrandLockup()
                        LiveActivityOrderLabel(model: model)
                    }
                }
                DynamicIslandExpandedRegion(.trailing) {
                    if let caption = model.card.timeCaption {
                        LiveActivityClock(caption: caption, at: model.legEnd, compact: false)
                    }
                }
                DynamicIslandExpandedRegion(.bottom) {
                    VStack(alignment: .leading, spacing: 8) {
                        LiveActivityHeadlineLine(headline: model.card.headline)
                        if let detail = model.card.detail {
                            Text(detail).font(.caption).foregroundColor(.secondary).lineLimit(1)
                        }
                        if let position = model.card.position {
                            LiveActivityStepperBar(position: position, liveRange: model.liveRange)
                        }
                    }
                }
            } compactLeading: {
                LiveActivityStepDots(position: model.card.position)
            } compactTrailing: {
                LiveActivityCompactReadout(model: model)
            } minimal: {
                Circle().fill(CleansiaColors.primary).frame(width: 8, height: 8)
            }
            .keylineTint(CleansiaColors.primary)
        }
    }
}
