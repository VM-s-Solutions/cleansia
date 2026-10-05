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

export interface ReferralCopy {
  key: string;
  params: Record<string, unknown>;
}

/** The dialog's helper line: the market's credit when it pays one, otherwise no amount at all. */
export function referralHelperCopy(amount: string | null): ReferralCopy {
  return amount
    ? { key: 'auth.register.referral.dialog_helper', params: { amount } }
    : { key: 'auth.register.referral.dialog_helper_no_amount', params: {} };
}

/** The accepted-code message, naming the referrer when the server sent a first name. */
export function referralSuccessCopy(referrerFirstName: string | null, amount: string | null): ReferralCopy {
  const name = referrerFirstName?.trim();
  if (amount) {
    return name
      ? { key: 'auth.register.referral.dialog_success_named', params: { name, amount } }
      : { key: 'auth.register.referral.dialog_success', params: { amount } };
  }
  return name
    ? { key: 'auth.register.referral.dialog_success_named_no_amount', params: { name } }
    : { key: 'auth.register.referral.dialog_success_no_amount', params: {} };
}
