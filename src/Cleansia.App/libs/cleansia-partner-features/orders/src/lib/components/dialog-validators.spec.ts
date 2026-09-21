import { FormControl } from '@angular/forms';
import { notBlank } from './dialog-validators';

describe('notBlank', () => {
  it('refuses text that is only whitespace', () => {
    expect(notBlank(new FormControl('   '))).toEqual({ blank: true });
    expect(notBlank(new FormControl('\n\t'))).toEqual({ blank: true });
  });

  it('accepts text with a character in it, even padded', () => {
    expect(notBlank(new FormControl('  a note '))).toBeNull();
  });

  // Emptiness is `Validators.required`'s call; this one only names the whitespace case.
  it('leaves an empty or missing value to required', () => {
    expect(notBlank(new FormControl(''))).toBeNull();
    expect(notBlank(new FormControl(null))).toBeNull();
  });
});
