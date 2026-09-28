#!/usr/bin/env node
/**
 * The production launch gate for legal texts: refuses while any legal text in force today, or dated
 * to come into force later, still carries the "Návrh" draft banner, in any locale.
 *
 * The seed tree is `src/Cleansia.Infra.Database/Seed/Legal/{audience}/{type}/{market}/{yyyy-MM-dd}/{lang}.md`
 * and every host start seeds all of it, in every environment. For each audience, document type and
 * market folder, the version in force is the newest effective date on or before today; that version's
 * files are the ones a customer would be shown and asked to accept. A version dated after today is
 * seeded by the same deploy and comes into force on its date with no deploy in between, so its texts
 * are held to the same rule.
 *
 * It runs in deploy-pro.yml only. Drafts are expected on every other branch and environment until the
 * lawyer delivers, so it is deliberately not a pull-request check.
 *
 * An empty scan is a failure, never a pass: no seed tree, or no version in force anywhere, means the
 * gate read nothing and cannot vouch for anything.
 *
 * Usage:
 *   node agents/tools/check-legal-drafts.mjs                    # today in UTC
 *   node agents/tools/check-legal-drafts.mjs --today=2026-12-01 # a chosen day
 *   node agents/tools/check-legal-drafts.mjs --root=DIR         # another tree (used by the self-test)
 */
import { existsSync, readdirSync, readFileSync, statSync } from "node:fs";
import { join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const args = process.argv.slice(2);
const argValue = (name) => (args.find((a) => a.startsWith(`--${name}=`)) || "").split("=")[1];
const REPO = argValue("root")
    ? resolve(argValue("root"))
    : resolve(join(fileURLToPath(import.meta.url), "..", "..", ".."));
const LEGAL = join(REPO, "src/Cleansia.Infra.Database/Seed/Legal");
const DATE = /^\d{4}-\d{2}-\d{2}$/;

// The banner each locale opens a draft with: cs and sk "Návrh", en "Draft", ru "Черновик", uk "Чернетка".
const DRAFT_BANNER = /^>\s*(?:Návrh|Draft|Черновик|Чернетка)\s+—/m;

const today = argValue("today") ?? new Date().toISOString().slice(0, 10);
if (!DATE.test(today)) {
    console.error(`check-legal-drafts: --today must be yyyy-MM-dd, got '${today}'.`);
    process.exit(1);
}

const dirs = (path) => readdirSync(path).filter((name) => statSync(join(path, name)).isDirectory()).sort();

if (!existsSync(LEGAL)) {
    console.error(`check-legal-drafts: no legal seed tree at ${LEGAL} — nothing was checked.`);
    process.exit(1);
}

const drafts = [];
const notYetInForce = [];
let versionsInForce = 0;
let versionsUpcoming = 0;
let filesRead = 0;

function readVersion(versionDir, label) {
    const texts = readdirSync(versionDir).filter((name) => name.endsWith(".md")).sort();
    for (const text of texts) {
        filesRead++;
        if (DRAFT_BANNER.test(readFileSync(join(versionDir, text), "utf8"))) {
            drafts.push(`${label}/${text}`);
        }
    }
    return texts.length;
}

for (const audience of dirs(LEGAL)) {
    for (const type of dirs(join(LEGAL, audience))) {
        for (const market of dirs(join(LEGAL, audience, type))) {
            const marketDir = join(LEGAL, audience, type, market);
            const versions = dirs(marketDir).filter((date) => DATE.test(date));
            const inForce = versions.filter((date) => date <= today).at(-1);
            const document = `${audience}/${type}/${market}`;

            for (const upcoming of versions.filter((date) => date > today)) {
                versionsUpcoming++;
                readVersion(join(marketDir, upcoming), `${document}/${upcoming}`);
            }

            if (!inForce) {
                notYetInForce.push(document);
                continue;
            }

            if (readVersion(join(marketDir, inForce), `${document}/${inForce}`) === 0) {
                console.error(`check-legal-drafts: ${document}/${inForce} is in force and holds no text.`);
                process.exit(1);
            }
            versionsInForce++;
        }
    }
}

if (versionsInForce === 0) {
    console.error(`check-legal-drafts: no legal document is in force on ${today} — nothing was checked.`);
    process.exit(1);
}

for (const draft of drafts) {
    console.log(`  DRAFT  ${draft}  still carries the draft banner`);
}
for (const document of notYetInForce) {
    console.log(`  note   ${document}  has no version in force on ${today}`);
}
console.log(
    `check-legal-drafts: read ${versionsInForce} version(s) in force on ${today} and ${versionsUpcoming} upcoming, ` +
        `${filesRead} text(s); ${drafts.length} draft text(s).`,
);

if (drafts.length > 0) {
    console.log(
        "Production must not launch on unreviewed legal texts. A version in force needs a new effective-date " +
            "version without the banner; an upcoming one needs the banner removed before this deploy seeds it.",
    );
    process.exit(1);
}
