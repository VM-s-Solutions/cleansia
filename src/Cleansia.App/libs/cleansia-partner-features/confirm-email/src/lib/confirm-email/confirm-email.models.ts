/**
 * The password rule as the confirm screen lists it: the one the server's ValidatePassword enforces and
 * CustomValidators.password checks (at least 8 characters, a letter and a digit of any script), kept
 * identical to the register and reset checklists so no screen advertises a different policy.
 */
export interface PasswordCheck {
  hasLetter: boolean;
  hasNumber: boolean;
  hasMinLength: boolean;
  arePasswordsEqual?: boolean;
}

export function checkIfPasswordsValid(
  password: string,
  confirmPassword?: string
): PasswordCheck {
  return {
    hasLetter: /\p{L}/u.test(password),
    hasNumber: /\p{Nd}/u.test(password),
    hasMinLength: password.length >= 8,
    arePasswordsEqual: confirmPassword ? password === confirmPassword : false,
  };
}
