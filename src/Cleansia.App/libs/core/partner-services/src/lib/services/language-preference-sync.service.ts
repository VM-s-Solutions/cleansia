import { DestroyRef, Injectable, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslateService } from '@ngx-translate/core';
import { EMPTY, Subscription, catchError, switchMap } from 'rxjs';
import { PartnerClient } from '../client/base-client';
import { buildLanguagePushCommand } from './language-preference-sync.models';
import { PartnerAuthService } from './partner-auth.service';

/**
 * Carries the display language the cleaner picked onto `User.PreferredLanguageCode`, which is what
 * `PayPeriodBackgroundService` threads into the period-closed email and the rendered payout invoice
 * PDF — a tax document the cleaner files. Without this the server's copy stays at whatever signup
 * sent, however often the cleaner switches the app.
 *
 * The seam is `onLangChange` rather than the switcher itself: the switcher is a shared component
 * rendered by all three apps and reachable from five places in this one, while the language a
 * partner app is running in is a single fact. `start()` is called once the app is running, so the
 * boot language chosen by the `initializeTranslations` APP_INITIALIZER — which has already run and
 * emitted — is not seen here. That ordering is what keeps this a push of a deliberate choice rather
 * than a reconcile on every load, which no other client does.
 */
@Injectable({
  providedIn: 'root',
})
export class PartnerLanguagePreferenceSyncService {
  private readonly partnerClient = inject(PartnerClient);
  private readonly auth = inject(PartnerAuthService);
  private readonly translate = inject(TranslateService);
  private readonly destroyRef = inject(DestroyRef);

  private subscription?: Subscription;

  start(): void {
    this.subscription ??= this.translate.onLangChange
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(({ lang }) => this.push(lang));
  }

  /**
   * The session is answered locally rather than by letting the call 401: the switcher is on the
   * login and registration screens too, and a 401 there hands the refresh coordinator a refresh it
   * cannot perform. Silent on failure by design — a language tap is not a save anyone is waiting
   * on, and the local switch has already happened.
   */
  private push(languageCode: string): void {
    if (!this.auth.isLoggedIn()) return;

    this.partnerClient.userClient
      .getCurrent()
      .pipe(
        switchMap((profile) => {
          const command = buildLanguagePushCommand(profile, languageCode);
          return command
            ? this.partnerClient.userClient.updateCurrentUser(command)
            : EMPTY;
        }),
        catchError(() => EMPTY)
      )
      .subscribe();
  }
}
