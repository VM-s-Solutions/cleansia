import { CommonModule } from '@angular/common';
import {
  AfterViewInit,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  inject,
  OnDestroy,
  TemplateRef,
  viewChild,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import {
  AdminReferralListItem,
  ReferralStatus,
} from '@cleansia/admin-services';
import {
  CleansiaCalendarComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaSelectComponent,
  CleansiaTableComponent,
  CleansiaTitleComponent,
  PaginationState,
  TableColumn,
} from '@cleansia/components';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { TagModule } from 'primeng/tag';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import {
  ReferralStatusFilter,
  ReferralsListFacade,
} from './referrals-list.facade';

@Component({
  selector: 'cleansia-admin-referrals-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslatePipe,
    TagModule,
    CleansiaCalendarComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaSelectComponent,
    CleansiaTableComponent,
    CleansiaTitleComponent,
  ],
  templateUrl: './referrals-list.component.html',
  providers: [ReferralsListFacade],
})
export class ReferralsListComponent implements AfterViewInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly translate = inject(TranslateService);
  private readonly cd = inject(ChangeDetectorRef);
  protected readonly facade = inject(ReferralsListFacade);

  readonly ReferralStatus = ReferralStatus;

  private readonly destroy$ = new Subject<void>();

  statusTemplate = viewChild<TemplateRef<AdminReferralListItem>>(
    'statusTemplate'
  );
  pointsTemplate = viewChild<TemplateRef<AdminReferralListItem>>(
    'pointsTemplate'
  );

  referralColumns!: TableColumn<AdminReferralListItem>[];

  filterForm = this.fb.group({
    status: ['all' as ReferralStatusFilter],
    dateFrom: [null as Date | null],
    dateTo: [null as Date | null],
  });

  statusOptions = [
    { label: 'pages.loyalty_referrals.filter.status_all', value: 'all' },
    {
      label: 'pages.loyalty_referrals.filter.status_accepted',
      value: 'accepted',
    },
    {
      label: 'pages.loyalty_referrals.filter.status_qualified',
      value: 'qualified',
    },
    {
      label: 'pages.loyalty_referrals.filter.status_expired',
      value: 'expired',
    },
  ];

  get translatedStatusOptions(): { label: string; value: string }[] {
    return this.statusOptions.map((opt) => ({
      label: this.translate.instant(opt.label),
      value: opt.value,
    }));
  }

  ngAfterViewInit(): void {
    this.rebuildTableDefinitions();
    this.cd.detectChanges();

    this.filterForm.controls.status.valueChanges
      .pipe(distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => this.applyFilters());

    // Auto-apply on date changes with a short debounce so a user picking a
    // date doesn't trigger two requests if they're navigating the calendar.
    this.filterForm.controls.dateFrom.valueChanges
      .pipe(debounceTime(250), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => this.applyFilters());

    this.filterForm.controls.dateTo.valueChanges
      .pipe(debounceTime(250), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => this.applyFilters());

    this.translate.onLangChange
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.rebuildTableDefinitions();
        this.cd.detectChanges();
      });

    this.facade.loadReferrals();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    this.facade.ngOnDestroy();
  }

  private rebuildTableDefinitions(): void {
    this.referralColumns = [
      {
        id: 'referrer',
        field: 'referrerEmail',
        header: this.translate.instant('pages.loyalty_referrals.column.referrer'),
        getValue: (row) => row.referrerEmail || '—',
        width: '20%',
      },
      {
        id: 'referred',
        field: 'referredEmail',
        header: this.translate.instant('pages.loyalty_referrals.column.referred'),
        getValue: (row) => row.referredEmail || '—',
        width: '20%',
      },
      {
        id: 'status',
        field: 'status',
        header: this.translate.instant('pages.loyalty_referrals.column.status'),
        customTemplate: this.statusTemplate(),
        width: '12%',
      },
      {
        id: 'acceptedOn',
        field: 'acceptedOn',
        header: this.translate.instant(
          'pages.loyalty_referrals.column.accepted_on'
        ),
        getValue: (row) => this.formatDate(row.acceptedOn),
        width: '14%',
      },
      {
        id: 'qualifiedOn',
        field: 'firstQualifyingOrderOn',
        header: this.translate.instant(
          'pages.loyalty_referrals.column.qualified_on'
        ),
        getValue: (row) => this.formatDate(row.firstQualifyingOrderOn),
        width: '14%',
      },
      {
        id: 'pointsAwarded',
        field: 'pointsAwardedToReferrer',
        header: this.translate.instant(
          'pages.loyalty_referrals.column.points_awarded'
        ),
        customTemplate: this.pointsTemplate(),
        width: '20%',
      },
    ];
  }

  applyFilters(): void {
    const v = this.filterForm.value;
    this.facade.applyFilter({
      status: (v.status ?? 'all') as ReferralStatusFilter,
      dateFrom: v.dateFrom ?? undefined,
      dateTo: v.dateTo ?? undefined,
    });
  }

  resetFilters(): void {
    this.filterForm.reset({ status: 'all', dateFrom: null, dateTo: null });
    this.facade.resetFilter();
  }

  onPageChange(event: PaginationState): void {
    this.facade.onPageChange(event.first, event.rows);
  }

  formatDate(d?: Date): string {
    if (!d) return this.translate.instant('pages.loyalty_referrals.not_yet');
    return new Intl.DateTimeFormat(this.translate.currentLang ?? 'en', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
    }).format(d);
  }

  statusLabel(status: ReferralStatus | undefined): string {
    switch (status) {
      case ReferralStatus.Accepted:
        return this.translate.instant('pages.loyalty_referrals.status.Accepted');
      case ReferralStatus.Qualified:
        return this.translate.instant(
          'pages.loyalty_referrals.status.Qualified'
        );
      case ReferralStatus.Expired:
        return this.translate.instant('pages.loyalty_referrals.status.Expired');
      default:
        return '';
    }
  }

  statusSeverity(
    status: ReferralStatus | undefined
  ): 'info' | 'success' | 'warn' | 'secondary' {
    switch (status) {
      case ReferralStatus.Accepted:
        return 'info';
      case ReferralStatus.Qualified:
        return 'success';
      case ReferralStatus.Expired:
        return 'warn';
      default:
        return 'secondary';
    }
  }

  formatPointsAwarded(row: AdminReferralListItem): string {
    if (
      row.pointsAwardedToReferrer == null &&
      row.pointsAwardedToReferred == null
    ) {
      return this.translate.instant('pages.loyalty_referrals.not_yet');
    }
    return this.translate.instant('pages.loyalty_referrals.points_format', {
      referrer: row.pointsAwardedToReferrer ?? 0,
      referred: row.pointsAwardedToReferred ?? 0,
    });
  }
}
