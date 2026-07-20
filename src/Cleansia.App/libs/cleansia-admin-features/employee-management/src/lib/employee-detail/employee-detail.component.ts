import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Code, EmployeeDocumentItem, EmployeeEntityType, TimeRange } from '@cleansia/admin-services';
import { selectDayOfWeekCodes } from '@cleansia/admin-stores';
import {
  CleansiaAvailabilityComponent,
  CleansiaButtonComponent,
  CleansiaCalendarComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaSelectComponent,
  CleansiaTelephoneComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
  ICleansiaSelectOption,
} from '@cleansia/components';
import {
  AuditResourceType,
  buildAuditResourceHistoryRoute,
  CleansiaAdminRoute,
  Policy,
} from '@cleansia/services';
import { CleansiaPermissionDirective } from '@cleansia/directives';
import { Store } from '@ngrx/store';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { CheckboxModule } from 'primeng/checkbox';
import { DialogService } from 'primeng/dynamicdialog';
import { ToastModule } from 'primeng/toast';
import { EmployeeDetailFacade } from './employee-detail.facade';
import { EmployeeDocumentsFacade } from './employee-documents.facade';
import { EmployeeDocumentsSectionComponent } from './employee-documents-section.component';

@Component({
  selector: 'cleansia-admin-employee-detail',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    CleansiaButtonComponent,
    CleansiaAvailabilityComponent,
    CleansiaCalendarComponent,
    CleansiaSelectComponent,
    CleansiaTelephoneComponent,
    CleansiaTextInputComponent,
    TranslatePipe,
    CleansiaTitleComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CheckboxModule,
    ToastModule,
    EmployeeDocumentsSectionComponent,
    CleansiaPermissionDirective,
  ],
  templateUrl: './employee-detail.component.html',
  providers: [EmployeeDocumentsFacade, EmployeeDetailFacade, DialogService],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EmployeeDetailComponent implements OnInit, OnDestroy {
  protected readonly facade = inject(EmployeeDetailFacade);
  protected readonly docsFacade = inject(EmployeeDocumentsFacade);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly store = inject(Store);
  private readonly translate = inject(TranslateService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly Policy = Policy;

  readonly daysOfWeek = signal<Code[]>([]);
  availabilityValue: { [key: string]: TimeRange[] } = {};

  // Reactive form backing every edit section — keyed so only the fields
  // belonging to the currently-open section are actually displayed.
  readonly editForm = new FormGroup({
    // personal
    firstName: new FormControl<string | null>(null, [Validators.required]),
    lastName: new FormControl<string | null>(null, [Validators.required]),
    phoneNumber: new FormControl<string | null>(null),
    birthDate: new FormControl<Date | null>(null),
    // address
    street: new FormControl<string | null>(null),
    city: new FormControl<string | null>(null),
    zipCode: new FormControl<string | null>(null),
    countryId: new FormControl<string | null>(null),
    // employment / business identity
    nationalityId: new FormControl<string | null>(null),
    passportId: new FormControl<string | null>(null),
    entityType: new FormControl<EmployeeEntityType>(
      EmployeeEntityType.NaturalPerson,
      [Validators.required]
    ),
    registrationNumber: new FormControl<string | null>(null),
    vatNumber: new FormControl<string | null>(null),
    legalEntityName: new FormControl<string | null>(null),
    iban: new FormControl<string | null>(null),
    // emergency contact
    emergencyContactName: new FormControl<string | null>(null),
    emergencyContactPhone: new FormControl<string | null>(null),
  });

  readonly entityTypeOptions: ICleansiaSelectOption[] = [
    {
      value: EmployeeEntityType.NaturalPerson,
      label: this.translate.instant(
        'pages.employee_detail.entity_type_natural_person'
      ),
    },
    {
      value: EmployeeEntityType.LegalEntity,
      label: this.translate.instant(
        'pages.employee_detail.entity_type_legal_entity'
      ),
    },
  ];

  readonly isLegalEntity = signal(false);

  readonly payConfigForm = new FormGroup({
    serviceId: new FormControl<string | null>(null),
    packageId: new FormControl<string | null>(null),
    currencyId: new FormControl<string | null>(null, [Validators.required]),
    basePay: new FormControl<number>(0, [Validators.required, Validators.min(0)]),
    extraPerRoom: new FormControl<number>(0, [Validators.min(0)]),
    extraPerBathroom: new FormControl<number>(0, [Validators.min(0)]),
    distanceRatePerKm: new FormControl<number>(0, [Validators.min(0)]),
    minimumPay: new FormControl<number>(0, [Validators.min(0)]),
    maximumPay: new FormControl<number>(0, [Validators.min(0)]),
    description: new FormControl<string | null>(null),
  });

  readonly bulkGradeForm = new FormGroup({
    grade: new FormControl<string | null>(null, [Validators.required]),
    currencyId: new FormControl<string | null>(null, [Validators.required]),
    overwriteExisting: new FormControl<boolean>(false),
  });

  readonly gradeOptions: ICleansiaSelectOption[] = [
    {
      label: this.translate.instant('pages.employee_detail.grade.junior'),
      value: 'junior',
    },
    {
      label: this.translate.instant('pages.employee_detail.grade.medior'),
      value: 'medior',
    },
    {
      label: this.translate.instant('pages.employee_detail.grade.senior'),
      value: 'senior',
    },
  ];

  ngOnInit(): void {
    const employeeId = this.route.snapshot.paramMap.get('employeeId');
    if (employeeId) {
      this.facade.loadEmployeeDetail(employeeId);
      this.facade.loadEmployeePayConfigs(employeeId);
      this.facade.loadPayConfigOptions();
    } else {
      this.router.navigate([CleansiaAdminRoute.EMPLOYEE_MANAGEMENT]);
    }

    this.store
      .select(selectDayOfWeekCodes)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((codes: Code[]) => {
        this.daysOfWeek.set(codes);
      });

    this.editForm.controls.entityType.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((value) => {
        this.isLegalEntity.set(value === EmployeeEntityType.LegalEntity);
      });
  }

  ngOnDestroy(): void {
    this.facade.ngOnDestroy();
    this.docsFacade.ngOnDestroy();
  }

  goBack(): void {
    this.router.navigate([CleansiaAdminRoute.EMPLOYEE_MANAGEMENT]);
  }

  viewAuditHistory(): void {
    const userId = this.facade.employee()?.userId;
    if (!userId) return;
    this.router.navigate(
      buildAuditResourceHistoryRoute(AuditResourceType.User, userId)
    );
  }

  getContractStatusClass(status: string | undefined): string {
    const statusName = status?.toLowerCase().replace(/\s+/g, '-') || 'pending';
    return `contract-status-badge status-${statusName}`;
  }

  formatDate(date: string | Date | null | undefined): string {
    if (!date) return '-';
    const dateObj = date instanceof Date ? date : new Date(date);
    return dateObj.toLocaleDateString('en-GB');
  }

  formatDateTime(date: string | Date | null | undefined): string {
    if (!date) return '-';
    const dateObj = date instanceof Date ? date : new Date(date);
    return dateObj.toLocaleString('en-GB');
  }

  formatTimeRange(start: unknown, end: unknown): string {
    if (!start || !end) return '-';
    return `${start} - ${end}`;
  }

  onRejectDocument(document: EmployeeDocumentItem): void {
    this.docsFacade.openRejectDocumentDialog(document, this.facade.employee()?.id);
  }

  onEditAvailability(): void {
    const employee = this.facade.employee();
    this.availabilityValue = employee?.availability
      ? { ...employee.availability }
      : {};
    this.facade.startEditingAvailability();
  }

  onSaveAvailability(): void {
    this.facade.saveAvailability(this.availabilityValue);
  }

  onCancelEditAvailability(): void {
    this.facade.cancelEditingAvailability();
  }

  applyGradeMultiplier(multiplier: number): void {
    // Load global configs and apply multiplier as base rates
    const basePay = this.payConfigForm.controls.basePay.value ?? 0;
    const baseRate = basePay > 0 ? basePay : 200; // default base if empty
    this.payConfigForm.patchValue({
      basePay: Math.round(baseRate * multiplier * 100) / 100,
      extraPerRoom: Math.round(50 * multiplier * 100) / 100,
      extraPerBathroom: Math.round(30 * multiplier * 100) / 100,
      distanceRatePerKm: Math.round(10 * multiplier * 100) / 100,
    });
  }

  onSavePayConfig(): void {
    this.facade.createEmployeePayConfig(this.payConfigForm.getRawValue());
  }

  onDeletePayConfig(payConfigId: string): void {
    this.facade.deleteEmployeePayConfig(payConfigId);
  }

  onBulkApplyGrade(): void {
    if (this.bulkGradeForm.invalid) {
      this.bulkGradeForm.markAllAsTouched();
      return;
    }
    const { grade, currencyId, overwriteExisting } = this.bulkGradeForm.getRawValue();
    if (!grade || !currencyId) return;
    this.facade.bulkApplyGrade(grade, currencyId, overwriteExisting ?? false);
  }

  onEditSection(section: string): void {
    if (section === 'payConfig') {
      this.payConfigForm.reset({
        serviceId: null,
        packageId: null,
        currencyId: null,
        basePay: 0,
        extraPerRoom: 0,
        extraPerBathroom: 0,
        distanceRatePerKm: 0,
        minimumPay: 0,
        maximumPay: 0,
        description: null,
      });
      this.facade.startEditingSection(section);
      return;
    }
    const employee = this.facade.employee();
    if (!employee) return;
    this.editForm.reset({
      firstName: employee.firstName ?? null,
      lastName: employee.lastName ?? null,
      phoneNumber: employee.phoneNumber ?? null,
      birthDate: employee.birthDate ? new Date(employee.birthDate) : null,
      street: employee.street ?? null,
      city: employee.city ?? null,
      zipCode: employee.zipCode ?? null,
      countryId: employee.countryId ?? null,
      nationalityId: employee.nationalityId ?? null,
      passportId: employee.passportId ?? null,
      entityType: employee.entityType ?? EmployeeEntityType.NaturalPerson,
      registrationNumber: employee.registrationNumber ?? null,
      vatNumber: employee.vatNumber ?? null,
      legalEntityName: employee.legalEntityName ?? null,
      iban: employee.iban ?? null,
      emergencyContactName: employee.emergencyContactName ?? null,
      emergencyContactPhone: employee.emergencyContactPhone ?? null,
    });
    this.isLegalEntity.set(
      (employee.entityType ?? EmployeeEntityType.NaturalPerson) ===
        EmployeeEntityType.LegalEntity
    );
    this.facade.startEditingSection(section);
  }

  onSaveSection(): void {
    this.facade.updateEmployee(this.editForm.getRawValue());
  }

  onCancelEditSection(): void {
    this.facade.cancelEditingSection();
  }
}
