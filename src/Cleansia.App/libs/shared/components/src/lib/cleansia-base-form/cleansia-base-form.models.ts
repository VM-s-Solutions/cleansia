export interface FormState {
  dirty: boolean;
  valid: boolean;
}

export function isFormStateEqual(current: FormState, other: FormState) {
  if (!current && !other) {
    return true;
  }

  if (current && other) {
    return current.dirty === other.dirty && current.valid === other.valid;
  }

  return false;
}

export type InputSize =
  | 'xx-small-width'
  | 'x-small-width'
  | 'small-width'
  | 'default-width'
  | 'large-width'
  | 'x-large-width'
  | 'xx-large-width'
  | 'full-width';
