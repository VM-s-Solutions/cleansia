# /mobile — Direct mobile work (single-shot escape hatch)

For a small, well-scoped Android or iOS change. For anything cross-layer or non-trivial, use `/team`.

## Usage
```
/mobile <describe the change>   # default Android; say "iOS" to target the SwiftUI apps
```

## What it does
Act as the **Android Dev** (`.claude/agents/android.md`) or **iOS Dev** (`.claude/agents/ios.md`),
reading first:
- `agents/knowledge/patterns-mobile.md` (ViewModel/StateFlow/events, screen split, repository+Hilt;
  the iOS parity table)
- `agents/knowledge/conventions.md`
- `docs/architecture/push-notifications.md` (when touching notifications)

Build to standards: MVVM + StateFlow/`@Published`, no logic in composables/views, navigation via
events, string resources (never hardcoded), shared code in `:core`/`Core`. iOS work mirrors the
existing Android feature 1:1.

## Build (Android)
- Mock debug: `gradlew.bat assembleMockDebug` · Prod debug: `gradlew.bat assembleProdDebug`

## Rules
- Do not commit or push unless the owner asks.

## Example
```
/mobile Add an order-detail screen with status updates
```
