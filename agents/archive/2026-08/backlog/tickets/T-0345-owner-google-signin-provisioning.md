---
id: T-0345
title: "Owner: Google Sign-In provisioning for iOS (Cloud Console project + iOS & web OAuth client ids + Google:ClientId config) — concretizes IMP-1"
status: proposed
size: S
owner: owner
created: 2026-06-28
updated: 2026-07-19
depends_on: []
blocks: []
stories: []
adrs: [0013]
layers: [ios, backend]
security_touching: false
manual_steps: [google-cloud-project, google-ios-client-id, google-web-client-id, google-clientid-config]
sprint: 12
source: Q-IOS-04 ruling (sprint-12 §7.14 §"MANUAL_STEPs" D5); concretizes the long-standing IMP-1 (Google OAuth — needs a Google Cloud Console project)
---

> **OWNER TASK — gates LIVE Google sign-in; ZERO backend code change.** The backend already verifies Google ID
> tokens (`GoogleTokenVerifier` pins `aud` to `Google:ClientId`, fails closed when empty) and `POST
> /api/Auth/GoogleAuth` is live. iOS reuses it 1:1. The only requirement is **config**: provision the client
> ids and set `Google:ClientId`. This is the still-pending **IMP-1** made concrete for the iOS customer app.

## The key invariant (keep three values in lockstep)
`iOS serverClientID` **==** backend `Google:ClientId` **==** the **WEB/server** OAuth client id.
Google mints the ID token with `aud = serverClientID`, so the token the iOS app sends carries the web client id
as its audience, and the existing verifier accepts it unchanged. A mismatch silently fails closed ("sign-in
broken").

## Steps (Google Cloud Console)
1. **Project + consent screen:** select/create the Cleansia auth project (IMP-1); configure the OAuth consent
   screen (app name, support email, developer contact, the `cz.cleansia.com` links); move to **In production**
   (or add test users while in Testing).
2. **iOS OAuth client id:** create an **OAuth 2.0 Client ID → iOS**, Bundle ID = `cz.cleansia.customer` (no
   wildcards). Google returns the iOS client id + its **reversed-client-id URL scheme**
   (`com.googleusercontent.apps.<ID>`). *(Claude adds that reversed-client-id as a `CFBundleURLSchemes` entry in
   the customer app's Info.plist / `project.yml` during T-0312 — provide the value.)*
3. **Web/server OAuth client id:** create a SECOND **OAuth 2.0 Client ID → Web application**. This is the
   audience the backend pins.
4. **Backend config:** set **`Google:ClientId` = the WEB/server client id** from step 3 (currently empty in
   committed appsettings). Set the iOS app's `serverClientID` to that **same** value.

## Done when
- [ ] The Google Cloud project + OAuth consent screen are configured.
- [ ] The iOS OAuth client id exists (+ the reversed-client-id is in the iOS Info.plist) and a web/server client id exists.
- [ ] `Google:ClientId` (backend) == iOS `serverClientID` == the web/server client id.
- [ ] A real-device Google sign-in issues a platform JWT end-to-end.

## Notes
- (Optional, NOT recommended) accepting id_tokens whose `aud` is the iOS client id would require adding the iOS
  client id to the verifier's allowed audiences — unnecessary with `serverClientID` set; a single web/server
  audience is the simplest correct setup.
- This closes the iOS slice of IMP-1; the web Google OAuth (if separately pending) is out of scope here.

## Status log
- 2026-06-28 — filed from the Q-IOS-04 ruling (§7.14 D5). Backend is config-only; iOS reuses the live
  `GoogleAuth` endpoint 1:1.
- 2026-07-19 — owner deferred 2026-07-19 — will provision Google sign-in slightly later. Code stays live and ready; no code change pending. Kept owner-blocked.
