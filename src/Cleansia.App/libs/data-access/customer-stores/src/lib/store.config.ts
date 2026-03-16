import { ActionReducerMap } from '@ngrx/store';
import { CustomerCatalogEffects, customerCatalogReducer, CustomerCatalogState } from './catalog';
import { customerLoadingReducer, CustomerLoadingState } from './loading';
import { CustomerUserEffects, customerUserReducer, CustomerUserState } from './user';
import { CustomerOrderEffects, customerOrderReducer, CustomerOrderState } from './order';
import { CustomerDisputeEffects, customerDisputeReducer, CustomerDisputeState } from './dispute';

export interface CustomerAppState {
  customerUser: CustomerUserState;
  customerLoading: CustomerLoadingState;
  customerCatalog: CustomerCatalogState;
  customerOrder: CustomerOrderState;
  customerDispute: CustomerDisputeState;
}

export const customerReducers: ActionReducerMap<CustomerAppState> = {
  customerUser: customerUserReducer,
  customerLoading: customerLoadingReducer,
  customerCatalog: customerCatalogReducer,
  customerOrder: customerOrderReducer,
  customerDispute: customerDisputeReducer,
};

export const customerEffects = [CustomerUserEffects, CustomerCatalogEffects, CustomerOrderEffects, CustomerDisputeEffects];
