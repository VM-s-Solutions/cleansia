import { FormControl } from '@angular/forms';
import { CustomValidators } from './custom-validators';

/**
 * The server's password rule (ValidationExtensions.ValidatePassword), with its length counted in UTF-16
 * units as the server and every rule list on the web count it. The `u` flag that `\p{L}` needs counts
 * an emoji once, so a pattern carrying it refused Heslo1😀 while every rule showed green.
 */
describe('CustomValidators.password', () => {
  const errorsFor = (password: string, minLength?: number) =>
    new FormControl(password, minLength === undefined ? CustomValidators.password() : CustomValidators.password(minLength))
      .errors;

  it.each(['Heslo123', 'Пароль2024', 'ěščřžýáí1', 'Heslo1😀', 'Heslo\u0661\u0662\u0663'])('takes %p', (password) => {
    expect(errorsFor(password)).toBeNull();
  });

  it.each([
    ['seven characters', 'Heslo12'],
    ['seven characters, an emoji counted twice', 'Hesl1😀'],
    ['no letter', '12345678'],
    ['no letter but emoji', '😀😀😀😀1'],
    ['no digit', 'Heslooooo'],
    ['a number that is no digit', 'Heslooo\u00bd'],
  ])('refuses %s as the pattern error the field already reads', (_label, password) => {
    expect(errorsFor(password)).toEqual({ pattern: true });
  });

  it('leaves an empty field to Validators.required', () => {
    expect(errorsFor('')).toBeNull();
  });

  it('asks an administrator for twelve, counted the same way', () => {
    expect(errorsFor('Heslo12345😀', 12)).toBeNull();
    expect(errorsFor('Heslo1234😀', 12)).toEqual({ pattern: true });
  });
});
