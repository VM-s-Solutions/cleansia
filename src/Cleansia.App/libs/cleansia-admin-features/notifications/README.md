# notifications

The administrator's own notification feed: the `/notifications` page over the generated
`AdminNotificationClient` — newest first, twenty a page, a row per event rendered from the admin
locale bundle, mark-read on open, mark-all-read — and the unread badge the shell draws from
`AdminNotificationBadgeService`.

## Running unit tests

Run `nx test notifications` to execute the unit tests via [Jest](https://jestjs.io).
