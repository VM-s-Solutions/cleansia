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
import { Router } from '@angular/router';
import { MembershipPlanListItem } from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaCheckboxComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTableComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
  PaginationState,
  TableAction,
  TableColumn,
} from '@cleansia/components';
import { CleansiaPermissionDirective } from '@cleansia/directives';
import { CleansiaAdminRoute, Policy } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ConfirmationService } from 'primeng/api';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import { MembershipPlanListFacade } from './membership-plan-list.facade';
import { getMembershipPlanTableDefinition } from './membership-plan-list.models';

@Component({
  selector: 'cleansia-admin-membership-plan-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslatePipe,
    CleansiaButtonComponent,
    CleansiaCheckboxComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaTableComponent,
    CleansiaTextInputComponent,
    CleansiaTitleComponent,
    CleansiaPermissionDirective,
  ],
  templateUrl: './membership-plan-list.component.html',
  providers: [MembershipPlanListFacade, ConfirmationService],
})
export class MembershipPlanListComponent implements AfterViewInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly cd = inject(ChangeDetectorRef);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  private readonly confirmationService = inject(ConfirmationService);
  protected readonly facade = inject(MembershipPlanListFacade);
  protected readonly Policy = Policy;

  readonly statusTemplate =
    viewChild<TemplateRef<MembershipPlanListItem>>('statusTemplate');

  planColumns!: TableColumn<MembershipPlanListItem>[];
  planActions!: TableAction<MembershipPlanListItem>[];

  private readonly destroy$ = new Subject<void>();

  filterForm = this.fb.group({
    search: this.fb.control<string>('', { nonNullable: true }),
    activeOnly: this.fb.control<boolean>(false, { nonNullable: true }),
  });

  ngAfterViewInit(): void {
    this.rebuildTableDefinitions();
    this.cd.detectChanges();

    this.filterForm.controls.search.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => this.applyFilters());

    this.filterForm.controls.activeOnly.valueChanges
      .pipe(distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => this.applyFilters());

    this.translate.onLangChange.pipe(takeUntil(this.destroy$)).subscribe(() => {
      this.rebuildTableDefinitions();
      this.cd.detectChanges();
    });

    this.facade.loadPlans();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    this.facade.ngOnDestroy();
  }

  createPlan(): void {
    this.router.navigate([
      `/${CleansiaAdminRoute.MEMBERSHIP_PLAN_MANAGEMENT}`,
      'new',
    ]);
  }

  applyFilters(): void {
    const v = this.filterForm.getRawValue();
    this.facade.applyFilter({
      search: v.search.trim() || undefined,
      active: v.activeOnly ? true : undefined,
    });
  }

  onPageChange(event: PaginationState): void {
    this.facade.onPageChange(event.first, event.rows);
  }

  private rebuildTableDefinitions(): void {
    const tableDef = getMembershipPlanTableDefinition(
      {
        onEdit: (row) => this.editPlan(row),
        onDeactivate: (row) => this.confirmDeactivate(row),
      },
      this.translate,
      this.statusTemplate()
    );
    this.planColumns = tableDef.columns;
    this.planActions = tableDef.actions;
  }

  private editPlan(row: MembershipPlanListItem): void {
    if (!row.id) return;
    this.router.navigate([
      `/${CleansiaAdminRoute.MEMBERSHIP_PLAN_MANAGEMENT}`,
      row.id,
      'edit',
    ]);
  }

  private confirmDeactivate(row: MembershipPlanListItem): void {
    this.confirmationService.confirm({
      message: this.translate.instant(
        'pages.membership_plans.deactivate_confirm.message',
        { code: row.code }
      ),
      header: this.translate.instant(
        'pages.membership_plans.deactivate_confirm.title'
      ),
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: this.translate.instant(
        'pages.membership_plans.deactivate_confirm.yes'
      ),
      rejectLabel: this.translate.instant('global.actions.cancel'),
      accept: () => this.facade.deactivatePlan(row),
    });
  }
}
