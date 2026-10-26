export interface ChangePasswordFormData {
  currentPassword: string;
  newPassword: string;
}

// The backend ChangeOwnPassword policy: the shared password rule with the administrator minimum
// (AdminPasswordMinLength) rather than the shared 8.
export const ADMIN_PASSWORD_MIN_LENGTH = 12;

