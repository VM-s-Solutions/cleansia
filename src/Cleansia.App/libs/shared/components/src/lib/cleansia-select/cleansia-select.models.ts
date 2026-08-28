export interface ICleansiaSelectOption {
  label: string;
  // Deliberately `any`, and recorded rather than left for the next reader to re-litigate.
  //
  // A select option's value is polymorphic by nature — this one interface carries country ISO
  // codes, enum ints, GUID ids and page sizes across 25+ call sites, and PrimeNG's own SelectItem
  // types it the same way for the same reason. `unknown` would be the type-safe choice but it is
  // not assignable without a cast at every consumer, so it moves the `any` outwards and multiplies
  // it. Making the interface generic (`ICleansiaSelectOption<T = unknown>`) is the real fix and is
  // a design-system-wide change: every consumer is written bare today, so the default would have to
  // be `unknown` and each one re-typed. Worth doing deliberately, not as a lint cleanup.
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  value: any;
  disabled?: boolean;
}
