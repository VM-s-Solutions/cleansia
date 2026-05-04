import { CommonModule } from '@angular/common';
import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnDestroy,
  OnInit,
} from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import {
  PromoCodeListItem,
  PromoCodeRedemptionListItem,
  PromoCodeType,
} from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTableComponent,
  CleansiaTitleComponent,
  PaginationState,
  TableColumn,
} from '@cleansia/components';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ConfirmationService } from 'primeng/api';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { Subject, takeUntil } from 'rxjs';
import {
  formatDiscount,
  formatGlobalLimit,
  formatStatus,
  formatType,
  formatValidity,
  getPromoCodeStatus,
} from '../promo-codes-list/promo-codes-list.models';
import { PromoCodeDetailFacade } from './promo-code-detail.facade';

@Component({
  selector: 'cleansia-admin-promo-code-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    TranslatePipe,
    CleansiaButtonComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaTableComponent,
    CleansiaTitleComponent,
    ConfirmDialogModule,
  ],
  templateUrl: './promo-code-detail.component.html',
  providers: [PromoCodeDetailFacade, ConfirmationService],
})
export class PromoCodeDetailComponent
  implements OnInit, AfterViewInit, OnDestroy
{
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  private readonly confirmationService = inject(ConfirmationService);
  protected readonly facade = inject(PromoCodeDetailFacade);

  protected readonly PromoCodeType = PromoCodeType;

  private readonly destroy$ = new Subject<void>();
  private promoCodeId: string | null = null;

  redemptionColumns!: TableColumn<PromoCodeRedemptionListItem>[];

  readonly canDeactivate = computed(() => {
    const pc = this.facade.promoCode();
    if (!pc) return false;
    // Reuse list-level helper by mapping the detail object onto list-shape fields.
    return getPromoCodeStatus(pc as unknown as PromoCodeListItem) === 'active';
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.router.navigate(['/loyalty/promos']);
      return;
    }
    this.promoCodeId = id;
    this.facade.loadPromoCode(id);
    this.facade.loadRedemptions(id, 0, 20);
  }

  ngAfterViewInit(): void {
    this.rebuildRedemptionColumns();
    this.translate.onLangChange
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => this.rebuildRedemptionColumns());
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    this.facade.ngOnDestroy();
  }

  private rebuildRedemptionColumns(): void {
    const t = this.translate;
    this.redemptionColumns = [
      {
        id: 'userEmail',
        field: 'userEmail',
        header: t.instant('pages.promo_codes.detail.column.user'),
        getValue: (row) => row.userEmail ?? '—',
        width: '30%',
      },
      {
        id: 'orderId',
        field: 'orderId',
        header: t.instant('pages.promo_codes.detail.column.order'),
        getValue: (row) => row.orderId ?? '—',
        width: '25%',
      },
      {
        id: 'appliedDiscount',
        field: 'appliedDiscount',
        header: t.instant('pages.promo_codes.detail.column.applied'),
        getValue: (row) => `${row.appliedDiscount}`,
        width: '20%',
      },
      {
        id: 'redeemedOn',
        field: 'redeemedOn',
        header: t.instant('pages.promo_codes.detail.column.redeemed_on'),
        getValue: (row) => this.formatDate(row.redeemedOn),
        width: '25%',
      },
    ];
  }

  formatDate(d?: Date): string {
    if (!d) return '—';
    return new Intl.DateTimeFormat(this.translate.currentLang ?? 'en', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    }).format(d);
  }

  formatType(): string {
    const pc = this.facade.promoCode();
    if (!pc) return '';
    return formatType(pc as unknown as PromoCodeListItem, this.translate);
  }

  formatDiscount(): string {
    const pc = this.facade.promoCode();
    if (!pc) return '';
    return formatDiscount(
      pc as unknown as PromoCodeListItem,
      this.translate
    );
  }

  formatStatus(): string {
    const pc = this.facade.promoCode();
    if (!pc) return '';
    return formatStatus(pc as unknown as PromoCodeListItem, this.translate);
  }

  formatValidity(): string {
    const pc = this.facade.promoCode();
    if (!pc) return '';
    return formatValidity(
      pc as unknown as PromoCodeListItem,
      this.translate,
      (d) => this.formatDate(d)
    );
  }

  formatGlobal(): string {
    const pc = this.facade.promoCode();
    if (!pc) return '';
    return formatGlobalLimit(
      pc as unknown as PromoCodeListItem,
      this.translate
    );
  }

  onRedemptionsPageChange(event: PaginationState): void {
    this.facade.onRedemptionsPageChange(event.first, event.rows);
  }

  onEdit(): void {
    this.facade.navigateToEdit();
  }

  onBack(): void {
    this.facade.navigateToList();
  }

  confirmDeactivate(): void {
    this.confirmationService.confirm({
      header: this.translate.instant(
        'pages.promo_codes.detail.deactivate_confirm_title'
      ),
      message: this.translate.instant(
        'pages.promo_codes.detail.deactivate_confirm_body'
      ),
      acceptLabel: this.translate.instant(
        'pages.promo_codes.detail.deactivate_confirm_yes'
      ),
      rejectLabel: this.translate.instant(
        'pages.promo_codes.detail.deactivate_confirm_cancel'
      ),
      icon: 'pi pi-exclamation-triangle',
      accept: () => this.facade.deactivate(),
    });
  }
}
