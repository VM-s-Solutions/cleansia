import { sectionHeadingsIn, withSectionNumbers } from './legal-document.models';

describe('sectionHeadingsIn', () => {
  it('lists the h2 headings in document order', () => {
    const html = '<p>Intro.</p>\n<h2>Acceptance of Terms</h2>\n<p>A.</p>\n<h2>Liability</h2>\n<p>B.</p>\n';

    expect(sectionHeadingsIn(html)).toEqual([
      { num: null, title: 'Acceptance of Terms' },
      { num: null, title: 'Liability' },
    ]);
  });

  it("splits the document's own section number off the title", () => {
    const html = '<h2>1. Prodávající</h2><h2>2 Objednávka</h2><h2>2.3 Platba</h2><h2>10. Kontakt</h2>';

    expect(sectionHeadingsIn(html)).toEqual([
      { num: '1', title: 'Prodávající' },
      { num: '2', title: 'Objednávka' },
      { num: '2.3', title: 'Platba' },
      { num: '10', title: 'Kontakt' },
    ]);
  });

  it('gives an unnumbered heading no number', () => {
    expect(sectionHeadingsIn('<h2>Contact</h2><h2>3D printing</h2>')).toEqual([
      { num: null, title: 'Contact' },
      { num: null, title: '3D printing' },
    ]);
  });

  it('reads the escaped characters the renderer wrote back as text', () => {
    expect(
      sectionHeadingsIn('<h2>Ordering &amp; Payment</h2><h2>Rights &lt;GDPR&gt; &quot;yours&quot; &#39;ok&#39;</h2>'),
    ).toEqual([
      { num: null, title: 'Ordering & Payment' },
      { num: null, title: 'Rights <GDPR> "yours" \'ok\'' },
    ]);
  });

  it('drops inline markup inside a heading', () => {
    expect(sectionHeadingsIn('<h2>6. Your <em>Rights</em> (GDPR)</h2>')).toEqual([
      { num: '6', title: 'Your Rights (GDPR)' },
    ]);
  });

  it('ignores other heading levels and an empty document', () => {
    expect(sectionHeadingsIn('<h1>Title</h1><h3>Sub</h3><p>x</p>')).toEqual([]);
    expect(sectionHeadingsIn('')).toEqual([]);
  });

  it('reads a heading that spans lines', () => {
    expect(sectionHeadingsIn('<h2>\n4. Contact\n</h2>')).toEqual([{ num: '4', title: 'Contact' }]);
  });
});

describe('withSectionNumbers', () => {
  it('wraps the leading number of each h2, without its dot', () => {
    expect(withSectionNumbers('<p>Intro.</p>\n<h2>1. Prodávající</h2>\n<p>A.</p>\n<h2>2.3 Platba</h2>\n')).toBe(
      '<p>Intro.</p>\n<h2><span class="cl-lgl__num">1</span> Prodávající</h2>\n<p>A.</p>\n' +
        '<h2><span class="cl-lgl__num">2.3</span> Platba</h2>\n',
    );
  });

  it('wraps only the leading number, not one later in the heading', () => {
    expect(withSectionNumbers('<h2>18. Withdrawal within 14 days</h2>')).toBe(
      '<h2><span class="cl-lgl__num">18</span> Withdrawal within 14 days</h2>',
    );
  });

  it('leaves h3 headings, unnumbered h2 headings and the body text alone', () => {
    const html = '<h2>Contact</h2>\n<h3>1. Sub-section</h3>\n<p>1. Not a heading</p>\n<ol>\n<li>Item</li>\n</ol>\n';

    expect(withSectionNumbers(html)).toBe(html);
  });
});
