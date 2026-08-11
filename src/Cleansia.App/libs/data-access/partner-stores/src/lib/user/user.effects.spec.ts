import { HttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { UserFilter } from '@cleansia/models';
import {
  IUserClient,
  MyProfileDto,
  PagedDataOfUserListItem,
  PartnerClient,
  SortDefinition,
  UserClient,
  UserItem,
} from '@cleansia/partner-services';
import { provideMockActions } from '@ngrx/effects/testing';
import { Action } from '@ngrx/store';
import { EMPTY, Subject, of, throwError } from 'rxjs';
import * as UserActions from './user.actions';
import { UserEffects } from './user.effects';

const USER_ID = 'user-1';

describe('UserEffects (partner)', () => {
  let actions$: Subject<Action>;
  let userClient: {
    getPaged: jest.Mock;
    getCurrent: jest.Mock;
    getById: jest.Mock;
  };

  const createEffects = (
    client: { userClient: Partial<IUserClient> } = { userClient },
  ): UserEffects => {
    TestBed.configureTestingModule({
      providers: [
        UserEffects,
        provideMockActions(() => actions$),
        { provide: PartnerClient, useValue: client },
      ],
    });
    return TestBed.inject(UserEffects);
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
    userClient = {
      getPaged: jest.fn(),
      getCurrent: jest.fn(),
      getById: jest.fn(),
    };
  });

  describe('loadPaged$', () => {
    it('emits the page on success', () => {
      const page = PagedDataOfUserListItem.fromJS({
        pageNumber: 2,
        pageSize: 20,
        total: 41,
        data: [{ id: USER_ID }],
      });
      userClient.getPaged.mockReturnValue(of(page));

      const emitted = collect(createEffects().loadPaged$);
      actions$.next(UserActions.loadUserPaged({}));

      expect(emitted).toEqual([UserActions.loadUserPagedSuccess({ page })]);
    });

    it('maps a client failure to loadUserPagedFailure carrying the error, never swallowing it', () => {
      const failure = { message: 'boom' };
      userClient.getPaged.mockReturnValue(throwError(() => failure));

      const emitted = collect(createEffects().loadPaged$);
      actions$.next(UserActions.loadUserPaged({}));

      expect(emitted).toHaveLength(1);
      expect(emitted[0].type).toBe(UserActions.loadUserPagedFailure.type);
      expect(emitted[0]).toMatchObject({ error: failure });
    });

    // The `catchError` lives INSIDE the mergeMap. Hoisting it to the outer pipe still compiles and
    // still reports the first failure — but the effect stream then completes and every later action
    // is dropped in silence.
    it('stays alive after a failure, so the next action is still served', () => {
      const page = PagedDataOfUserListItem.fromJS({ pageNumber: 1, pageSize: 20, total: 0, data: [] });
      userClient.getPaged
        .mockReturnValueOnce(throwError(() => ({ message: 'boom' })))
        .mockReturnValueOnce(of(page));

      const emitted = collect(createEffects().loadPaged$);
      actions$.next(UserActions.loadUserPaged({}));
      actions$.next(UserActions.loadUserPaged({}));

      expect(emitted.map((a) => a.type)).toEqual([
        UserActions.loadUserPagedFailure.type,
        UserActions.loadUserPagedSuccess.type,
      ]);
    });

    it('sends every filter field to the query parameter the backend reads it from', () => {
      const http = { request: jest.fn().mockReturnValue(EMPTY) };
      const realClient = new UserClient(
        http as unknown as HttpClient,
        'https://partner.test',
      );

      collect(createEffects({ userClient: realClient }).loadPaged$);
      actions$.next(
        UserActions.loadUserPaged({
          filter: new UserFilter({
            id: 'id-value',
            firstName: 'first-value',
            lastName: 'last-value',
            phoneNumber: 'phone-value',
            email: 'email-value',
            userProfiles: [3],
            authenticationTypes: [7],
          }),
          isActive: true,
          sort: [SortDefinition.fromJS({ field: 'lastName', direction: 1 })],
          offset: 40,
          limit: 20,
        }),
      );

      const url: string = http.request.mock.calls[0][1];
      expect(decodeURIComponent(url)).toBe(
        'https://partner.test/api/User/GetPaged?' +
          'Filter.Id=id-value&' +
          'Filter.IsActive=true&' +
          'Filter.FirstName=first-value&' +
          'Filter.LastName=last-value&' +
          'Filter.PhoneNumber=phone-value&' +
          'Filter.Email=email-value&' +
          'Filter.UserProfiles=3&' +
          'Filter.AuthenticationTypes=7&' +
          'Sort[0].field=lastName&Sort[0].direction=1&' +
          'Offset=40&' +
          'Limit=20',
      );
    });

    it('omits absent filter fields from the query string rather than sending blanks', () => {
      const http = { request: jest.fn().mockReturnValue(EMPTY) };
      const realClient = new UserClient(
        http as unknown as HttpClient,
        'https://partner.test',
      );

      collect(createEffects({ userClient: realClient }).loadPaged$);
      actions$.next(UserActions.loadUserPaged({ limit: 20 }));

      expect(http.request.mock.calls[0][1]).toBe(
        'https://partner.test/api/User/GetPaged?Limit=20',
      );
    });
  });

  describe('loadCurrent$', () => {
    it('emits the profile on success', () => {
      const user = MyProfileDto.fromJS({ id: USER_ID, firstName: 'Ada' });
      userClient.getCurrent.mockReturnValue(of(user));

      const emitted = collect(createEffects().loadCurrent$);
      actions$.next(UserActions.loadUserCurrent());

      expect(emitted).toEqual([UserActions.loadUserCurrentSuccess({ user })]);
    });

    it('emits loadUserCurrentFailure and stays alive when the profile read fails', () => {
      const user = MyProfileDto.fromJS({ id: USER_ID });
      userClient.getCurrent
        .mockReturnValueOnce(throwError(() => ({ message: 'offline' })))
        .mockReturnValueOnce(of(user));

      const emitted = collect(createEffects().loadCurrent$);
      actions$.next(UserActions.loadUserCurrent());
      actions$.next(UserActions.loadUserCurrent());

      expect(emitted.map((a) => a.type)).toEqual([
        UserActions.loadUserCurrentFailure.type,
        UserActions.loadUserCurrentSuccess.type,
      ]);
    });
  });

  describe('loadDetail$', () => {
    it('reads the requested id and emits it on success', () => {
      const user = UserItem.fromJS({ id: USER_ID });
      userClient.getById.mockReturnValue(of(user));

      const emitted = collect(createEffects().loadDetail$);
      actions$.next(UserActions.loadUserDetail({ id: USER_ID }));

      expect(userClient.getById).toHaveBeenCalledWith(USER_ID);
      expect(emitted).toEqual([UserActions.loadUserDetailSuccess({ user })]);
    });

    it('maps a failed detail read to loadUserDetailFailure', () => {
      const failure = { message: 'not found' };
      userClient.getById.mockReturnValue(throwError(() => failure));

      const emitted = collect(createEffects().loadDetail$);
      actions$.next(UserActions.loadUserDetail({ id: USER_ID }));

      expect(emitted).toHaveLength(1);
      expect(emitted[0].type).toBe(UserActions.loadUserDetailFailure.type);
    });
  });
});
