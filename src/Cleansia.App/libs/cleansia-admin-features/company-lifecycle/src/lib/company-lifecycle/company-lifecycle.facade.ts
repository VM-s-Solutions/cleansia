import { computed, inject, Injectable, signal } from '@angular/core';
import { AdminClient, CompanyLifecycleDto, CompanyLifecycleState } from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { ConfirmOptions, DialogService, SnackbarService } from '@cleansia/services';
import { formatDate } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';
import { catchError, filter, finalize, Observable, of, Subject, switchMap, takeUntil, tap } from 'rxjs';
import {
  ActAvailability,
  buildSettlementFacts,
  buildStamps,
  buildWindDownCommand,
  formatSettlementFactValue,
  getActAvailability,
  getFactNameKey,
  getFactStatusKey,
  getStateSeverity,
  isWindDownRunInProgress,
  LifecycleAct,
  LifecycleStamp,
  SETTLEMENT_FACT_DEFINITIONS,
  SettlementFactRow,
  startOfDay,
  StateSeverity,
} from './company-lifecycle.models';

const PAGE = 'pages.company_lifecycle';
const DANGER: ConfirmOptions = { danger: true };

@Injectable()
export class CompanyLifecycleFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbar = inject(SnackbarService);
  private readonly dialog = inject(DialogService);
  private readonly translate = inject(TranslateService);

  readonly lifecycle = signal<CompanyLifecycleDto | null>(null);
  readonly loading = signal<boolean>(false);
  readonly initialLoading = signal<boolean>(true);
  readonly hasError = signal<boolean>(false);
  readonly actInFlight = signal<LifecycleAct | null>(null);
  readonly windDownDialogOpen = signal<boolean>(false);

  // The instant the page last read the server: every "is it in the future / still running" question
  // is answered against it, so the view stays consistent until the next read rather than drifting.
  private readonly readAt = signal<Date>(new Date());
  private readonly language = signal<string>(this.translate.currentLang);

  readonly stateSeverity = computed<StateSeverity | null>(() => {
    const dto = this.lifecycle();
    return dto ? getStateSeverity(dto.state) : null;
  });

  readonly stamps = computed<LifecycleStamp[]>(() => {
    const dto = this.lifecycle();
    const lang = this.language();
    return dto ? buildStamps(dto, (d) => this.day(d, lang), (d) => this.stamp(d, lang)) : [];
  });

  readonly facts = computed<SettlementFactRow[]>(() => {
    const dto = this.lifecycle();
    const lang = this.language();
    if (!dto) return [];
    return buildSettlementFacts(dto, this.readAt()).map((fact) => ({
      ...fact,
      display: formatSettlementFactValue(fact, this.translate, (d) => this.day(d, lang)),
      statusKey: getFactStatusKey(fact.status),
    }));
  });

  readonly acts = computed<ActAvailability[]>(() => {
    const dto = this.lifecycle();
    const lang = this.language();
    if (!dto) return [];
    return getActAvailability(
      dto,
      this.readAt(),
      (id) => this.translate.instant(getFactNameKey(id)),
      (d) => this.day(d, lang)
    );
  });

  readonly runInProgress = computed(() => {
    const dto = this.lifecycle();
    return !!dto && isWindDownRunInProgress(dto, this.readAt());
  });

  readonly minWindDownDate = computed(() => startOfDay(this.readAt()));

  private readonly reload$ = new Subject<void>();

  constructor() {
    super();
    this.reload$
      .pipe(
        tap(() => {
          this.loading.set(true);
          this.hasError.set(false);
        }),
        switchMap(() =>
          this.adminClient.adminCompanyLifecycleClient.get().pipe(
            catchError(() => {
              this.hasError.set(true);
              return of(null);
            })
          )
        ),
        takeUntil(this.destroyed$)
      )
      .subscribe((response) => {
        this.readAt.set(new Date());
        this.lifecycle.set(response);
        this.loading.set(false);
        if (this.initialLoading()) {
          this.initialLoading.set(false);
        }
      });

    this.translate.onLangChange
      .pipe(takeUntil(this.destroyed$))
      .subscribe((event) => this.language.set(event.lang));
  }

  load(): void {
    this.reload$.next();
  }

  perform(act: LifecycleAct): void {
    const dto = this.lifecycle();
    const availability = this.acts().find((a) => a.act === act);
    if (!dto || !availability?.enabled || this.actInFlight()) return;

    switch (act) {
      case LifecycleAct.Deactivate:
        return this.deactivate(dto);
      case LifecycleAct.Reactivate:
        return this.reactivate(dto);
      case LifecycleAct.WindDown:
        return this.windDown(dto);
      case LifecycleAct.Archive:
        return this.archive(dto, availability);
    }
  }

  setWindDownDialogOpen(open: boolean): void {
    this.windDownDialogOpen.set(open);
  }

  confirmWindDown(fromDate: Date): void {
    if (this.actInFlight()) return;
    this.windDownDialogOpen.set(false);
    this.run(
      LifecycleAct.WindDown,
      of(true),
      () => this.adminClient.adminCompanyLifecycleClient.windDown(buildWindDownCommand(fromDate)),
      `${PAGE}.messages.wind_down_requested`
    );
  }

  private deactivate(dto: CompanyLifecycleDto): void {
    const confirmed$ = dto.windDownFrom
      ? this.dialog.confirmTranslated(
          `${PAGE}.confirm.deactivate_with_wind_down`,
          `${PAGE}.acts.deactivate`,
          { name: dto.name, date: this.day(dto.windDownFrom) },
          DANGER
        )
      : this.dialog.confirmTranslated(`${PAGE}.confirm.deactivate`, `${PAGE}.acts.deactivate`, { name: dto.name }, DANGER);
    this.run(
      LifecycleAct.Deactivate,
      confirmed$,
      () => this.adminClient.adminCompanyLifecycleClient.deactivate(),
      `${PAGE}.messages.deactivated`
    );
  }

  private reactivate(dto: CompanyLifecycleDto): void {
    const confirmed$ = dto.windDownFrom
      ? this.dialog.confirmTranslated(`${PAGE}.confirm.reactivate_after_wind_down`, `${PAGE}.acts.reactivate`, {
          name: dto.name,
          date: this.day(dto.windDownFrom),
        })
      : this.dialog.confirmTranslated(`${PAGE}.confirm.reactivate`, `${PAGE}.acts.reactivate`, { name: dto.name });
    this.run(
      LifecycleAct.Reactivate,
      confirmed$,
      () => this.adminClient.adminCompanyLifecycleClient.reactivate(),
      `${PAGE}.messages.reactivated`
    );
  }

  private windDown(dto: CompanyLifecycleDto): void {
    if (!dto.windDownFrom) {
      this.windDownDialogOpen.set(true);
      return;
    }
    this.run(
      LifecycleAct.WindDown,
      this.dialog.confirmTranslated(`${PAGE}.confirm.run_wind_down_again`, `${PAGE}.acts.run_wind_down_again`, {
        name: dto.name,
        date: this.day(dto.windDownFrom),
      }),
      () => this.adminClient.adminCompanyLifecycleClient.windDown(buildWindDownCommand(undefined)),
      `${PAGE}.messages.wind_down_rerun`
    );
  }

  private archive(dto: CompanyLifecycleDto, availability: ActAvailability): void {
    const confirmed$ =
      dto.state === CompanyLifecycleState.Frozen && dto.archiveRequestedOn
        ? this.dialog.confirmTranslated(
            `${PAGE}.confirm.archive_again`,
            availability.labelKey,
            { name: dto.name, frozenOn: this.stamp(dto.archiveRequestedOn) },
            DANGER
          )
        : this.dialog.confirmTranslated(
            `${PAGE}.confirm.archive`,
            availability.labelKey,
            {
              name: dto.name,
              facts: SETTLEMENT_FACT_DEFINITIONS.filter((d) => d.archivePrecondition && d.kind === 'count')
                .map((d) => this.translate.instant(getFactNameKey(d.id)))
                .join(', '),
              date: dto.chargebackHorizonEndsOn
                ? this.day(dto.chargebackHorizonEndsOn)
                : this.translate.instant(`${PAGE}.values.no_horizon`),
            },
            DANGER
          );
    this.run(
      LifecycleAct.Archive,
      confirmed$,
      () => this.adminClient.adminCompanyLifecycleClient.archive(),
      `${PAGE}.messages.archive_requested`
    );
  }

  private run(
    act: LifecycleAct,
    confirmed$: Observable<boolean>,
    request: () => Observable<unknown>,
    successKey: string
  ): void {
    confirmed$
      .pipe(
        filter((confirmed) => confirmed),
        tap(() => this.actInFlight.set(act)),
        switchMap(() =>
          request().pipe(
            catchError(() => of(null)),
            finalize(() => this.actInFlight.set(null))
          )
        ),
        takeUntil(this.destroyed$)
      )
      .subscribe((response) => {
        if (response) {
          this.snackbar.showSuccessTranslated(successKey);
        }
        this.load();
      });
  }

  private day(date: Date, lang = this.language()): string {
    return formatDate(date, lang);
  }

  private stamp(date: Date, lang = this.language()): string {
    return formatDate(date, lang, 'dateTime');
  }
}
