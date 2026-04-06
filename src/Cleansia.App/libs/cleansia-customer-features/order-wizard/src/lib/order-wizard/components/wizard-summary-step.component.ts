import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, Input } from '@angular/core';
import { PackageListItem, PaymentType, ServiceListItem } from '@cleansia/partner-services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { OrderWizardFacade } from '../order-wizard.facade';
import { formatPrice, getItemTranslation } from '../order-wizard.models';

@Component({
  selector: 'cleansia-wizard-summary-step',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, TranslatePipe],
  template: `
    <div class="order-wizard__step">
      <div class="order-wizard__section">
        <h2 class="order-wizard__section-title">
          <i class="pi pi-check-circle"></i>
          {{ 'pages.order.summary_section_title' | translate }}
        </h2>

        @if (selectedServices().length > 0) {
          <ng-container
            *ngTemplateOutlet="summaryCard; context: {
              icon: 'pi-list',
              titleKey: 'pages.order.summary_services',
              editStep: 0,
              rows: serviceRows()
            }"
          />
        }

        @if (selectedPackages().length > 0) {
          <ng-container
            *ngTemplateOutlet="summaryCard; context: {
              icon: 'pi-box',
              titleKey: 'pages.order.summary_packages',
              editStep: 0,
              rows: packageRows()
            }"
          />
        }

        <ng-container
          *ngTemplateOutlet="summaryCard; context: {
            icon: 'pi-building',
            titleKey: 'pages.order.summary_property',
            editStep: 0,
            rows: propertyRows()
          }"
        />

        <ng-container
          *ngTemplateOutlet="summaryCard; context: {
            icon: 'pi-map-marker',
            titleKey: 'pages.order.summary_address',
            editStep: 1,
            rows: addressRows()
          }"
        />

        <ng-container
          *ngTemplateOutlet="summaryCard; context: {
            icon: 'pi-calendar',
            titleKey: 'pages.order.summary_datetime',
            editStep: 2,
            rows: dateTimeRows()
          }"
        />

        <ng-container
          *ngTemplateOutlet="summaryCard; context: {
            icon: 'pi-credit-card',
            titleKey: 'pages.order.summary_payment',
            editStep: 3,
            rows: paymentRows()
          }"
        />

        <ng-container
          *ngTemplateOutlet="summaryCard; context: {
            icon: 'pi-user',
            titleKey: 'pages.order.summary_contact',
            editStep: 1,
            rows: contactRows()
          }"
        />

        @if (facade.formData().specialInstructions || facade.formData().entryInstructions) {
          <ng-container
            *ngTemplateOutlet="summaryCard; context: {
              icon: 'pi-comment',
              titleKey: 'pages.order.summary_instructions',
              editStep: 3,
              rows: instructionRows()
            }"
          />
        }
      </div>
    </div>

    <ng-template #summaryCard let-icon="icon" let-titleKey="titleKey" let-editStep="editStep" let-rows="rows">
      <div class="order-wizard__summary-card">
        <div class="order-wizard__summary-card-header">
          <div class="order-wizard__summary-card-icon"><i class="pi {{ icon }}"></i></div>
          <h3>{{ titleKey | translate }}</h3>
          <button class="order-wizard__summary-edit" (click)="facade.goToStep(editStep)">
            {{ 'pages.order.summary_edit' | translate }}
          </button>
        </div>
        <div class="order-wizard__summary-card-body">
          @for (row of rows; track $index) {
            <div class="order-wizard__summary-row">
              <span>{{ row.label }}</span>
              @if (row.value) {
                <span class="order-wizard__summary-row-price">{{ row.value }}</span>
              }
            </div>
          }
        </div>
      </div>
    </ng-template>
  `,
})
export class WizardSummaryStepComponent {
  @Input({ required: true }) facade!: OrderWizardFacade;
  private readonly translate = inject(TranslateService);
  protected readonly PaymentType = PaymentType;

  readonly selectedServices = computed(() => {
    const ids = this.facade.formData().selectedServiceIds;
    return this.facade.services().filter((s) => s.id && ids.includes(s.id));
  });

  readonly selectedPackages = computed(() => {
    const ids = this.facade.formData().selectedPackageIds;
    return this.facade.packages().filter((p) => p.id && ids.includes(p.id));
  });

  readonly serviceRows = computed(() =>
    this.selectedServices().map((s) => ({
      label: getItemTranslation(s, 'name', this.translate),
      value: formatPrice(s.basePrice),
    }))
  );

  readonly packageRows = computed(() =>
    this.selectedPackages().map((p) => ({
      label: getItemTranslation(p, 'name', this.translate),
      value: formatPrice(p.price),
    }))
  );

  readonly propertyRows = computed(() => {
    const d = this.facade.formData();
    return [
      { label: this.translate.instant('pages.order.rooms_label'), value: String(d.rooms) },
      { label: this.translate.instant('pages.order.bathrooms_label'), value: String(d.bathrooms) },
    ];
  });

  readonly addressRows = computed(() => {
    const a = this.facade.formData().address;
    return [{ label: `${a.street}, ${a.city} ${a.zipCode}` }];
  });

  readonly dateTimeRows = computed(() => {
    const d = this.facade.formData();
    const dateStr = d.cleaningDate
      ? new Date(d.cleaningDate).toLocaleDateString('cs-CZ')
      : '';
    return [{ label: `${dateStr} ${d.cleaningTime}` }];
  });

  readonly paymentRows = computed(() => {
    const key =
      this.facade.formData().paymentType === PaymentType.Card
        ? 'pages.order.payment_card_title'
        : 'pages.order.payment_cash_title';
    return [{ label: this.translate.instant(key) }];
  });

  readonly contactRows = computed(() => {
    const d = this.facade.formData();
    return [
      { label: `${d.customerFirstName} ${d.customerLastName}` },
      { label: d.customerEmail },
      { label: d.customerPhone },
    ];
  });

  readonly instructionRows = computed(() => {
    const d = this.facade.formData();
    const rows: { label: string }[] = [];
    if (d.specialInstructions) rows.push({ label: d.specialInstructions });
    if (d.entryInstructions) rows.push({ label: d.entryInstructions });
    return rows;
  });
}
