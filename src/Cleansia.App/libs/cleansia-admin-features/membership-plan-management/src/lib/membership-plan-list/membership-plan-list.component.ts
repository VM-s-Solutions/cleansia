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
import { Router } from '@angular/router';
import { MembershipPlanListItem } from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaCheckboxComponent,
  CleansiaFilterChipsComponent,
  CleansiaFilterDrawerComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaStatusBadgeComponent,
  CleansiaTableComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
  PaginationState,
} from '@cleansia/components';
import { CleansiaPermissionDirective } from '@cleansia/directives';
import { CleansiaAdminRoute, PermissionService, Policy } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { MembershipPlanListFacade } from './membership-plan-list.facade';
import { getMembershipPlanTableDefinition } from './membership-plan-list.models';

@Component({
  selector: 'cleansia-admin-membership-plan-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    TranslatePipe,
    CleansiaButtonComponent,
    CleansiaCheckboxComponent,
    CleansiaFilterChipsComponent,
    CleansiaFilterDrawerComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaStatusBadgeComponent,
    CleansiaTableComponent,
    CleansiaTextInputComponent,
    CleansiaTitleComponent,
    CleansiaPermissionDirective,
  ],
  templateUrl: './membership-plan-list.component.html',
  providers: [MembershipPlanListFacade],
})
export class MembershipPlanListComponent implements OnInit {
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  private readonly permissions = inject(PermissionService);
  protected readonly facade = inject(MembershipPlanListFacade);
  protected readonly Policy = Policy;

  private readonly statusTemplate = viewChild<TemplateRef<MembershipPlanListItem>>('statusTemplate');

  protected readonly table = computed(() => {
    this.facade.lang();
    return getMembershipPlanTableDefinition(
      {
        onEdit: (row) => this.editPlan(row),
        onDeactivate: (row) => this.facade.deactivatePlan(row),
      },
      this.translate,
      this.permissions,
      this.statusTemplate()
    );
  });

  ngOnInit(): void {
    this.facade.loadPlans();
  }

  createPlan(): void {
    this.router.navigate([`/${CleansiaAdminRoute.MEMBERSHIP_PLAN_MANAGEMENT}`, 'new']);
  }

  onPageChange(event: PaginationState): void {
    this.facade.onPageChange(event.first, event.rows);
  }

  private editPlan(row: MembershipPlanListItem): void {
    if (!row.id) return;
    this.router.navigate([`/${CleansiaAdminRoute.MEMBERSHIP_PLAN_MANAGEMENT}`, row.id, 'edit']);
  }
}
