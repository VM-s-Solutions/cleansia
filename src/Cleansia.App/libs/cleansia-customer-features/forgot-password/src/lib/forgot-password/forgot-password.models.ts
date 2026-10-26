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
