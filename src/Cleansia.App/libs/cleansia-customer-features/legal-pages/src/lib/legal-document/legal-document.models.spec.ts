import { sectionHeadingsIn } from './legal-document.models';

describe('sectionHeadingsIn', () => {
  it('lists the h2 headings in document order', () => {
    const html = '<p>Intro.</p>\n<h2>Acceptance of Terms</h2>\n<p>A.</p>\n<h2>Liability</h2>\n<p>B.</p>\n';

    expect(sectionHeadingsIn(html)).toEqual(['Acceptance of Terms', 'Liability']);
  });

  it('reads the escaped characters the renderer wrote back as text', () => {
    expect(sectionHeadingsIn('<h2>Ordering &amp; Payment</h2><h2>Rights &lt;GDPR&gt; &quot;yours&quot; &#39;ok&#39;</h2>')).toEqual([
      'Ordering & Payment',
      'Rights <GDPR> "yours" \'ok\'',
    ]);
  });

  it('drops inline markup inside a heading', () => {
    expect(sectionHeadingsIn('<h2>Your <em>Rights</em> (GDPR)</h2>')).toEqual(['Your Rights (GDPR)']);
  });

  it('ignores other heading levels and an empty document', () => {
    expect(sectionHeadingsIn('<h1>Title</h1><h3>Sub</h3><p>x</p>')).toEqual([]);
    expect(sectionHeadingsIn('')).toEqual([]);
  });

  it('reads a heading that spans lines', () => {
    expect(sectionHeadingsIn('<h2>\nContact\n</h2>')).toEqual(['Contact']);
  });
});
