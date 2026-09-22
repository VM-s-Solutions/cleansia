import { computed, Signal, signal, WritableSignal } from '@angular/core';
import { FormGroup } from '@angular/forms';
import { debounceTime, distinctUntilChanged, map, merge, Observable, skip, startWith, Subject, takeUntil } from 'rxjs';
import { FilterChip } from './cleansia-filter-drawer.models';

const APPLY_DEBOUNCE_MS = 500;

export interface FilterDrawerOptions<TForm extends FormGroup> {
  form: TForm;
  /** The chips for a form value. Read `lang` inside so translated labels follow the session. */
  chips: (value: ReturnType<TForm['getRawValue']>) => FilterChip[];
  /** Receives the settled form value; the facade maps it onto its query and loads. */
  apply: (value: ReturnType<TForm['getRawValue']>) => void;
  lang?: Signal<string>;
}

/**
 * The filter drawer's state, owned by a list facade and rendered by `cleansia-filter-drawer` and
 * `cleansia-filter-chips`. The form's value at construction is the reset value; a chip removal
 * restores its controls to it and applies at once, typing applies after it settles, and a value
 * equal to the last one applied is not applied again.
 */
export class FilterDrawerState<TForm extends FormGroup = FormGroup> {
  readonly isOpen = signal(false);
  readonly chips: Signal<FilterChip[]>;
  readonly count = computed(() => this.chips().length);
  readonly active = computed(() => this.count() > 0);

  private readonly defaults: ReturnType<TForm['getRawValue']>;
  private readonly value: WritableSignal<ReturnType<TForm['getRawValue']>>;
  private readonly applyNow$ = new Subject<void>();

  constructor(private readonly options: FilterDrawerOptions<TForm>) {
    this.defaults = options.form.getRawValue();
    this.value = signal(this.defaults);
    this.chips = computed(() => {
      options.lang?.();
      return options.chips(this.value());
    });
  }

  connect(destroyed$: Observable<unknown>): void {
    const form = this.options.form;
    form.valueChanges.pipe(takeUntil(destroyed$)).subscribe(() => this.value.set(form.getRawValue()));

    merge(form.valueChanges.pipe(debounceTime(APPLY_DEBOUNCE_MS)), this.applyNow$)
      .pipe(
        map(() => form.getRawValue() as ReturnType<TForm['getRawValue']>),
        startWith(this.defaults),
        distinctUntilChanged((previous, next) => JSON.stringify(previous) === JSON.stringify(next)),
        skip(1),
        takeUntil(destroyed$),
      )
      .subscribe((value) => this.options.apply(value));
  }

  open(): void {
    this.isOpen.set(true);
  }

  close(): void {
    this.isOpen.set(false);
  }

  removeChip(key: string): void {
    const controls = this.chips().find((chip) => chip.key === key)?.controls ?? [key];
    const patch: Record<string, unknown> = {};
    for (const control of controls) {
      patch[control] = (this.defaults as Record<string, unknown>)[control];
    }
    this.options.form.patchValue(patch);
    this.applyNow$.next();
  }

  reset(): void {
    this.options.form.reset(this.defaults);
    this.applyNow$.next();
  }
}
