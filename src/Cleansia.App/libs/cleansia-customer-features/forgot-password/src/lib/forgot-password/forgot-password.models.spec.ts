import { checkIfPasswordsValid } from './forgot-password.models';

describe('checkIfPasswordsValid (customer forgot-password)', () => {
  it('counts a letter of any alphabet, as the server does', () => {
    expect(checkIfPasswordsValid('Пароль2024').hasLetter).toBe(true);
    expect(checkIfPasswordsValid('ěščřžýáí1').hasLetter).toBe(true);
    expect(checkIfPasswordsValid('12345678').hasLetter).toBe(false);
  });

  it('counts a digit of any script, as the server does', () => {
    expect(checkIfPasswordsValid('Heslo\u0661\u0662\u0663').hasNumber).toBe(true);
  });

  it('counts the length as the server and the form do, an emoji as two characters', () => {
    expect(checkIfPasswordsValid('Heslo1😀').hasMinLength).toBe(true);
  });
});
