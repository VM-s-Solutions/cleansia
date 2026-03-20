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
    hasLetter: /[a-zA-Z]/.test(password),
    hasNumber: /\d/.test(password),
    hasMinLength: password.length >= 8,
    arePasswordsEqual: confirmPassword ? password === confirmPassword : false,
  };
}
