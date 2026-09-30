export interface ChangePasswordFormData {
  currentPassword: string;
  newPassword: string;
}

// Mirrors the backend ChangeOwnPassword validator policy: at least one letter and one digit, and
// the administrator minimum of 12 characters (AdminPasswordMinLength) rather than the shared 8.
export const PASSWORD_PATTERN = /^(?=.*[a-zA-Z])(?=.*\d).{12,}$/;

