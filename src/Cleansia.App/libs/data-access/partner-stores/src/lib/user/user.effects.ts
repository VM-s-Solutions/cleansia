import { inject, Injectable } from '@angular/core';
import {
  BlobFileDto,
  PartnerClient,
  UpdateCurrentUserCommand,
  UserListItem,
} from '@cleansia/partner-services';
import { SnackbarService } from '@cleansia/services';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { catchError, map, mergeMap, of } from 'rxjs';
import * as UserActions from './user.actions';

@Injectable()
export class UserEffects {
  private readonly partnerClient = inject(PartnerClient);
  private readonly actions$ = inject(Actions);
  private readonly store = inject(Store);
  private readonly translate = inject(TranslateService);
  private readonly snackbarService = inject(SnackbarService);

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
          map((user: UserListItem) =>
            UserActions.loadUserCurrentSuccess({ user })
          ),
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

  updateCurrent$ = createEffect(() =>
    this.actions$.pipe(
      ofType(UserActions.updateUserCurrent),
      mergeMap(
        ({
          id,
          firstName,
          lastName,
          phoneNumber,
          birthDate,
          photo = new BlobFileDto({
            fileName: '',
            contentType: '',
            base64Content: '',
          }),
        }) =>
          this.partnerClient.userClient
            .updateCurrentUser(
              new UpdateCurrentUserCommand({
                id,
                firstName,
                lastName,
                phoneNumber,
                birthDate,
                photo,
                languageCode:
                  this.translate.currentLang || this.translate.getDefaultLang(),
              })
            )
            .pipe(
              map(({ id }) => {
                this.snackbarService.showSuccess(
                  this.translate.instant(
                    'pages.user_profile.user_updated_message.success'
                  )
                );
                return UserActions.updateUserCurrentSuccess({ id: id! });
              }),
              catchError((error) =>
                of(UserActions.updateUserCurrentFailure({ error }))
              )
            )
      )
    )
  );
}
