import { DestroyRef, Injectable, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslateService } from '@ngx-translate/core';
import { EMPTY, Subscription, catchError, switchMap } from 'rxjs';
import { SilentFailurePartnerClient } from '../client/base-client';
import { buildLanguagePushCommand } from './language-preference-sync.models';
import { PartnerAuthService } from './partner-auth.service';

/**
 * Carries the cleaner's chosen display language onto their user record, which is what the payroll job
 * threads into the period-closed email and **the rendered payout invoice PDF — a tax document the
 * cleaner files**. Without it the server's copy stays at whatever signup sent.
 *
 * **The seam is the language-change event, not the switcher**, because the switcher is a shared
 * component rendered by all three apps and reachable from several places.
 * → /flows/pay-and-payouts
 */
@Injectable({
  providedIn: 'root',
})
export class PartnerLanguagePreferenceSyncService {
  private readonly partnerClient = inject(SilentFailurePartnerClient);
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
   * on, and the local switch has already happened. Both requests therefore travel on
   * `SilentFailurePartnerClient`, or the shared error interceptor would toast what this swallows.
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
