import { inject, Injectable } from '@angular/core';
import { PartnerClient } from '@cleansia/partner-services';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { catchError, map, mergeMap, of } from 'rxjs';
import * as UserActions from './user.actions';

@Injectable()
export class UserEffects {
  private readonly partnerClient = inject(PartnerClient);
  private readonly actions$ = inject(Actions);

  loadPaged$ = createEffect(() =>
    this.actions$.pipe(
      ofType(UserActions.loadUserPaged),
      mergeMap((req) =>
        this.partnerClient.userClient
          .getPaged(
            req.filter?.id,
            req.isActive,
            req.filter?.firstName,
            req.filter?.lastName,
            req.filter?.phoneNumber,
            req.filter?.email,
            req.filter?.userProfiles,
            req.filter?.authenticationTypes,
            req.sort,
            req.offset,
            req.limit
          )
          .pipe(
            map((page) => UserActions.loadUserPagedSuccess({ page })),
            catchError((error) =>
              of(UserActions.loadUserPagedFailure({ error }))
            )
          )
      )
    )
  );

  loadCurrent$ = createEffect(() =>
    this.actions$.pipe(
      ofType(UserActions.loadUserCurrent),
      mergeMap(() =>
        this.partnerClient.userClient.getCurrent().pipe(
          map((user) => UserActions.loadUserCurrentSuccess({ user })),
          catchError((error) =>
            of(UserActions.loadUserCurrentFailure({ error }))
          )
        )
      )
    )
  );

  loadDetail$ = createEffect(() =>
    this.actions$.pipe(
      ofType(UserActions.loadUserDetail),
      mergeMap(({ id }) =>
        this.partnerClient.userClient.getById(id).pipe(
          map((user) => UserActions.loadUserDetailSuccess({ user })),
          catchError((error) =>
            of(UserActions.loadUserDetailFailure({ error }))
          )
        )
      )
    )
  );
}
