import { inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { CustomerClient, takeCardSetupReturnUrl } from '@cleansia/customer-services';
import { SnackbarService } from '@cleansia/services';
import { catchError, finalize, of, takeUntil } from 'rxjs';
import { CARD_SETUP_CANCEL, CARD_SETUP_SUCCESS, SavedCardRow, toSavedCardRow } from './saved-cards.models';

@Injectable()
export class SavedCardsFacade extends UnsubscribeControlDirective {
  private readonly customerClient = inject(CustomerClient);
  private readonly snackbar = inject(SnackbarService);
  private readonly router = inject(Router);

  readonly cards = signal<SavedCardRow[]>([]);
  readonly loading = signal(true);
  readonly hasError = signal(false);
  readonly removingId = signal<string | null>(null);

  /**
   * Stripe returns every card capture to the profile, so the customer is sent on to the booking or
   * schedule the capture started from, which parked itself before leaving.
   */
  init(cardSetupOutcome: string | null): void {
    if (cardSetupOutcome === CARD_SETUP_SUCCESS) {
      this.snackbar.showSuccessTranslated('pages.profile.saved_cards.setup_success');
    } else if (cardSetupOutcome === CARD_SETUP_CANCEL) {
      this.snackbar.showInfoTranslated('pages.profile.saved_cards.setup_cancelled');
    } else {
      this.load();
      return;
    }
    const returnUrl = takeCardSetupReturnUrl();
    if (returnUrl) {
      void this.router.navigateByUrl(returnUrl, { replaceUrl: true });
      return;
    }
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.hasError.set(false);
    this.customerClient.savedCardClient
      .getMine()
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => {
          this.hasError.set(true);
          return of(null);
        }),
        finalize(() => this.loading.set(false)),
      )
      .subscribe((cards) => {
        this.cards.set((cards ?? []).map(toSavedCardRow));
      });
  }

  remove(id: string): void {
    if (this.removingId()) return;
    this.removingId.set(id);
    this.customerClient.savedCardClient
      .remove(id)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.removingId.set(null)),
      )
      .subscribe((response) => {
        if (response) this.snackbar.showSuccessTranslated('pages.profile.saved_cards.remove_success');
        this.load();
      });
  }
}
