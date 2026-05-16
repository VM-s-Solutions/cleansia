#!/usr/bin/env python3
"""Add the 6 backend error keys missing from frontend i18n files.

Idempotent: only inserts a key if it's absent (so re-running is safe).
Preserves JSON formatting style (4-space indent, no trailing newline change).
"""
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
APPS = [
    "src/Cleansia.App/apps/cleansia.app",
    "src/Cleansia.App/apps/cleansia-partner.app",
    "src/Cleansia.App/apps/cleansia-admin.app",
]
LANGS = ["en", "cs", "sk", "uk", "ru"]

# Translations were authored to mirror tone of adjacent existing entries
# (informal but precise, no exclamation marks, plain user-facing phrasing).
TRANSLATIONS = {
    ("gdpr", "deletion_already_pending"): {
        "en": "A deletion request is already in progress.",
        "cs": "Žádost o smazání už probíhá.",
        "sk": "Žiadosť o vymazanie už prebieha.",
        "uk": "Запит на видалення вже опрацьовується.",
        "ru": "Запрос на удаление уже в обработке.",
    },
    ("gdpr", "deletion_blocked_by_order"): {
        "en": "We can't delete your account while you have an active order.",
        "cs": "Účet nelze smazat, dokud máte aktivní objednávku.",
        "sk": "Účet nie je možné vymazať, kým máte aktívnu objednávku.",
        "uk": "Не можемо видалити обліковий запис, поки у вас є активне замовлення.",
        "ru": "Удалить аккаунт нельзя, пока есть активный заказ.",
    },
    ("gdpr", "deletion_blocked_by_invoice"): {
        "en": "We can't delete your account while you have a pending invoice.",
        "cs": "Účet nelze smazat, dokud máte nevyřízenou fakturu.",
        "sk": "Účet nie je možné vymazať, kým máte nevybavenú faktúru.",
        "uk": "Не можемо видалити обліковий запис, поки є непогашений рахунок.",
        "ru": "Удалить аккаунт нельзя, пока есть неоплаченный счёт.",
    },
    ("auth", "refresh_token_reused"): {
        "en": "Your session was used elsewhere. Please sign in again.",
        "cs": "Vaše relace byla použita jinde. Přihlaste se prosím znovu.",
        "sk": "Vaša relácia bola použitá inde. Prihláste sa, prosím, znova.",
        "uk": "Сесію використано в іншому місці. Увійдіть, будь ласка, знову.",
        "ru": "Сессия использована в другом месте. Войдите, пожалуйста, заново.",
    },
    ("order", "review", "already_exists"): {
        "en": "You've already reviewed this order.",
        "cs": "Tuto objednávku jste už hodnotili.",
        "sk": "Túto objednávku ste už hodnotili.",
        "uk": "Ви вже залишили відгук про це замовлення.",
        "ru": "Вы уже оставили отзыв по этому заказу.",
    },
    ("recurring_booking", "not_found"): {
        "en": "That recurring booking no longer exists.",
        "cs": "Tato opakovaná objednávka už neexistuje.",
        "sk": "Táto opakovaná objednávka už neexistuje.",
        "uk": "Цього регулярного замовлення більше не існує.",
        "ru": "Этого повторяющегося заказа больше не существует.",
    },
}


def upsert(node, path, value):
    """Insert value at the leaf if missing. Return True if changed."""
    *parents, leaf = path
    cur = node
    for p in parents:
        if p not in cur or not isinstance(cur[p], dict):
            cur[p] = {}
        cur = cur[p]
    if leaf in cur and isinstance(cur[leaf], str):
        return False
    cur[leaf] = value
    return True


def main():
    total_added = 0
    files_touched = 0
    for app in APPS:
        for lang in LANGS:
            path = ROOT / app / "src/assets/i18n" / f"{lang}.json"
            data = json.loads(path.read_text(encoding="utf-8"))
            added = 0
            for key, langs in TRANSLATIONS.items():
                if upsert(data, key, langs[lang]):
                    added += 1
            if added:
                # Match existing formatting: 2-space indent, CRLF line endings,
                # ensure_ascii=False so Cyrillic / Czech diacritics stay readable.
                serialized = json.dumps(data, ensure_ascii=False, indent=2) + "\n"
                serialized = serialized.replace("\r\n", "\n").replace("\n", "\r\n")
                path.write_bytes(serialized.encode("utf-8"))
                files_touched += 1
                total_added += added
                print(f"  {app}/{lang}.json: +{added} keys")
    print(f"\nTotal: {total_added} keys added across {files_touched} files.")


if __name__ == "__main__":
    main()
