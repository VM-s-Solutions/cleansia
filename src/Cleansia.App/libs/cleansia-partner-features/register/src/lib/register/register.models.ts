export interface PasswordCheck {
  hasLowerCase: boolean;
  hasUpperCase: boolean;
  hasNumber: boolean;
  hasMinLength: boolean;
  hasSpecialCharacter: boolean;
  arePasswordsEqual?: boolean;
}

export function checkIfPasswordsValid(
  password: string,
  confirmPassword?: string
) {
  return {
    hasLowerCase: /[a-z]/.test(password),
    hasUpperCase: /[A-Z]/.test(password),
    hasNumber: /\d/.test(password),
    hasMinLength: password.length >= 12,
    hasSpecialCharacter: /[@$!%*?&#^()]/.test(password),
    arePasswordsEqual: confirmPassword ? password === confirmPassword : false,
  } as PasswordCheck;
}
