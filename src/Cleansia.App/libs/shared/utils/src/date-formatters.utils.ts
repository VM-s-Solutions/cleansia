export function formatDate(date: Date, locale: string = 'en-GB'): string {
  return date.toLocaleDateString(locale);
}
