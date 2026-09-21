import { signal, WritableSignal } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { Subject } from 'rxjs';
import { FilterChip } from './cleansia-filter-drawer.models';
import { FilterDrawerState } from './filter-drawer-state';

interface Fixture {
  state: FilterDrawerState<ReturnType<typeof buildForm>>;
  apply: jest.Mock;
  lang: WritableSignal<string>;
  destroyed$: Subject<void>;
  form: ReturnType<typeof buildForm>;
}

function buildForm() {
  return new FormBuilder().group({
    searchTerm: [''],
    statuses: [[] as number[]],
    from: [null as Date | null],
    to: [null as Date | null],
  });
}

function setup(): Fixture {
  const form = buildForm();
  const apply = jest.fn();
  const lang = signal('cs');
  const destroyed$ = new Subject<void>();
  const state = new FilterDrawerState({
    form,
    lang,
    chips: (value): FilterChip[] => {
      const chips: FilterChip[] = [];
      if (value.searchTerm) chips.push({ key: 'searchTerm', label: `search:${lang()}`, value: value.searchTerm });
      if (value.statuses?.length) chips.push({ key: 'statuses', label: 'status', value: value.statuses.join(',') });
      if (value.from || value.to) {
        chips.push({ key: 'range', label: 'range', value: 'r', controls: ['from', 'to'] });
      }
      return chips;
    },
    apply,
  });
  state.connect(destroyed$);
  return { state, apply, lang, destroyed$, form };
}

describe('FilterDrawerState', () => {
  beforeEach(() => jest.useFakeTimers());
  afterEach(() => jest.useRealTimers());

  it('starts closed with no chips', () => {
    const { state } = setup();
    expect(state.isOpen()).toBe(false);
    expect(state.chips()).toEqual([]);
    expect(state.count()).toBe(0);
    expect(state.active()).toBe(false);
  });

  it('opens and closes', () => {
    const { state } = setup();
    state.open();
    expect(state.isOpen()).toBe(true);
    state.close();
    expect(state.isOpen()).toBe(false);
  });

  it('rebuilds the chips on every form change, before the debounce', () => {
    const { state, form } = setup();
    form.patchValue({ searchTerm: 'anna' });
    expect(state.chips()).toEqual([{ key: 'searchTerm', label: 'search:cs', value: 'anna' }]);
    expect(state.count()).toBe(1);
    expect(state.active()).toBe(true);
  });

  it('rebuilds the chips when the language changes so their labels follow the session', () => {
    const { state, form, lang } = setup();
    form.patchValue({ searchTerm: 'anna' });
    lang.set('en');
    expect(state.chips()[0].label).toBe('search:en');
  });

  it('applies the form value once the typing settles', () => {
    const { form, apply } = setup();
    form.patchValue({ searchTerm: 'a' });
    form.patchValue({ searchTerm: 'an' });
    expect(apply).not.toHaveBeenCalled();
    jest.advanceTimersByTime(500);
    expect(apply).toHaveBeenCalledTimes(1);
    expect(apply).toHaveBeenCalledWith(expect.objectContaining({ searchTerm: 'an' }));
  });

  it('does not apply a value that equals the last one applied', () => {
    const { form, apply } = setup();
    form.patchValue({ searchTerm: 'a' });
    form.patchValue({ searchTerm: '' });
    jest.advanceTimersByTime(500);
    expect(apply).not.toHaveBeenCalled();
  });

  it('removing a chip restores its control to the default and applies at once', () => {
    const { state, form, apply } = setup();
    form.patchValue({ searchTerm: 'anna', statuses: [1, 2] });
    jest.advanceTimersByTime(500);
    apply.mockClear();

    state.removeChip('statuses');

    expect(form.value.statuses).toEqual([]);
    expect(form.value.searchTerm).toBe('anna');
    expect(apply).toHaveBeenCalledTimes(1);
    expect(apply).toHaveBeenCalledWith(expect.objectContaining({ searchTerm: 'anna', statuses: [] }));
    jest.advanceTimersByTime(500);
    expect(apply).toHaveBeenCalledTimes(1);
  });

  it('removing a compound chip restores every control it names', () => {
    const { state, form } = setup();
    form.patchValue({ from: new Date(2026, 0, 1), to: new Date(2026, 0, 31) });
    state.removeChip('range');
    expect(form.value.from).toBeNull();
    expect(form.value.to).toBeNull();
    expect(state.chips()).toEqual([]);
  });

  it('reset restores the defaults, clears the chips and applies once', () => {
    const { state, form, apply } = setup();
    form.patchValue({ searchTerm: 'anna', statuses: [3] });
    jest.advanceTimersByTime(500);
    apply.mockClear();

    state.reset();

    expect(form.value).toEqual({ searchTerm: '', statuses: [], from: null, to: null });
    expect(state.chips()).toEqual([]);
    expect(apply).toHaveBeenCalledTimes(1);
    jest.advanceTimersByTime(500);
    expect(apply).toHaveBeenCalledTimes(1);
  });

  it('stops applying once the owner is destroyed', () => {
    const { form, apply, destroyed$ } = setup();
    destroyed$.next();
    form.patchValue({ searchTerm: 'anna' });
    jest.advanceTimersByTime(500);
    expect(apply).not.toHaveBeenCalled();
  });
});
