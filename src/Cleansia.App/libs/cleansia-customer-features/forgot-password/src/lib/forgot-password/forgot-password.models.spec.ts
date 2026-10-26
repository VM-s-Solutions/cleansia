import { checkIfPasswordsValid } from './forgot-password.models';

describe('checkIfPasswordsValid (customer forgot-password)', () => {
  it('counts a letter of any alphabet, as the server does', () => {
    expect(checkIfPasswordsValid('Пароль2024').hasLetter).toBe(true);
    expect(checkIfPasswordsValid('ěščřžýáí1').hasLetter).toBe(true);
    expect(checkIfPasswordsValid('12345678').hasLetter).toBe(false);
  });
});
