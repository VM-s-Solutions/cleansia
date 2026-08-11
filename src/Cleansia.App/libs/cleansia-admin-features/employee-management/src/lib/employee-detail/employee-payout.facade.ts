import { Injectable, computed, inject, signal } from '@angular/core';
import {
  AdminClient,
  AdminPayoutDetailsService,
  MaskedPayoutDetails,
  RevealedPayoutDetails,
} from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';
import {
  PayoutDetailRow,
  PayoutRowDeps,
  buildMaskedPayoutRows,
  buildRevealedPayoutRows,
} from './employee-payout.models';

@Injectable()
export class EmployeePayoutFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly payoutDetailsService = inject(AdminPayoutDetailsService);
  private readonly translate = inject(TranslateService);

  readonly maskedDetails = signal<MaskedPayoutDetails | null>(null);
  readonly revealedDetails = signal<RevealedPayoutDetails | null>(null);
  readonly loading = signal(false);
  readonly loadFailed = signal(false);
  readonly revealing = signal(false);

  private readonly loaded = signal(false);
  private readonly countryNames = signal<ReadonlyMap<string, string>>(new Map());

  readonly isEmpty = computed(
    () => this.loaded() && !this.loadFailed() && this.maskedDetails() === null
  );

  readonly isRevealed = computed(() => this.revealedDetails() !== null);

  readonly maskedRows = computed<PayoutDetailRow[]>(() => {
    const details = this.maskedDetails();
    return details ? buildMaskedPayoutRows(details, this.rowDeps()) : [];
  });

  readonly revealedRows = computed<PayoutDetailRow[]>(() => {
    const details = this.revealedDetails();
    return details ? buildRevealedPayoutRows(details, this.rowDeps()) : [];
  });

  load(employeeId: string): void {
    this.loading.set(true);
    this.loadFailed.set(false);
    this.revealedDetails.set(null);
    this.loadCountryNames();

    this.payoutDetailsService
      .getForEmployee(employeeId)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => {
          this.loadFailed.set(true);
          return of(null);
        }),
        finalize(() => {
          this.loading.set(false);
          this.loaded.set(true);
        })
      )
      .subscribe((details) => this.maskedDetails.set(details));
  }

  retry(employeeId: string): void {
    this.load(employeeId);
  }

  /**
   * The reveal is a POST because the audit engine only keys on commands and it
   * stamps `LastRevealedAt`/`RevealCount` (ADR-0034 D8.4). The masked record is
   * therefore re-read afterwards so the counters the admin just moved are the
   * ones on screen.
   */
  reveal(employeeId: string): void {
    if (this.revealing()) return;

    this.revealing.set(true);

    this.adminClient.payoutDetailsClient
      .reveal(employeeId)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.revealing.set(false))
      )
      .subscribe((details) => {
        if (!details) return;
        this.revealedDetails.set(details);
        this.refreshMasked(employeeId);
      });
  }

  hide(): void {
    this.revealedDetails.set(null);
  }

  private refreshMasked(employeeId: string): void {
    this.payoutDetailsService
      .getForEmployee(employeeId)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null))
      )
      .subscribe((details) => {
        if (details) this.maskedDetails.set(details);
      });
  }

  private rowDeps(): PayoutRowDeps {
    const names = this.countryNames();
    return {
      translate: (key) => this.translate.instant(key),
      formatDateTime: (value) => (value ? value.toLocaleString('en-GB') : '—'),
      resolveCountryName: (countryId) =>
        countryId ? names.get(countryId) ?? '' : '',
    };
  }

  private loadCountryNames(): void {
    if (this.countryNames().size > 0) return;

    this.adminClient.adminCountryClient
      .getOverview()
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of([]))
      )
      .subscribe((countries) => {
        const currentLang = this.translate.currentLang;
        this.countryNames.set(
          new Map(
            (countries ?? [])
              .filter((country) => !!country.id)
              .map((country) => [
                country.id as string,
                country.translations?.[currentLang]?.name ?? country.name ?? '',
              ])
          )
        );
      });
  }
}
