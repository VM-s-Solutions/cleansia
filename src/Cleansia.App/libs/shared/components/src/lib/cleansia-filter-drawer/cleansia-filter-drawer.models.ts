/**
 * One active filter as the chip row shows it. `controls` names the form controls a compound chip
 * clears (a date range is one chip over two controls); a plain chip clears the control named by
 * its key.
 */
export interface FilterChip {
  key: string;
  label: string;
  value: string;
  controls?: string[];
}
