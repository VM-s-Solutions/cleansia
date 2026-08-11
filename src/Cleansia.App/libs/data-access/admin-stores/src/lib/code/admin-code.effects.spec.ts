import { TestBed } from '@angular/core/testing';
import { AdminClient, Code } from '@cleansia/admin-services';
import { provideMockActions } from '@ngrx/effects/testing';
import { Action } from '@ngrx/store';
import { Subject, of, throwError } from 'rxjs';
import * as AdminCodeActions from './admin-code.actions';
import { AdminCodeEffects } from './admin-code.effects';

describe('AdminCodeEffects', () => {
  let actions$: Subject<Action>;
  let adminCodeClient: { getOverview: jest.Mock };

  const createEffects = (): AdminCodeEffects => {
    TestBed.configureTestingModule({
      providers: [
        AdminCodeEffects,
        provideMockActions(() => actions$),
        { provide: AdminClient, useValue: { adminCodeClient } },
      ],
    });
    return TestBed.inject(AdminCodeEffects);
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
    adminCodeClient = { getOverview: jest.fn() };
  });

  it('emits the codes the client returned', () => {
    const codes = [Code.fromJS({ type: 'OrderStatus', name: 'New', value: 0 })];
    adminCodeClient.getOverview.mockReturnValue(of(codes));

    const emitted = collect(createEffects().loadAdminCodes$);
    actions$.next(AdminCodeActions.loadAdminCodes());

    expect(emitted).toEqual([AdminCodeActions.loadAdminCodesSuccess({ data: codes })]);
  });

  it('emits an empty list as a success, not as a failure', () => {
    adminCodeClient.getOverview.mockReturnValue(of([]));

    const emitted = collect(createEffects().loadAdminCodes$);
    actions$.next(AdminCodeActions.loadAdminCodes());

    expect(emitted).toEqual([AdminCodeActions.loadAdminCodesSuccess({ data: [] })]);
  });

  it('maps a failed load to loadAdminCodesFailure carrying the error', () => {
    const failure = { message: 'offline' };
    adminCodeClient.getOverview.mockReturnValue(throwError(() => failure));

    const emitted = collect(createEffects().loadAdminCodes$);
    actions$.next(AdminCodeActions.loadAdminCodes());

    expect(emitted).toHaveLength(1);
    expect(emitted[0].type).toBe(AdminCodeActions.loadAdminCodesFailure.type);
    expect(emitted[0]).toMatchObject({ error: failure });
  });

  // The `catchError` lives INSIDE the mergeMap. Hoisting it to the outer pipe still compiles and
  // still reports the first failure — but the effect stream then completes and every later action
  // is dropped in silence. These codes back every admin enum dropdown, so a dead effect leaves the
  // whole app with empty selects after one transient failure.
  it('stays alive after a failure, so the retry is still served', () => {
    const codes = [Code.fromJS({ type: 'OrderStatus', name: 'New', value: 0 })];
    adminCodeClient.getOverview
      .mockReturnValueOnce(throwError(() => ({ message: 'offline' })))
      .mockReturnValueOnce(of(codes));

    const emitted = collect(createEffects().loadAdminCodes$);
    actions$.next(AdminCodeActions.loadAdminCodes());
    actions$.next(AdminCodeActions.loadAdminCodes());

    expect(emitted.map((a) => a.type)).toEqual([
      AdminCodeActions.loadAdminCodesFailure.type,
      AdminCodeActions.loadAdminCodesSuccess.type,
    ]);
  });
});
