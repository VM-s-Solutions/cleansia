# `/.well-known` assets

Everything in this directory is published verbatim at `https://<host>/.well-known/<name>`.
Three moving parts make that work, and all three are easy to break:

- `project.json` copies `**/*` from here to the output `/.well-known` (this README is
  excluded — do not remove that `ignore` entry, or it would be served publicly too). The
  source directory is deliberately dot-less; only the output directory is dotted.
- `server.ts` mounts `/.well-known` with `dotfiles: 'allow'` **before** the general static
  handler. Express's `send` defaults to `dotfiles: 'ignore'`, which would 404 the request
  into the SSR catch-all and answer with the Angular HTML page and **HTTP 200** — Apple
  reports that as "invalid file", not as "not found".
- The deploy's *Upload Customer App artifact (SSR)* step (`.github/workflows/deploy-azure.yml`)
  sets `include-hidden-files: true`. `actions/upload-artifact` leaves dot-folders out by
  default, so without it `browser/.well-known` never reaches the host and every file here
  is answered with the Angular page.

## `apple-app-site-association`

Names the customer iOS app (`ST9QYZHG63.cz.cleansia.customer`) under `webcredentials`, so iOS
offers and saves strong passwords for this host. The file has no extension, so `server.ts`
sets its `Content-Type` to `application/json`; `send` would type it
`application/octet-stream`. After a deploy:
`curl -i https://<host>/.well-known/apple-app-site-association` must answer 200,
`content-type: application/json`, no redirect, and the JSON itself.

## `apple-developer-domain-association.txt`

Currently a **placeholder**. Sign in with Apple on the web will not verify until it is
replaced:

1. Apple Developer portal → Certificates, Identifiers & Profiles → Identifiers → the
   `Cleansia Customer Web` **Services ID** → Sign In with Apple → **Configure**.
2. Register the domain and the return URL (`https://<host>/auth/apple/callback` — it must
   match `APPLE_REDIRECT_PATH` in `libs/core/services/.../apple-sign-in.ts`), then
   **Download** the domain-association file.
3. Overwrite this directory's `apple-developer-domain-association.txt` with it, unchanged.
4. Deploy, and confirm the file — not the app shell — is what is served:
   `curl -i https://<host>/.well-known/apple-developer-domain-association.txt`
   A body starting with `<!doctype html` means the mount or the asset glob did not take.
5. Only then click **Verify** in the portal; Apple fetches the URL synchronously.

An `assetlinks.json` dropped in here is published the same way. A file with no extension also
needs its content type set in `server.ts`, as `apple-app-site-association` has.
