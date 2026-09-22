"""Check every citation in one lawyer-analysis folder against the audited commit.

    python lawyers-docs/check-citations.py analyza-2026-09-22 6792f0c256e81e1473908308bd73881004853be4

Reads every top-level *.md in the folder and podklady/INVENTAR.md. A link is a citation when its href
resolves into src/, sql-scripts/, deploy/, .github/ or docs/ (a code citation) or into
analyza-2026-09-11/podklady/*.txt (a legal-draft citation); every other link is ignored.

  code   [File.cs:123](../../src/.../File.cs#L123)        or  [File.cs:120-131](...#L120-L131)
  draft  [VOP P028](../analyza-2026-09-11/podklady/Cleansia_VOP.txt)   (ranges: P156–P166)

Scope of a citation = the table cell it sits in (a cell holding nothing but citations and pointers — a
"citace" column — is read with its whole row) or, in prose, the sentence it closes. Identifiers of a
scope = backticked tokens (split into identifier words of 3+ characters) and numeric literals of two or
more digits (Czech thousands "1 440" read as 1440), with links and the §/R/A/S/F/P/ADR pointers removed.

Checks:
  1. the href path exists at the SHA (git show <SHA>:<path>) and the label names that file;
  2. the cited line (or range end) is within the file, and the label line equals the anchor line;
  3. if the scope has at least one identifier, one of them occurs within L-3 ... L+3 — a scope with
     none passes on 1+2 and is counted separately as "bez identifikátoru";
  4. a draft link's extract exists and contains a line starting "[Pxxx]" (both ends of a range);
  5. no table cell has more than 40 words (whitespace-separated, links included, as wc -w counts);
  6. none of the forbidden tense words outside a „verbatim quote": "od <d>. <m>.", "nově", "dosud",
     "změněno", "trvá".

Prints one line per problem and ends with:  N citací · M bez identifikátoru · K nevyřešených
Exit code 1 when K > 0.
"""
import os
import re
import subprocess
import sys

sys.stdout.reconfigure(encoding='utf-8')  # the summary line carries Czech letters on any console

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)

LINK = re.compile(r'\[([^\]]+)\]\(([^)\s]+)\)')
CODE_LABEL = re.compile(r'^(?P<name>[^\s:]+):(?P<a>\d+)(?:-(?P<b>\d+))?$')
CODE_ANCHOR = re.compile(r'^(?P<path>[^#]+)#L(?P<a>\d+)(?:-L(?P<b>\d+))?$')
DRAFT_LABEL = re.compile(r'^(?P<abbr>[A-ZŘŠ][A-Za-zŘŠřš]*) P(?P<a>\d{3})(?:[–-]P?(?P<b>\d{3}))?$')
BACKTICK = re.compile(r'`([^`]+)`')
IDENT_WORD = re.compile(r'[A-Za-z_][A-Za-z0-9_]{2,}')
NUMBER = re.compile(r'(?<![\w.])\d{2,}(?:[.,]\d+)?(?![\w])')
THOUSANDS = re.compile(r'(?<=\d)[ \u00a0\u202f](?=\d{3}\b)')
CLOCK = re.compile(r'\b\d{1,2}:\d{2}\b')  # a time of day is not an identifier (the cron behind it is)
QUOTED = re.compile(r'„[^“"]*[“"]')  # a verbatim quote from a draft keeps the draft's words
POINTER = re.compile(r'(?:§\s?\d+(?:\.\d+)*|\b[RASF]\d+\b|\bP\d{3}\b|\bADR-\d{4}\b|\bQ-WC-\d{2}\b)')
FORBIDDEN = [
    (re.compile(r'\bod \d{1,2}\. ?\d{1,2}\.'), 'od <d>. <m>.'),
    (re.compile(r'\bnově\b', re.I), 'nově'),
    (re.compile(r'\bdosud\b', re.I), 'dosud'),
    (re.compile(r'\bzměněn[oaáýé]?\b', re.I), 'změněno'),
    (re.compile(r'\btrvá\b', re.I), 'trvá'),
]
SENTENCE_SPLIT = re.compile(r'(?<=[.;!?])\s+(?=[A-ZÁ-Ž„(\[`0-9])')
MAX_CELL_WORDS = 40
CITATION_ONLY = re.compile(r'\[[^\]]+\]\([^)\s]+\)|[·,;→\s]+|\bR\d+\b|\bA[12]\b|§\s?\d+(?:\.\d+)*|\b[A-ZŘ]{2,}\b')
CODE_TREES = ('src/', 'sql-scripts/', 'deploy/', '.github/', 'docs/')
DRAFTS = 'lawyers-docs/analyza-2026-09-11/podklady/'


def split_row(line):
    """Split a table row on its cell separators: a pipe outside a `code span` and not backslash-escaped.
    A pipe inside backticks is content (a grep alternation), exactly as the renderer reads it."""
    cells, cur, in_code, i = [], [], False, 0
    while i < len(line):
        c = line[i]
        if c == '`':
            in_code = not in_code
        if c == '|' and not in_code and (i == 0 or line[i - 1] != '\\'):
            cells.append(''.join(cur))
            cur = []
        else:
            cur.append(c)
        i += 1
    cells.append(''.join(cur))
    return cells


class Checker:
    def __init__(self, sha):
        self.sha = sha
        self.files = {}
        self.problems = []
        self.total = 0
        self.without_identifier = 0

    def file_lines(self, path):
        if path not in self.files:
            r = subprocess.run(['git', 'show', f'{self.sha}:{path}'], cwd=ROOT, capture_output=True)
            self.files[path] = r.stdout.decode('utf-8', errors='replace').split('\n') if r.returncode == 0 else None
        return self.files[path]

    def problem(self, doc, lineno, text):
        self.problems.append(f'{doc}:{lineno} · {text}')

    # -- scopes -------------------------------------------------------------------------------
    @staticmethod
    def scopes_of(lines):
        """Yield (lineno, scope_text, is_cell, cell_text) for every table cell and prose sentence."""
        for i, line in enumerate(lines, 1):
            s = line.strip()
            if not s:
                continue
            if s.startswith('|'):
                if re.match(r'^\|[\s:\-|]+\|$', s):
                    continue  # separator row
                cells = [c.strip() for c in split_row(s)[1:-1]]
                row = ' '.join(cells)
                for cell in cells:
                    # a cell holding nothing but citations and pointers (a "citace" column) is read with its row
                    only_links = not CITATION_ONLY.sub('', cell).strip()
                    yield i, (row if only_links else cell), True, cell
            elif s.startswith(('- ', '* ', '> ')) or re.match(r'^\d+\. ', s) or s.startswith('#'):
                yield i, s, False, s
            else:
                for sentence in SENTENCE_SPLIT.split(s):
                    yield i, sentence, False, sentence

    @staticmethod
    def identifiers(scope):
        text = LINK.sub(' ', scope)
        text = POINTER.sub(' ', text)
        text = THOUSANDS.sub('', text)
        text = CLOCK.sub(' ', text)
        found = set()
        for tok in BACKTICK.findall(text):
            found.update(IDENT_WORD.findall(tok))
            found.update(NUMBER.findall(tok))
        text = BACKTICK.sub(' ', text)
        found.update(NUMBER.findall(text))
        return found

    # -- checks -------------------------------------------------------------------------------
    def check_code(self, doc, lineno, label, href, path, scope):
        m_label, m_href = CODE_LABEL.match(label), CODE_ANCHOR.match(href)
        if not m_label or not m_href:
            self.problem(doc, lineno, f'tvar citace: [{label}]({href})')
            return
        lines = self.file_lines(path)
        if lines is None:
            self.problem(doc, lineno, f'soubor není v commitu: {path}')
            return
        if os.path.basename(path) != m_label['name']:
            self.problem(doc, lineno, f'popisek {m_label["name"]} ≠ soubor {os.path.basename(path)}')
        a, b = int(m_href['a']), int(m_href['b'] or m_href['a'])
        if (m_label['a'], m_label['b'] or m_label['a']) != (m_href['a'], m_href['b'] or m_href['a']):
            self.problem(doc, lineno, f'popisek {label} ≠ kotva {href[href.index("#"):]}')
        length = len(lines) - (1 if lines and lines[-1] == '' else 0)
        if b > length or a < 1 or b < a:
            self.problem(doc, lineno, f'řádek {a}-{b} mimo soubor ({length} řádků): {path}')
            return
        idents = self.identifiers(scope)
        if not idents:
            self.without_identifier += 1
            return
        window = '\n'.join(lines[max(0, a - 4):min(length, b + 3)])
        window_norm = window.replace('_', '')
        if not any(t in window or t in window_norm for t in idents):
            self.problem(doc, lineno, f'žádný identifikátor {sorted(idents)} u {label} (řádky {max(1, a - 3)}-{b + 3})')

    def check_draft(self, doc, lineno, label, href, path):
        m = DRAFT_LABEL.match(label)
        if not m:
            self.problem(doc, lineno, f'tvar odkazu na návrh: [{label}]({href})')
            return
        lines = self.file_lines(path)
        if lines is None:
            self.problem(doc, lineno, f'extrakce není v commitu: {path}')
            return
        for p in {m['a'], m['b'] or m['a']}:
            if not any(l.startswith(f'[P{p}]') for l in lines):
                self.problem(doc, lineno, f'[P{p}] není v {os.path.basename(path)}')

    def check_doc(self, doc_path, base):
        """base = the document's folder relative to the repository root (hrefs resolve against it)."""
        doc = os.path.relpath(doc_path, HERE).replace('\\', '/')
        lines = open(doc_path, encoding='utf-8').read().split('\n')
        for lineno, scope, is_cell, cell in self.scopes_of(lines):
            if is_cell and len(cell.split()) > MAX_CELL_WORDS:
                self.problem(doc, lineno, f'buňka má {len(cell.split())} slov (> {MAX_CELL_WORDS}): {cell[:60]}…')
            for rx, name in FORBIDDEN:
                if rx.search(QUOTED.sub(' ', LINK.sub(' ', cell))):
                    self.problem(doc, lineno, f'zakázané slovo „{name}": {cell[:80]}…')
            for label, href in LINK.findall(cell):
                if '://' in href or href.startswith('#'):
                    continue
                path = os.path.normpath(os.path.join(base, href.split('#')[0])).replace('\\', '/')
                if path.startswith(CODE_TREES):
                    self.total += 1
                    if '#L' not in href:
                        self.problem(doc, lineno, f'chybí kotva #L: [{label}]({href})')
                        continue
                    self.check_code(doc, lineno, label, href, path, scope)
                elif path.startswith(DRAFTS) and path.endswith('.txt'):
                    if label == os.path.basename(path):
                        continue  # a link to the extract itself (the inventory), not a paragraph citation
                    self.total += 1
                    self.check_draft(doc, lineno, label, href, path)


def main(argv):
    if len(argv) != 3:
        sys.exit(__doc__)
    folder = os.path.join(HERE, argv[1])
    if not os.path.isdir(folder):
        sys.exit(f'no such folder under lawyers-docs: {argv[1]}')
    checker = Checker(argv[2])
    base = f'lawyers-docs/{argv[1]}'
    for name in sorted(f for f in os.listdir(folder) if f.endswith('.md')):
        checker.check_doc(os.path.join(folder, name), base)
    inventory = os.path.join(folder, 'podklady', 'INVENTAR.md')
    if os.path.exists(inventory):
        checker.check_doc(inventory, base + '/podklady')
    for p in checker.problems:
        print(p)
    print(f'{checker.total} citací · {checker.without_identifier} bez identifikátoru · {len(checker.problems)} nevyřešených')
    sys.exit(1 if checker.problems else 0)


if __name__ == '__main__':
    main(sys.argv)
