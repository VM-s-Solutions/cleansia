#!/usr/bin/env node
// Self-test for check-error-text-parity.
//
// The checker fails when one backend error key reads differently between clients in the same
// language. A parity check that cannot fail reports "0 differ" forever, so every "does NOT flag" case
// below is paired with one proving the same rule still FIRES, and the reach cases prove a client the
// checker could not read is a failure, never a silent pass.

import { execFileSync } from "child_process";
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from "fs";
import { join } from "path";
import { fileURLToPath } from "url";
import assert from "assert";

const REPO = join(fileURLToPath(import.meta.url), "..", "..", "..");
const TOOL = join(REPO, "agents", "tools", "check-error-text-parity.mjs");
const LOCALES = ["en", "cs", "sk", "uk", "ru"];
const WEBS = ["cleansia.app", "cleansia-partner.app", "cleansia-admin.app"];
const ANDROIDS = ["customer-app", "partner-app"];
const IOS_CATALOG = "src/cleansia_ios/CleansiaCore/Sources/CleansiaCore/Resources/Localizable.xcstrings";

// A tree where every client says "Wrong password." for validation.invalid_password, in every locale.
// `edit(tree)` mutates the file map before it is written; a value of null deletes that file.
function tree() {
    const files = {};
    for (const app of WEBS)
        for (const l of LOCALES)
            files[`src/Cleansia.App/apps/${app}/src/assets/i18n/${l}.json`] = {
                api: { validation: { invalid_password: `Wrong password ${l}.` } },
            };
    for (const app of ANDROIDS)
        for (const l of LOCALES)
            files[`src/cleansia_android/${app}/src/main/res/values${l === "en" ? "" : `-${l}`}/strings.xml`] = [
                `<string name="error_validation_invalid_password">Wrong password ${l}.</string>`,
            ];
    const strings = {
        "error.validation.invalid_password": {
            localizations: Object.fromEntries(
                LOCALES.map((l) => [l, { stringUnit: { state: "translated", value: `Wrong password ${l}.` } }]),
            ),
        },
    };
    files[IOS_CATALOG] = { sourceLanguage: "en", strings, version: "1.0" };
    return files;
}

function run(edit = () => {}, extra = []) {
    const files = tree();
    edit(files);
    const root = mkdtempSync(join(REPO, ".errtext-fixture-"));
    try {
        for (const [path, body] of Object.entries(files)) {
            if (body === null) continue;
            const full = join(root, path);
            mkdirSync(join(full, ".."), { recursive: true });
            const text = Array.isArray(body)
                ? `<?xml version="1.0" encoding="utf-8"?>\n<resources>\n    ${body.join("\n    ")}\n</resources>\n`
                : typeof body === "string"
                  ? body
                  : JSON.stringify(body, null, 2);
            writeFileSync(full, text, "utf8");
        }
        try {
            return { code: 0, out: execFileSync(process.execPath, [TOOL, `--root=${root}`, ...extra], { encoding: "utf8" }) };
        } catch (e) {
            return { code: e.status ?? 1, out: (e.stdout ?? "") + (e.stderr ?? "") };
        }
    } finally {
        rmSync(root, { recursive: true, force: true });
    }
}

const web = (app, l) => `src/Cleansia.App/apps/${app}/src/assets/i18n/${l}.json`;
const droid = (app, l) => `src/cleansia_android/${app}/src/main/res/values${l === "en" ? "" : `-${l}`}/strings.xml`;
const iosValue = (files, key, l) => files[IOS_CATALOG].strings[key].localizations[l].stringUnit;

const cases = [];
const test = (name, fn) => cases.push([name, fn]);

test("passes when every client reads the same in every locale", () => {
    const r = run();
    assert.equal(r.code, 0, r.out);
    assert.match(r.out, /1 key\(s\) compared across 6 clients, 0 differ/);
});

test("fails when one web reads differently, naming key, locale and every client's text", () => {
    const r = run((f) => {
        f[web("cleansia-admin.app", "uk")].api.validation.invalid_password = "Неправильний пароль";
    });
    assert.equal(r.code, 1, r.out);
    assert.match(r.out, /validation\.invalid_password \[uk\]/);
    assert.match(r.out, /web-admin\s+Неправильний пароль/);
    assert.match(r.out, /web-customer\s+Wrong password uk\./);
    assert.match(r.out, /1 key\(s\) compared across 6 clients, 1 differ \(1 key-locale\(s\)\)/);
});

test("fails when an Android app reads differently", () => {
    const r = run((f) => {
        f[droid("partner-app", "cs")] = [`<string name="error_validation_invalid_password">Jiné heslo.</string>`];
    });
    assert.equal(r.code, 1, r.out);
    assert.match(r.out, /validation\.invalid_password \[cs\][\s\S]*android-partner\s+Jiné heslo\./);
});

test("fails when iOS reads differently", () => {
    const r = run((f) => {
        iosValue(f, "error.validation.invalid_password", "ru").value = "Другой пароль.";
    });
    assert.equal(r.code, 1, r.out);
    assert.match(r.out, /validation\.invalid_password \[ru\][\s\S]*ios\s+Другой пароль\./);
});

test("a trailing full stop is wording, not normalised away", () => {
    const r = run((f) => {
        f[web("cleansia-partner.app", "en")].api.validation.invalid_password = "Wrong password en";
    });
    assert.equal(r.code, 1, r.out);
    assert.match(r.out, /web-partner\s+Wrong password en\n/);
});

test("surrounding whitespace is not wording", () => {
    const r = run((f) => {
        f[web("cleansia.app", "en")].api.validation.invalid_password = "  Wrong password en.\n";
        f[droid("customer-app", "en")] = [
            `<string name="error_validation_invalid_password">\n        Wrong password en.\n    </string>`,
        ];
    });
    assert.equal(r.code, 0, r.out);
});

test("Android resource encoding is decoded: escapes, entities and a quoted value", () => {
    const r = run((f) => {
        for (const app of WEBS) f[web(app, "en")].api.validation.invalid_password = `Can't "sign" in & retry.`;
        iosValue(f, "error.validation.invalid_password", "en").value = `Can't "sign" in & retry.`;
        f[droid("customer-app", "en")] = [`<string name="error_validation_invalid_password">Can\\'t \\"sign\\" in &amp; retry.</string>`];
        f[droid("partner-app", "en")] = [`<string name="error_validation_invalid_password">"Can't \\"sign\\" in &amp; retry."</string>`];
    });
    assert.equal(r.code, 0, r.out);
});

test("a key a client does not translate is not drift", () => {
    const r = run((f) => {
        f[droid("customer-app", "sk")] = [`<string name="error_other">x</string>`];
        delete f[IOS_CATALOG].strings["error.validation.invalid_password"].localizations.sk;
    });
    assert.equal(r.code, 0, r.out);
});

test("the partner app's error_key_ fallback and '-' in a key are read as it reads them", () => {
    const r = run((f) => {
        for (const app of WEBS) f[web(app, "en")].api.order = { "no-show": "Missed." };
        f[droid("partner-app", "en")].push(`<string name="error_key_order_no_show">Absent.</string>`);
    });
    assert.equal(r.code, 1, r.out);
    assert.match(r.out, /order\.no-show \[en\][\s\S]*android-partner\s+Absent\./);
});

test("keys outside the webs' api subtree are not compared", () => {
    const r = run((f) => {
        f[web("cleansia.app", "en")].common = { validation: { invalid_password: "Other." } };
    });
    assert.equal(r.code, 0, r.out);
});

test("REACH: a missing client file fails, never passes silently", () => {
    const r = run((f) => {
        f[droid("partner-app", "ru")] = null;
    });
    assert.equal(r.code, 1, r.out);
    assert.match(r.out, /P0 .*partner-app.*values-ru\/strings\.xml/);
});

test("REACH: a client that yields no error text at all fails", () => {
    const r = run((f) => {
        f[IOS_CATALOG].strings = { "other.key": f[IOS_CATALOG].strings["error.validation.invalid_password"] };
    });
    assert.equal(r.code, 1, r.out);
    assert.match(r.out, /P0 ios: read no error text/);
});

test("--markdown prints the list as a table", () => {
    const r = run(
        (f) => {
            f[web("cleansia-admin.app", "uk")].api.validation.invalid_password = "Інше";
        },
        ["--markdown"],
    );
    assert.equal(r.code, 1, r.out);
    assert.match(r.out, /^\| Key \| Locale \|/m);
    assert.match(r.out, /\| `validation\.invalid_password` \| uk \|.*Інше/);
});

let failed = 0;
for (const [name, fn] of cases) {
    try {
        fn();
        console.log(`  ok   ${name}`);
    } catch (e) {
        failed++;
        console.log(`  FAIL ${name}\n       ${e.message}`);
    }
}
console.log(
    failed === 0
        ? `\ncheck-error-text-parity: ${cases.length} passed`
        : `\ncheck-error-text-parity: ${failed}/${cases.length} FAILED`,
);
process.exit(failed === 0 ? 0 : 1);
