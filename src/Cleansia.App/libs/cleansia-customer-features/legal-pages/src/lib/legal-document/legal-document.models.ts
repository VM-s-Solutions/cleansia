const ENTITIES: Record<string, string> = {
  '&amp;': '&',
  '&lt;': '<',
  '&gt;': '>',
  '&quot;': '"',
  '&#39;': "'",
};

const H2 = /<h2(?:\s[^>]*)?>([\s\S]*?)<\/h2>/g;
const SECTION_NUMBER = /^(\d+(?:\.\d+)*)\.?\s+/;
const NUMBERED_H2 = /(<h2(?:\s[^>]*)?>)\s*(\d+(?:\.\d+)*)\.?\s+/g;

export interface SectionHeading {
  num: string | null;
  title: string;
}

/**
 * The section titles of a served legal text, in order: its `<h2>` elements, read as text. The
 * server renders the markdown, so the rail can only learn the sections from the HTML it was given.
 * A heading's leading number is the document's own and is split off, so it is shown once.
 */
export function sectionHeadingsIn(html: string): SectionHeading[] {
  return Array.from(html.matchAll(H2), ([, inner]) => {
    const text = inner
      .replace(/<[^>]+>/g, '')
      .replace(/&(?:amp|lt|gt|quot|#39);/g, (entity) => ENTITIES[entity])
      .trim();
    const number = SECTION_NUMBER.exec(text);
    return number
      ? { num: number[1], title: text.slice(number[0].length) }
      : { num: null, title: text };
  });
}

/** The served HTML with each `<h2>`'s leading number wrapped, so it can be styled as a label. */
export function withSectionNumbers(html: string): string {
  return html.replace(NUMBERED_H2, '$1<span class="cl-lgl__num">$2</span> ');
}
