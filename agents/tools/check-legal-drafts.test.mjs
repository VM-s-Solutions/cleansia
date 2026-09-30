#!/usr/bin/env node
/**
 * Self-test for the legal launch gate (`check-legal-drafts.mjs`). Each scenario builds a throwaway
 * seed tree with the real shape and runs the gate against it on a fixed day, so the result never
 * depends on the calendar or on the texts in the repository. Stub the gate to exit 0 and every
 * must-fail scenario goes red.
 *
 *   node agents/tools/check-legal-drafts.test.mjs
 */
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { spawnSync } from "node:child_process";
import { dirname, join } from "node:path";
import { tmpdir } from "node:os";
import { fileURLToPath } from "node:url";

const TOOL = join(dirname(fileURLToPath(import.meta.url)), "check-legal-drafts.mjs");
const SEED = "src/Cleansia.Infra.Database/Seed/Legal";
const TODAY = "2026-10-01";
const LOCALES = ["cs", "en", "ru", "sk", "uk"];

const BANNERS = {
    cs: "> Návrh — toto znění zatím neprošlo právní kontrolou a není konečnou závaznou verzí.",
    en: "> Draft — this wording has not yet been through legal review and is not the final, binding version.",
    ru: "> Черновик — этот текст ещё не прошёл юридическую проверку и не является окончательной обязательной редакцией.",
    sk: "> Návrh — toto znenie zatiaľ neprešlo právnou kontrolou a nie je konečnou záväznou verziou.",
    uk: "> Чернетка — цей текст ще не пройшов юридичну перевірку і не є остаточною обов’язковою редакцією.",
};

const text = (lang, draft, body = "Body of the document.") =>
    `---\ntitle: Title ${lang}\n---\n\n${draft ? `${BANNERS[lang]}\n\n` : ""}${body}\n`;

/** Writes one version: every locale, drafts in the listed locales only. */
function version(root, path, { draftIn = [], body } = {}) {
    for (const lang of LOCALES) {
        const file = join(root, SEED, path, `${lang}.md`);
        mkdirSync(dirname(file), { recursive: true });
        writeFileSync(file, text(lang, draftIn.includes(lang), body));
    }
}

/** The shape of the real tree, all reviewed: three customer documents, one market folder each. */
function cleanTree(root) {
    version(root, "customer/terms-of-service/any/2026-09-14");
    version(root, "customer/privacy-policy/any/2026-09-14");
    version(root, "customer/work-contract/any/2026-09-20");
}

let failed = 0;
function scenario(name, { build, today = TODAY, expectExit, expectText = [], rejectText = [] }) {
    const root = mkdtempSync(join(tmpdir(), "legal-drafts-"));
    try {
        build(root);
        const r = spawnSync(process.execPath, [TOOL, `--root=${root}`, `--today=${today}`], { encoding: "utf8" });
        const out = `${r.stdout}${r.stderr}`;
        const okExit = r.status === expectExit;
        const okText = expectText.every((t) => out.includes(t));
        const okReject = rejectText.every((t) => !out.includes(t));
        if (okExit && okText && okReject) {
            console.log(`  PASS  ${name}`);
        } else {
            failed++;
            console.log(`  FAIL  ${name}`);
            console.log(`        expected exit ${expectExit}, got ${r.status}`);
            if (!okText) console.log(`        expected output to contain: ${expectText.join(" | ")}`);
            if (!okReject) console.log(`        expected output NOT to contain: ${rejectText.join(" | ")}`);
            console.log(out.split("\n").map((l) => `        > ${l}`).join("\n"));
        }
    } finally {
        rmSync(root, { recursive: true, force: true });
    }
}

console.log("check-legal-drafts self-test:");

scenario("reviewed texts in force pass and the summary states what was read", {
    build: cleanTree,
    expectExit: 0,
    expectText: ["read 3 version(s) in force on 2026-10-01 and 0 upcoming, 15 text(s); 0 draft text(s)"],
});

scenario("a draft in force fails and names every draft file", {
    build: (r) => {
        cleanTree(r);
        version(r, "customer/terms-of-service/any/2026-09-27", { draftIn: LOCALES });
    },
    expectExit: 1,
    expectText: [
        "customer/terms-of-service/any/2026-09-27/cs.md",
        "customer/terms-of-service/any/2026-09-27/uk.md",
        "5 draft text(s)",
    ],
});

for (const lang of LOCALES) {
    scenario(`the banner in ${lang} alone fails`, {
        build: (r) => {
            cleanTree(r);
            version(r, "customer/privacy-policy/any/2026-09-30", { draftIn: [lang] });
        },
        expectExit: 1,
        expectText: [`customer/privacy-policy/any/2026-09-30/${lang}.md`, "1 draft text(s)"],
    });
}

scenario("a superseded draft no longer counts", {
    build: (r) => {
        version(r, "customer/terms-of-service/any/2026-09-01", { draftIn: LOCALES });
        version(r, "customer/terms-of-service/any/2026-09-20");
    },
    expectExit: 0,
    rejectText: ["2026-09-01"],
});

scenario("a version dated today is the one in force", {
    build: (r) => {
        cleanTree(r);
        version(r, `customer/work-contract/any/${TODAY}`, { draftIn: ["cs"] });
    },
    expectExit: 1,
    expectText: [`customer/work-contract/any/${TODAY}/cs.md`],
});

scenario("a draft dated after today fails: this deploy seeds it and it comes into force on its date", {
    build: (r) => {
        cleanTree(r);
        version(r, "customer/terms-of-service/any/2026-10-02", { draftIn: LOCALES });
    },
    expectExit: 1,
    expectText: [
        "customer/terms-of-service/any/2026-10-02/cs.md",
        "customer/terms-of-service/any/2026-10-02/uk.md",
        "and 1 upcoming",
        "5 draft text(s)",
    ],
});

scenario("a reviewed version dated after today passes", {
    build: (r) => {
        cleanTree(r);
        version(r, "customer/terms-of-service/any/2026-10-02");
    },
    expectExit: 0,
    expectText: ["and 1 upcoming", "0 draft text(s)"],
});

scenario("each market folder is judged on its own", {
    build: (r) => {
        cleanTree(r);
        version(r, "customer/terms-of-service/CZE/2026-09-15", { draftIn: ["cs"] });
    },
    expectExit: 1,
    expectText: ["customer/terms-of-service/CZE/2026-09-15/cs.md"],
});

scenario("the word Návrh in running text is not the banner", {
    build: (r) => {
        cleanTree(r);
        version(r, "customer/work-contract/any/2026-09-25", {
            body: "Návrh smlouvy předkládá zhotovitel.\n\n> Poznámka — Návrh není banner, pokud nezačíná citaci.",
        });
    },
    expectExit: 0,
});

scenario("a market with nothing in force yet is noted, not failed", {
    build: (r) => {
        cleanTree(r);
        version(r, "customer/privacy-policy/SVK/2026-11-01");
    },
    expectExit: 0,
    expectText: ["customer/privacy-policy/SVK  has no version in force on 2026-10-01"],
});

scenario("a market whose only version is a draft still to come fails", {
    build: (r) => {
        cleanTree(r);
        version(r, "customer/privacy-policy/SVK/2026-11-01", { draftIn: ["sk"] });
    },
    expectExit: 1,
    expectText: ["customer/privacy-policy/SVK/2026-11-01/sk.md", "1 draft text(s)"],
});

scenario("no seed tree fails instead of passing an empty scan", {
    build: () => {},
    expectExit: 1,
    expectText: ["no legal seed tree"],
});

scenario("nothing in force anywhere fails instead of passing an empty scan", {
    build: (r) => version(r, "customer/terms-of-service/any/2026-12-01"),
    expectExit: 1,
    expectText: ["no legal document is in force on 2026-10-01"],
});

scenario("a version in force with no text fails", {
    build: (r) => {
        cleanTree(r);
        mkdirSync(join(r, SEED, "customer/terms-of-service/any/2026-09-30"), { recursive: true });
    },
    expectExit: 1,
    expectText: ["customer/terms-of-service/any/2026-09-30 is in force and holds no text"],
});

scenario("a malformed --today is refused", {
    build: cleanTree,
    today: "01.10.2026",
    expectExit: 1,
    expectText: ["--today must be yyyy-MM-dd"],
});

if (failed > 0) {
    console.log(`\n${failed} scenario(s) failed.`);
    process.exit(1);
}
console.log("\nAll scenarios passed.");
