#!/usr/bin/env node
// check-error-text-parity — one backend error key reads the same on every client, in each language.
//
// A backend refusal is one sentence the platform says. Six clients each carry their own copy of it —
// three webs, two Android apps and the iOS catalogue the two iOS apps share — and nothing kept those
// copies in step: on 2026-11-18 `validation.invalid_password` read four ways in Ukrainian (F-095,
// T-0938). This checker compares them.
//
// THE KEYS: every key under `api` in the three webs' `assets/i18n/<locale>.json` (the shared
// HttpErrorInterceptorFn resolves `api.<key>`). The same key is found on each other client the way
// that client resolves it at runtime:
//   - Android customer  `R.string.error_<key with . -> _, lowercased>`          (ApiErrorParser.kt)
//   - Android partner   the same with `-` -> `_` too, then `error_key_<…>`      (ApiErrorTranslator.kt)
//   - iOS (both apps)   `error.<key>` in CleansiaCore's Localizable.xcstrings    (ApiErrorLocalizer.swift)
// A key a client does not translate is not compared there: coverage is the parity specs' job, this
// is the wording.
//
// WHAT IS NORMALISED, AND ONLY THIS — none of it is wording:
//   - surrounding whitespace;
//   - Android's resource encoding: XML entities, inline markup tags, a value wrapped in double quotes,
//     and backslash escapes (`\'`, `\"`, `\n`, …). The webs' JSON and the xcstrings JSON need nothing.
// A trailing full stop is NOT normalised. Measured on 2026-11-19 the platforms share no convention to
// differ on: the customer web, both Android apps and iOS end 85-99 % of their error texts with one;
// the partner and admin webs split about half and half within the same file, which is drift, not a
// convention. No error text carries a placeholder today, so none is normalised.
//
// ANTI-FALSE-GREEN: every one of the 30 client files (6 clients x 5 locales) must exist and parse,
// and every client must yield at least one error text; otherwise a P0, never a pass.
//
// Usage:
//   node agents/tools/check-error-text-parity.mjs               # any difference -> exit 1
//   node agents/tools/check-error-text-parity.mjs --markdown    # the same, as a Markdown table
//   node agents/tools/check-error-text-parity.mjs --root=DIR    # another tree (used by the self-test)

import { existsSync, readFileSync } from "node:fs";
import { join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const args = process.argv.slice(2);
const markdown = args.includes("--markdown");
const rootArg = (args.find((a) => a.startsWith("--root=")) || "").slice("--root=".length);
const REPO = rootArg ? resolve(rootArg) : resolve(join(fileURLToPath(import.meta.url), "..", "..", ".."));

const LOCALES = ["en", "cs", "sk", "uk", "ru"];
const P0 = [];

function readText(path) {
    const full = join(REPO, path);
    if (!existsSync(full)) {
        P0.push(`P0 ${path}: not found`);
        return null;
    }
    return readFileSync(full, "utf8");
}

function readJson(path) {
    const text = readText(path);
    if (text === null) return null;
    try {
        return JSON.parse(text);
    } catch (e) {
        P0.push(`P0 ${path}: not JSON (${e.message})`);
        return null;
    }
}

// ── webs: nested `api` object -> flat dot keys ──────────────────────────────
function flatten(obj, prefix, out) {
    for (const [k, v] of Object.entries(obj ?? {})) {
        if (v && typeof v === "object") flatten(v, `${prefix}${k}.`, out);
        else if (typeof v === "string") out.set(`${prefix}${k}`, v);
    }
    return out;
}

function webClient(name, app) {
    const byLocale = {};
    for (const l of LOCALES) {
        const json = readJson(`src/Cleansia.App/apps/${app}/src/assets/i18n/${l}.json`);
        byLocale[l] = flatten(json?.api, "", new Map());
    }
    return { name, byLocale, lookup: (l, key) => byLocale[l].get(key) };
}

// ── Android: strings.xml, decoded the way the resource compiler decodes it ──
const ENTITIES = { amp: "&", lt: "<", gt: ">", quot: '"', apos: "'" };
function decodeAndroid(raw) {
    let s = raw
        .replace(/<[^>]+>/g, "")
        .replace(/&(#x[0-9a-f]+|#\d+|\w+);/gi, (m, e) =>
            e[0] === "#"
                ? String.fromCodePoint(e[1] === "x" || e[1] === "X" ? parseInt(e.slice(2), 16) : Number(e.slice(1)))
                : (ENTITIES[e] ?? m),
        )
        .trim();
    if (s.length > 1 && s.startsWith('"') && s.endsWith('"') && !s.endsWith('\\"')) s = s.slice(1, -1);
    return s.replace(/\\(.)/g, (_, c) => ({ n: "\n", t: "\t" })[c] ?? c);
}

function androidClient(name, app, names) {
    const byLocale = {};
    for (const l of LOCALES) {
        const xml = readText(`src/cleansia_android/${app}/src/main/res/values${l === "en" ? "" : `-${l}`}/strings.xml`);
        const map = new Map();
        for (const m of (xml ?? "").matchAll(/<string\s+name="([^"]+)"[^>]*>([\s\S]*?)<\/string>/g))
            map.set(m[1], decodeAndroid(m[2]));
        byLocale[l] = map;
    }
    return {
        name,
        byLocale,
        lookup: (l, key) => names(key).map((n) => byLocale[l].get(n)).find((v) => v !== undefined),
    };
}

// ── iOS: CleansiaCore's xcstrings, read by both iOS apps ─────────────────────
function iosClient() {
    const catalog = readJson("src/cleansia_ios/CleansiaCore/Sources/CleansiaCore/Resources/Localizable.xcstrings");
    const strings = catalog?.strings ?? {};
    const value = (l, key) => strings[`error.${key}`]?.localizations?.[l]?.stringUnit?.value;
    return { name: "ios", lookup: value };
}

const clients = [
    webClient("web-customer", "cleansia.app"),
    webClient("web-partner", "cleansia-partner.app"),
    webClient("web-admin", "cleansia-admin.app"),
    androidClient("android-customer", "customer-app", (k) => [`error_${k.replace(/\./g, "_").toLowerCase()}`]),
    androidClient("android-partner", "partner-app", (k) => {
        const n = k.replace(/[.-]/g, "_").toLowerCase();
        return [`error_${n}`, `error_key_${n}`];
    }),
    iosClient(),
];

// ── compare ─────────────────────────────────────────────────────────────────
const keys = new Set();
for (const c of clients.slice(0, 3)) for (const l of LOCALES) for (const k of c.byLocale[l].keys()) keys.add(k);

const reached = new Map(clients.map((c) => [c.name, 0]));
const drift = []; // { key, locale, texts: [[client, text]] }
for (const key of [...keys].sort()) {
    for (const l of LOCALES) {
        const texts = [];
        for (const c of clients) {
            const v = c.lookup(l, key);
            if (v === undefined) continue;
            reached.set(c.name, reached.get(c.name) + 1);
            texts.push([c.name, v.trim()]);
        }
        if (new Set(texts.map(([, t]) => t)).size > 1) drift.push({ key, locale: l, texts });
    }
}
for (const [name, n] of reached) if (n === 0) P0.push(`P0 ${name}: read no error text — its file or its key naming has moved`);

// ── report ──────────────────────────────────────────────────────────────────
const driftKeys = new Set(drift.map((d) => d.key)).size;
const summary = `check-error-text-parity: ${keys.size} key(s) compared across ${clients.length} clients, ${driftKeys} differ (${drift.length} key-locale(s))`;
const cell = (t) => t.replace(/\|/g, "\\|").replace(/\n/g, "<br>");

if (markdown) {
    const names = clients.map((c) => c.name);
    console.log(`| Key | Locale | ${names.join(" | ")} |`);
    console.log(`|---|---|${names.map(() => "---").join("|")}|`);
    for (const d of drift) {
        const by = new Map(d.texts);
        console.log(`| \`${d.key}\` | ${d.locale} | ${names.map((n) => (by.has(n) ? cell(by.get(n)) : "")).join(" | ")} |`);
    }
} else {
    for (const d of drift) {
        console.log(`${d.key} [${d.locale}]`);
        for (const [name, text] of d.texts) console.log(`    ${name.padEnd(17)} ${JSON.stringify(text).slice(1, -1)}`);
    }
}
for (const p of P0) console.log(p);
console.log(markdown ? `\n${summary}` : summary);
process.exit(P0.length || drift.length ? 1 : 0);
