"""Render one lawyer-analysis folder: every top-level *.md -> HTML (the stylesheet the 09-11 set
introduced, read from analyza-2026-09-11/SHRNUTI.html) -> PDF through Edge headless.

    python lawyers-docs/render.py analyza-2026-09-16                    # every document in the folder
    python lawyers-docs/render.py analyza-2026-09-16 PROVOZ-PLATFORMY   # one document

Needs the `markdown` package (pip install markdown) and Microsoft Edge. Documents are ordered by
DOCS first, then alphabetically; the nav links every document, its PDF and podklady/INVENTAR.html,
which is rendered too (HTML only, no PDF) when podklady/INVENTAR.md exists and no single document
was asked for.
"""
import html
import os
import re
import subprocess
import sys
import time

import markdown

HERE = os.path.dirname(os.path.abspath(__file__))
EDGE = r'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe'
CSS_SOURCE = os.path.join(HERE, 'analyza-2026-09-11', 'SHRNUTI.html')

# Known documents in reading order with their nav labels; anything else in the folder follows.
DOCS = [
    ('PROVOZ-PLATFORMY', 'Jak platforma funguje'),
    ('ROZPORY-DOKUMENTY-VS-KOD', 'Dokumenty vs. aplikace'),
    ('TECHNICKE-MEZERY', 'Technické mezery'),
    ('OBCHODNI-MEZERY-A-HRANICNI-PRIPADY', 'Obchodní mezery a hraniční případy'),
    ('AUDIT-LOG', 'Záznam auditu'),
    ('SHRNUTI', 'Shrnutí'),
    ('PODROBNY-ROZBOR', 'Podrobný rozbor'),
]


def title_of(md):
    m = re.search(r'^#\s+(.+)$', md, re.M)
    return re.sub(r'[*`_]', '', m.group(1)).strip() if m else 'CleanSia'


def documents_in(folder):
    present = {f[:-3] for f in os.listdir(folder) if f.endswith('.md')}
    known = [(n, label) for n, label in DOCS if n in present]
    rest = sorted(present - {n for n, _ in DOCS})
    return known + [(n, n.replace('-', ' ').capitalize()) for n in rest]


def nav_for(folder, docs, prefix):
    nav = ''.join(f'<a href="{prefix}{n}.html">{label}</a>' for n, label in docs)
    nav += ''.join(f'<a href="{prefix}{n}.pdf">{label} (PDF)</a>' for n, label in docs)
    if os.path.exists(os.path.join(folder, 'podklady', 'INVENTAR.md')):
        nav += f'<a href="{prefix}podklady/INVENTAR.html">Inventář podkladů</a>'
    return nav


def page_for(md, nav, css):
    body = markdown.markdown(md, extensions=['tables', 'toc', 'sane_lists', 'fenced_code'], output_format='html5')
    return ('<!doctype html>\n<html lang="cs"><head><meta charset="utf-8">'
            '<meta name="viewport" content="width=device-width, initial-scale=1">'
            f'<title>{html.escape(title_of(md))}</title>{css}</head><body><main><nav>{nav}</nav>{body}</main></body></html>')


def print_pdf(dst, pdf):
    if os.path.exists(pdf):
        os.remove(pdf)
    subprocess.run([EDGE, '--headless', '--disable-gpu', '--no-pdf-header-footer',
                    '--run-all-compositor-stages-before-draw', '--virtual-time-budget=10000',
                    f'--print-to-pdf={pdf}', 'file:///' + dst.replace('\\', '/')],
                   check=False, timeout=180, capture_output=True)
    for _ in range(120):  # an open Edge hands the print to the running instance and returns early
        if os.path.exists(pdf) and os.path.getsize(pdf) > 0:
            break
        time.sleep(1)
    return os.path.getsize(pdf) if os.path.exists(pdf) else 'MISSING'


def render(folder, name, docs, css):
    md = open(os.path.join(folder, name + '.md'), encoding='utf-8').read()
    dst = os.path.join(folder, name + '.html')
    open(dst, 'w', encoding='utf-8', newline='\n').write(page_for(md, nav_for(folder, docs, ''), css))
    print(name, 'html', os.path.getsize(dst), 'pdf', print_pdf(dst, os.path.join(folder, name + '.pdf')))


def render_inventory(folder, docs, css):
    src = os.path.join(folder, 'podklady', 'INVENTAR.md')
    if not os.path.exists(src):
        return
    md = open(src, encoding='utf-8').read()
    dst = os.path.join(folder, 'podklady', 'INVENTAR.html')
    open(dst, 'w', encoding='utf-8', newline='\n').write(page_for(md, nav_for(folder, docs, '../'), css))
    print('podklady/INVENTAR html', os.path.getsize(dst))


def main(argv):
    if len(argv) < 2:
        sys.exit(__doc__)
    folder = os.path.join(HERE, argv[1])
    if not os.path.isdir(folder):
        sys.exit(f'no such folder under lawyers-docs: {argv[1]}')
    source = open(CSS_SOURCE, encoding='utf-8').read()
    css = source[source.find('<style>'):source.find('</style>') + len('</style>')]
    docs = documents_in(folder)
    for name in argv[2:] or [n for n, _ in docs]:
        render(folder, name, docs, css)
    if not argv[2:]:
        render_inventory(folder, docs, css)


if __name__ == '__main__':
    main(sys.argv)
