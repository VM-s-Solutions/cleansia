export interface ChangePasswordFormData {
  currentPassword: string;
  newPassword: string;
}

// Mirrors the backend ChangeOwnPassword validator policy: minimum 8
// characters, at least one letter and one digit.
export const PASSWORD_PATTERN = /^(?=.*[a-zA-Z])(?=.*\d).{8,}$/;

