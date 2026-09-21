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
  CleansiaStatusBadgeComponent,
  CleansiaTableComponent,
  CleansiaTitleComponent,
  PaginationState,
  TableColumn,
} from '@cleansia/components';
import { CleansiaPermissionDirective } from '@cleansia/directives';
import { Policy } from '@cleansia/services';
import { formatDate } from '@cleansia/utils';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Subject, takeUntil } from 'rxjs';
import {
  formatDiscount,
  formatGlobalLimit,
  formatType,
  formatValidity,
  getPromoCodeStatus,
  PromoCodeStatusBadge,
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
    CleansiaStatusBadgeComponent,
    CleansiaTableComponent,
    CleansiaTitleComponent,
    CleansiaPermissionDirective,
  ],
  templateUrl: './promo-code-detail.component.html',
  providers: [PromoCodeDetailFacade],
})
export class PromoCodeDetailComponent
  implements OnInit, AfterViewInit, OnDestroy
{
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  protected readonly facade = inject(PromoCodeDetailFacade);
  protected readonly Policy = Policy;

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
    return formatDate(d, this.translate.currentLang, 'dateTime') || '—';
  }

  formatType(): string {
    const pc = this.facade.promoCode();
    if (!pc) return '';
    return formatType(pc as unknown as PromoCodeListItem, this.translate);
  }

  formatDiscount(): string {
    const pc = this.facade.promoCode();
    if (!pc) return '';
    return formatDiscount(pc as unknown as PromoCodeListItem);
  }

  promoStatus(): PromoCodeStatusBadge | null {
    const pc = this.facade.promoCode();
    return pc ? getPromoCodeStatus(pc as unknown as PromoCodeListItem) : null;
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
    this.facade.deactivate();
  }
}
