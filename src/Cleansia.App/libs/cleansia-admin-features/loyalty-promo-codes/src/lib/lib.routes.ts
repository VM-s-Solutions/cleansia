import { Route } from '@angular/router';
import { PromoCodesListComponent } from './promo-codes-list/promo-codes-list.component';
import { PromoCodeFormComponent } from './promo-code-form/promo-code-form.component';
import { PromoCodeDetailComponent } from './promo-code-detail/promo-code-detail.component';

export const promoCodesRoutes: Route[] = [
  {
    path: '',
    component: PromoCodesListComponent,
    data: { title: 'page_titles.admin.promo_codes' },
  },
  {
    path: 'new',
    component: PromoCodeFormComponent,
    data: { mode: 'create', title: 'page_titles.admin.promo_code_create' },
  },
  {
    path: ':id',
    component: PromoCodeDetailComponent,
    data: { title: 'page_titles.admin.promo_code_detail' },
  },
  {
    path: ':id/edit',
    component: PromoCodeFormComponent,
    data: { mode: 'edit', title: 'page_titles.admin.promo_code_edit' },
  },
];
