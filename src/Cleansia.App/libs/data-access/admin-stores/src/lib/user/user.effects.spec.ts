import { HttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { AdminUserListItem } from '@cleansia/admin-services';
import { provideMockActions } from '@ngrx/effects/testing';
import { Action } from '@ngrx/store';
import { Subject } from 'rxjs';
import * as UserActions from './user.actions';
import { AdminUserEffects } from './user.effects';

describe('AdminUserEffects', () => {
  let actions$: Subject<Action>;
  let http: { request: jest.Mock };

  const createEffects = (): AdminUserEffects => {
    TestBed.configureTestingModule({
      providers: [
        AdminUserEffects,
        provideMockActions(() => actions$),
        { provide: HttpClient, useValue: http },
      ],
    });
    return TestBed.inject(AdminUserEffects);
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
    http = { request: jest.fn() };
  });

  it('unblocks the reducer with an empty record instead of calling an endpoint that is not wired', () => {
    const emitted = collect(createEffects().loadCurrent$);
    actions$.next(UserActions.loadUserCurrent());

    expect(http.request).not.toHaveBeenCalled();
    expect(emitted).toHaveLength(1);
    expect(emitted[0].type).toBe(UserActions.loadUserCurrentSuccess.type);
    expect(emitted[0]).toMatchObject({ user: expect.any(AdminUserListItem) });
  });

  it('answers every request, not just the first — the boot sequence can ask twice', () => {
    const emitted = collect(createEffects().loadCurrent$);
    actions$.next(UserActions.loadUserCurrent());
    actions$.next(UserActions.loadUserCurrent());

    expect(emitted.map((a) => a.type)).toEqual([
      UserActions.loadUserCurrentSuccess.type,
      UserActions.loadUserCurrentSuccess.type,
    ]);
  });

  it('ignores actions it does not own', () => {
    const emitted = collect(createEffects().loadCurrent$);
    actions$.next(UserActions.loadUserDetail({ id: 'user-1' }));

    expect(emitted).toEqual([]);
  });
});
