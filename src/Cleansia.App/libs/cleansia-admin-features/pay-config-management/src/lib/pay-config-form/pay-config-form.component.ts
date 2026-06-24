import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  OnDestroy,
  OnInit,
  signal,
} from '@angular/core';
import {
  FormBuilder,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { CleansiaPermissionDirective } from '@cleansia/directives';
import {
  AuditResourceType,
  buildAuditResourceHistoryRoute,
  CleansiaAdminRoute,
  Policy,
} from '@cleansia/services';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaSelectComponent,
  CleansiaTextInputComponent,
  CleansiaTextareaComponent,
  CleansiaTitleComponent,
  ICleansiaSelectOption,
} from '@cleansia/components';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { PayConfigFormData, PayConfigFormFacade } from './pay-config-form.facade';
import { AdminPayConfigService } from '../admin-pay-config.service';

@Component({
  selector: 'cleansia-admin-pay-config-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslatePipe,
    CleansiaButtonComponent,
    CleansiaTextInputComponent,
    CleansiaTextareaComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaSelectComponent,
    CleansiaTitleComponent,
    CleansiaPermissionDirective,
  ],
  templateUrl: './pay-config-form.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [PayConfigFormFacade, AdminPayConfigService],
})
export class PayConfigFormComponent implements OnInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  protected readonly facade = inject(PayConfigFormFacade);

  private readonly mode = signal<'create' | 'edit'>('create');

  protected readonly Policy = Policy;

  readonly isEditMode = computed(() => this.mode() === 'edit');
  readonly pageTitle = computed(() =>
    this.isEditMode()
      ? this.translate.instant('pages.pay_config_form.edit_title')
      : this.translate.instant('pages.pay_config_form.create_title')
  );

  readonly form = this.fb.nonNullable.group({
    serviceId: [''],
    packageId: [''],
    basePay: [0, [Validators.required, Validators.min(0)]],
    extraPerRoom: [0, [Validators.min(0)]],
    extraPerBathroom: [0, [Validators.min(0)]],
    distanceRatePerKm: [0, [Validators.min(0)]],
    minimumPay: [0, [Validators.min(0)]],
    maximumPay: [0, [Validators.min(0)]],
    currencyId: ['', [Validators.required]],
    description: [''],
  });

  readonly serviceOptions = computed<ICleansiaSelectOption[]>(() =>
    this.facade.services().map((s) => ({ label: s.name, value: s.id }))
  );

  readonly packageOptions = computed<ICleansiaSelectOption[]>(() =>
    this.facade.packages().map((p) => ({ label: p.name, value: p.id }))
  );

  readonly currencyOptions = computed<ICleansiaSelectOption[]>(() =>
    this.facade.currencies().map((c) => ({ label: c.code, value: c.id }))
  );

  private payConfigLoadEffect = effect(() => {
    const payConfig = this.facade.payConfig();
    if (payConfig && this.isEditMode()) {
      this.form.patchValue({
        serviceId: payConfig.serviceId ?? '',
        packageId: payConfig.packageId ?? '',
        basePay: payConfig.basePay ?? 0,
        extraPerRoom: payConfig.extraPerRoom ?? 0,
        extraPerBathroom: payConfig.extraPerBathroom ?? 0,
        distanceRatePerKm: payConfig.distanceRatePerKm ?? 0,
        minimumPay: payConfig.minimumPay ?? 0,
        maximumPay: payConfig.maximumPay ?? 0,
        currencyId: payConfig.currencyId ?? '',
        description: payConfig.description ?? '',
      });
    }
  });

  ngOnInit(): void {
    const routeMode = this.route.snapshot.data['mode'] as 'create' | 'edit';
    if (routeMode) {
      this.mode.set(routeMode);
    }

    this.facade.loadServices();
    this.facade.loadPackages();
    this.facade.loadCurrencies();

    if (this.isEditMode()) {
      const payConfigId = this.route.snapshot.paramMap.get('payConfigId');
      if (payConfigId) {
        this.facade.loadPayConfig(payConfigId);
      } else {
        this.router.navigate([CleansiaAdminRoute.PAY_CONFIG_MANAGEMENT]);
      }
    }
  }

  ngOnDestroy(): void {
    this.facade.ngOnDestroy();
  }

  onSave(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const formValue = this.form.getRawValue();

    const data: PayConfigFormData = {
      serviceId: formValue.serviceId || undefined,
      packageId: formValue.packageId || undefined,
      basePay: formValue.basePay,
      extraPerRoom: formValue.extraPerRoom,
      extraPerBathroom: formValue.extraPerBathroom,
      distanceRatePerKm: formValue.distanceRatePerKm,
      minimumPay: formValue.minimumPay,
      maximumPay: formValue.maximumPay,
      currencyId: formValue.currencyId,
      description: formValue.description || undefined,
    };

    if (this.isEditMode()) {
      const payConfigId = this.route.snapshot.paramMap.get('payConfigId');
      if (payConfigId) {
        this.facade.updatePayConfig(payConfigId, data);
      }
    } else {
      this.facade.createPayConfig(data);
    }
  }

  onCancel(): void {
    this.facade.navigateBack();
  }

  viewAuditHistory(): void {
    const payConfigId = this.route.snapshot.paramMap.get('payConfigId');
    if (!payConfigId) return;
    this.router.navigate(
      buildAuditResourceHistoryRoute(
        AuditResourceType.EmployeePayConfig,
        payConfigId
      )
    );
  }
}
