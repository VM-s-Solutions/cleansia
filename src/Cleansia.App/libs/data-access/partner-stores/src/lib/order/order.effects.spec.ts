import { TestBed } from '@angular/core/testing';
import { CompleteOrderResponse, PartnerClient } from '@cleansia/partner-services';
import { SnackbarService } from '@cleansia/services';
import { provideMockActions } from '@ngrx/effects/testing';
import { Action } from '@ngrx/store';
import { provideMockStore } from '@ngrx/store/testing';
import { TranslateService } from '@ngx-translate/core';
import { Subject, of } from 'rxjs';
import { CODE_FEATURE_KEY, codeInitialState } from '../code/code.state';
import * as OrderActions from './order.actions';
import { OrderEffects } from './order.effects';

const ORDER_ID = 'order-1';

describe('OrderEffects (partner)', () => {
  let actions$: Subject<Action>;
  let orderClient: { completeOrder: jest.Mock };

  const createEffects = (): OrderEffects => {
    TestBed.configureTestingModule({
      providers: [
        OrderEffects,
        provideMockActions(() => actions$),
        provideMockStore({ initialState: { [CODE_FEATURE_KEY]: codeInitialState } }),
        { provide: PartnerClient, useValue: { orderClient } },
        { provide: TranslateService, useValue: { instant: (key: string) => key } },
        { provide: SnackbarService, useValue: { showSuccess: jest.fn(), showError: jest.fn() } },
      ],
    });
    return TestBed.inject(OrderEffects);
  };

  const collect = (source: { subscribe: (fn: (a: Action) => void) => void }) => {
    const emitted: Action[] = [];
    source.subscribe((action) => emitted.push(action));
    return emitted;
  };

  beforeEach(() => {
    TestBed.resetTestingModule();
    actions$ = new Subject<Action>();
    orderClient = { completeOrder: jest.fn() };
  });

  describe('completeOrder$', () => {
    it('reports success for the order the action named, whatever the response body carries', () => {
      orderClient.completeOrder.mockReturnValue(
        of(CompleteOrderResponse.fromJS({ orderId: undefined, actualCompletionTime: 90 })),
      );

      const emitted = collect(createEffects().completeOrder$);
      actions$.next(
        OrderActions.completeOrder({
          orderId: ORDER_ID,
          actualCompletionTimeMinutes: 90,
          completionNotes: 'done',
        }),
      );

      expect(emitted).toEqual([
        OrderActions.completeOrderSuccess({ orderId: ORDER_ID, orderStatus: 'Completed' }),
      ]);
    });
  });
});
