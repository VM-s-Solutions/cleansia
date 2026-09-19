export function formatDate(date: Date, locale = 'en-GB'): string {
  return date.toLocaleDateString(locale);
}
