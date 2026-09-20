import { computed, inject, Injectable, signal } from '@angular/core';
import { CustomerClient, WorkContractDto } from '@cleansia/customer-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { localeFor } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';
import {
  BehaviorSubject,
  catchError,
  combineLatest,
  distinctUntilChanged,
  map,
  of,
  startWith,
  switchMap,
  takeUntil,
} from 'rxjs';
import {
  buildWorkContractFactRows,
  formatDateTime,
  languageDisplayName,
} from './work-contract-page.models';

/**
 * The contract for work a cleaner accepted for one seat of the customer's order, as the server
 * rendered it: the accepted document's text in the current language, the job facts frozen at the
 * acceptance, and the acceptance itself. Keyed on the acceptance, so a dropped cleaner's contract
 * stays readable; a language switch re-reads the same acceptance.
 */
@Injectable()
export class WorkContractPageFacade extends UnsubscribeControlDirective {
  private readonly client = inject(CustomerClient);
  private readonly translate = inject(TranslateService);

  readonly contract = signal<WorkContractDto | null>(null);
  readonly loading = signal(true);
  readonly hasError = signal(false);
  readonly language = signal<string>(this.translate.currentLang);

  readonly factRows = computed(() =>
    buildWorkContractFactRows(this.contract()?.facts, this.language()),
  );

  readonly acceptedOn = computed(() => {
    const acceptedOn = this.contract()?.acceptance?.acceptedOn;
    return acceptedOn ? formatDateTime(acceptedOn, localeFor(this.language())) : '';
  });

  readonly acceptedLanguageName = computed(() => {
    const contract = this.contract();
    const acceptedLanguage = contract?.acceptance?.acceptedLanguage;
    if (!acceptedLanguage || acceptedLanguage === contract?.language) return null;
    return languageDisplayName(acceptedLanguage, this.language());
  });

  private readonly attempt$ = new BehaviorSubject(0);

  load(acceptanceId: string): void {
    const lang$ = this.translate.onLangChange.pipe(
      map(({ lang }) => lang),
      startWith(this.translate.currentLang),
      distinctUntilChanged(),
    );

    combineLatest([lang$, this.attempt$])
      .pipe(
        switchMap(([lang]) => {
          this.language.set(lang);
          this.contract.set(null);
          this.hasError.set(false);
          this.loading.set(true);
          return this.client.orderClient
            .getWorkContract(acceptanceId, lang)
            .pipe(catchError(() => of(null)));
        }),
        takeUntil(this.destroyed$),
      )
      .subscribe((contract) => {
        this.contract.set(contract);
        this.hasError.set(contract === null);
        this.loading.set(false);
      });
  }

  retry(): void {
    this.attempt$.next(this.attempt$.value + 1);
  }
}
