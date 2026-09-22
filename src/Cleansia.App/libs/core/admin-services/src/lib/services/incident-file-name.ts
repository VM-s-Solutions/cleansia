/**
 * The fallback name for a served incident file when Content-Disposition carries none: the same
 * shape the server prints (ExportCustomerIncidentFile), on the UTC day, so a file never carries
 * two names.
 */
export function incidentFileName(userId: string, generatedAt: Date): string {
  const day = generatedAt.toISOString().slice(0, 10).replace(/-/g, '');
  return `incident-${userId}-${day}.pdf`;
}
