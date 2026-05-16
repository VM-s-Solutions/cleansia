import { MyProfileDto } from '@cleansia/customer-services';

export const CUSTOMER_USER_FEATURE_KEY = 'customerUser';

export interface CustomerUserState {
  currentUser?: MyProfileDto;
  loading: Record<string, boolean>;
  error: Record<string, string | null>;
}

export const customerUserInitialState: CustomerUserState = {
  currentUser: undefined,
  loading: {},
  error: {},
};
