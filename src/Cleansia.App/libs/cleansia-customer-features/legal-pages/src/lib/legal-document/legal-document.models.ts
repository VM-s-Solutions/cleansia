const ENTITIES: Record<string, string> = {
  '&amp;': '&',
  '&lt;': '<',
  '&gt;': '>',
  '&quot;': '"',
  '&#39;': "'",
};

/**
 * The section titles of a served legal text, in order: its `<h2>` elements, read as text. The
 * server renders the markdown, so the rail can only learn the sections from the HTML it was given.
 */
export function sectionHeadingsIn(html: string): string[] {
  return Array.from(html.matchAll(/<h2(?:\s[^>]*)?>([\s\S]*?)<\/h2>/g), ([, inner]) =>
    inner
      .replace(/<[^>]+>/g, '')
      .replace(/&(?:amp|lt|gt|quot|#39);/g, (entity) => ENTITIES[entity])
      .trim(),
  );
}
