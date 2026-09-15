import { computed, inject, Injectable, signal } from '@angular/core';
import { CustomerClient, LegalDocumentDto, LegalDocumentType } from '@cleansia/customer-services';
import { selectMarketCountryId } from '@cleansia/customer-stores';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { Store } from '@ngrx/store';
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
import { sectionHeadingsIn } from './legal-document.models';

/**
 * The legal text in force for the chosen market, in the current language, as the server rendered
 * it. Anonymous and credential-less, so the customer app's server render fetches it and the browser
 * takes it from the transfer cache; a market or language switch afterwards re-reads it.
 */
@Injectable()
export class LegalDocumentFacade extends UnsubscribeControlDirective {
  private readonly client = inject(CustomerClient);
  private readonly store = inject(Store);
  private readonly translate = inject(TranslateService);

  readonly document = signal<LegalDocumentDto | null>(null);
  readonly loading = signal(true);
  readonly hasError = signal(false);
  readonly language = signal<string>(this.translate.currentLang);
  readonly headings = computed(() => sectionHeadingsIn(this.document()?.contentHtml ?? ''));

  private readonly attempt$ = new BehaviorSubject(0);

  load(type: LegalDocumentType): void {
    const countryId$ = this.store.select(selectMarketCountryId).pipe(distinctUntilChanged());
    const lang$ = this.translate.onLangChange.pipe(
      map(({ lang }) => lang),
      startWith(this.translate.currentLang),
      distinctUntilChanged(),
    );

    combineLatest([countryId$, lang$, this.attempt$])
      .pipe(
        switchMap(([countryId, lang]) => {
          this.language.set(lang);
          this.document.set(null);
          this.hasError.set(false);
          this.loading.set(true);
          return this.client.legalClient
            .getDocument(type, countryId ?? undefined, lang)
            .pipe(catchError(() => of(null)));
        }),
        takeUntil(this.destroyed$),
      )
      .subscribe((document) => {
        this.document.set(document);
        this.hasError.set(document === null);
        this.loading.set(false);
      });
  }

  retry(): void {
    this.attempt$.next(this.attempt$.value + 1);
  }
}
