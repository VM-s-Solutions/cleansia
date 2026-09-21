import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  TemplateRef,
  viewChild,
} from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { PromoCodeListItem } from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaFilterChipsComponent,
  CleansiaFilterDrawerComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaSelectComponent,
  CleansiaStatusBadgeComponent,
  CleansiaTableComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
  PaginationState,
} from '@cleansia/components';
import { PermissionService, Policy } from '@cleansia/services';
import { CleansiaPermissionDirective } from '@cleansia/directives';
import { formatDate } from '@cleansia/utils';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { PromoCodesListFacade } from './promo-codes-list.facade';
import {
  getPromoCodeStatus,
  getPromoCodeTableDefinition,
  PromoCodeStatusBadge,
} from './promo-codes-list.models';

@Component({
  selector: 'cleansia-admin-promo-codes-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    TranslatePipe,
    CleansiaButtonComponent,
    CleansiaFilterChipsComponent,
    CleansiaFilterDrawerComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaSelectComponent,
    CleansiaStatusBadgeComponent,
    CleansiaTableComponent,
    CleansiaTextInputComponent,
    CleansiaTitleComponent,
    CleansiaPermissionDirective,
  ],
  templateUrl: './promo-codes-list.component.html',
  providers: [PromoCodesListFacade],
})
export class PromoCodesListComponent implements OnInit {
  private readonly translate = inject(TranslateService);
  private readonly permissions = inject(PermissionService);
  protected readonly facade = inject(PromoCodesListFacade);
  protected readonly Policy = Policy;

  private readonly statusTemplate = viewChild<TemplateRef<PromoCodeListItem>>('statusTemplate');

  protected readonly table = computed(() => {
    this.facade.lang();
    return getPromoCodeTableDefinition(
      {
        onView: (row) => this.facade.navigateToDetail(row),
        onEdit: (row) => this.facade.navigateToEdit(row),
        onDeactivate: (row) => this.facade.deactivate(row),
      },
      this.translate,
      this.permissions,
      (d?: Date) => formatDate(d, this.translate.currentLang) || '—',
      this.statusTemplate()
    );
  });

  ngOnInit(): void {
    this.facade.loadPromoCodes();
  }

  promoStatus(row: PromoCodeListItem): PromoCodeStatusBadge {
    return getPromoCodeStatus(row);
  }

  onPageChange(event: PaginationState): void {
    this.facade.onPageChange(event.first, event.rows);
  }
}
