import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Code, EmployeeDocumentItem, EmployeeEntityType, TimeRange } from '@cleansia/admin-services';
import { selectDayOfWeekCodes } from '@cleansia/admin-stores';
import {
  CleansiaAvailabilityComponent,
  CleansiaButtonComponent,
  CleansiaCalendarComponent,
  CleansiaLanguageSwitcherComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaSelectComponent,
  CleansiaTelephoneComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
  ICleansiaSelectOption,
} from '@cleansia/components';
import { CleansiaAdminRoute } from '@cleansia/services';
import { Store } from '@ngrx/store';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { DialogService } from 'primeng/dynamicdialog';
import { ToastModule } from 'primeng/toast';
import { EmployeeDetailFacade } from './employee-detail.facade';

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
    CleansiaLanguageSwitcherComponent,
    ToastModule,
  ],
  templateUrl: './employee-detail.component.html',
  providers: [EmployeeDetailFacade, DialogService],
})
export class EmployeeDetailComponent implements OnInit, OnDestroy {
  protected readonly facade = inject(EmployeeDetailFacade);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly store = inject(Store);
  private readonly translate = inject(TranslateService);

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

  ngOnInit(): void {
    const employeeId = this.route.snapshot.paramMap.get('employeeId');
    if (employeeId) {
      this.facade.loadEmployeeDetail(employeeId);
    } else {
      this.router.navigate([CleansiaAdminRoute.EMPLOYEE_MANAGEMENT]);
    }

    this.store.select(selectDayOfWeekCodes).subscribe((codes: Code[]) => {
      this.daysOfWeek.set(codes);
    });

    this.editForm.controls.entityType.valueChanges.subscribe((value) => {
      this.isLegalEntity.set(value === EmployeeEntityType.LegalEntity);
    });
  }

  ngOnDestroy(): void {
    this.facade.ngOnDestroy();
  }

  goBack(): void {
    this.router.navigate([CleansiaAdminRoute.EMPLOYEE_MANAGEMENT]);
  }

  getContractStatusClass(status: string): string {
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
    this.facade.openRejectDocumentDialog(document);
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

  onEditSection(section: string): void {
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
