import { TestBed } from '@angular/core/testing';
import { Code, PartnerClient } from '@cleansia/partner-services';
import { provideMockActions } from '@ngrx/effects/testing';
import { Action } from '@ngrx/store';
import { Subject, of } from 'rxjs';
import * as CodeActions from './code.actions';
import { CodeEffects } from './code.effects';

describe('CodeEffects', () => {
  let actions$: Subject<Action>;
  let codeClient: { getOverview: jest.Mock };

  const createEffects = (): CodeEffects => {
    TestBed.configureTestingModule({
      providers: [
        CodeEffects,
        provideMockActions(() => actions$),
        { provide: PartnerClient, useValue: { codeClient } },
      ],
    });
    return TestBed.inject(CodeEffects);
  };

  /** Subscribe first: `actions$` is a Subject, so anything pushed before this is lost. */
  const collect = (source: { subscribe: (fn: (a: Action) => void) => void }) => {
    const emitted: Action[] = [];
    source.subscribe((action) => emitted.push(action));
    return emitted;
  };

  beforeEach(() => {
    TestBed.resetTestingModule();
    actions$ = new Subject<Action>();
    codeClient = { getOverview: jest.fn() };
  });

  it('emits the codes the client returned', () => {
    const codes = [Code.fromJS({ type: 'OrderStatus', name: 'New', value: 0 })];
    codeClient.getOverview.mockReturnValue(of(codes));

    const emitted = collect(createEffects().loadCodes$);
    actions$.next(CodeActions.loadCodes());

    expect(emitted).toEqual([CodeActions.loadCodesSuccess({ data: codes })]);
  });

  // The generated client emits NULL — not `[]` — for a 200 with a non-array body or a 204, and
  // `catchError` cannot see it because null is not an error. The reducer spreads the payload
  // straight into state, so an unguarded null becomes the code list every partner dropdown reads.
  it('turns a null body into an empty list rather than putting null in the store', () => {
    codeClient.getOverview.mockReturnValue(of(null));

    const emitted = collect(createEffects().loadCodes$);
    actions$.next(CodeActions.loadCodes());

    expect(emitted).toEqual([CodeActions.loadCodesSuccess({ data: [] })]);
  });
});
