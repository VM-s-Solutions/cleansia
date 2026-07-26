/**
 * Mirrors the only password rule the backend actually enforces
 * (ValidationExtensions.ValidatePassword, and PASSWORD_PATTERN in
 * @cleansia/services): at least 8 characters, one letter and one digit.
 * Kept identical to the partner register checklist so the two screens cannot
 * advertise different policies.
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
    hasLetter: /[a-zA-Z]/.test(password),
    hasNumber: /\d/.test(password),
    hasMinLength: password.length >= 8,
    arePasswordsEqual: confirmPassword ? password === confirmPassword : false,
  };
}
