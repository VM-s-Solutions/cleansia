import { Component, OnDestroy, OnInit, effect, inject, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { EmailType } from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaLanguageSwitcherComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTextareaComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { DialogModule } from 'primeng/dialog';
import { EmailTemplateFormFacade, EmailTemplateFormData } from './email-template-form.facade';

interface TemplateVariable {
  name: string;
  descriptionKey: string;
}

@Component({
  selector: 'lib-email-template-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslateModule,
    DialogModule,
    CleansiaButtonComponent,
    CleansiaLanguageSwitcherComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaTextareaComponent,
    CleansiaTextInputComponent,
    CleansiaTitleComponent,
  ],
  providers: [EmailTemplateFormFacade],
  templateUrl: './email-template-form.component.html',
})
export class EmailTemplateFormComponent implements OnInit, OnDestroy {
  readonly facade = inject(EmailTemplateFormFacade);
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly translate = inject(TranslateService);

  form!: FormGroup;
  testEmailForm!: FormGroup;
  templateId: string | null = null;
  showTestEmailDialog = signal(false);

  constructor() {
    effect(() => {
      const template = this.facade.template();
      if (template) {
        this.form.patchValue({
          value: template.value,
        });
      }
    });
  }

  ngOnInit(): void {
    this.templateId = this.route.snapshot.paramMap.get('templateId');

    this.initForm();

    if (this.templateId) {
      this.facade.loadTemplate(this.templateId);
    }
  }

  private initForm(): void {
    this.form = this.fb.group({
      value: ['', [Validators.required, Validators.maxLength(10000)]],
    });

    this.testEmailForm = this.fb.group({
      recipientEmail: ['', [Validators.required, Validators.email]],
    });
  }

  pageTitle = computed(() => {
    return this.translate.instant('pages.template_management.edit_email_template');
  });

  get templateKey(): string {
    return this.facade.template()?.key || '';
  }

  get languageCode(): string {
    return this.facade.template()?.languageCode || '';
  }

  get emailType(): EmailType | undefined {
    return this.facade.template()?.emailType;
  }

  get emailTypeName(): string {
    const type = this.emailType;
    if (type === undefined) return '';

    const typeNames: Record<EmailType, string> = {
      [EmailType.ConfirmationEmail]: 'Confirmation Email',
      [EmailType.ResetPassword]: 'Reset Password',
      [EmailType.OrderReceipt]: 'Order Receipt',
      [EmailType.PeriodClosed]: 'Period Closed',
      [EmailType.PeriodEndReminder]: 'Period End Reminder',
    };
    return typeNames[type] || '';
  }

  readonly availableVariables = computed<TemplateVariable[]>(() => {
    const type = this.facade.template()?.emailType;
    if (type === undefined) return [];

    const variablesByType: Record<EmailType, TemplateVariable[]> = {
      [EmailType.ConfirmationEmail]: [
        { name: '{{UserName}}', descriptionKey: 'pages.template_management.variables.user_name' },
        { name: '{{VerificationCode}}', descriptionKey: 'pages.template_management.variables.verification_code' },
      ],
      [EmailType.ResetPassword]: [
        { name: '{{UserName}}', descriptionKey: 'pages.template_management.variables.user_name' },
        { name: '{{VerificationCode}}', descriptionKey: 'pages.template_management.variables.verification_code' },
        { name: '{{ResetPasswordLink}}', descriptionKey: 'pages.template_management.variables.reset_password_link' },
      ],
      [EmailType.OrderReceipt]: [
        { name: '{{CustomerName}}', descriptionKey: 'pages.template_management.variables.customer_name' },
        { name: '{{OrderNumber}}', descriptionKey: 'pages.template_management.variables.order_number' },
        { name: '{{OrderDate}}', descriptionKey: 'pages.template_management.variables.order_date' },
        { name: '{{TotalAmount}}', descriptionKey: 'pages.template_management.variables.total_amount' },
        { name: '{{OrderStatusLink}}', descriptionKey: 'pages.template_management.variables.order_status_link' },
      ],
      [EmailType.PeriodClosed]: [
        { name: '{{EmployeeName}}', descriptionKey: 'pages.template_management.variables.employee_name' },
        { name: '{{PeriodLabel}}', descriptionKey: 'pages.template_management.variables.period_label' },
        { name: '{{StartDate}}', descriptionKey: 'pages.template_management.variables.start_date' },
        { name: '{{EndDate}}', descriptionKey: 'pages.template_management.variables.end_date' },
        { name: '{{ClosedAt}}', descriptionKey: 'pages.template_management.variables.closed_at' },
      ],
      [EmailType.PeriodEndReminder]: [
        { name: '{{EmployeeName}}', descriptionKey: 'pages.template_management.variables.employee_name' },
        { name: '{{PeriodLabel}}', descriptionKey: 'pages.template_management.variables.period_label' },
        { name: '{{StartDate}}', descriptionKey: 'pages.template_management.variables.start_date' },
        { name: '{{EndDate}}', descriptionKey: 'pages.template_management.variables.end_date' },
        { name: '{{DaysRemaining}}', descriptionKey: 'pages.template_management.variables.days_remaining' },
      ],
    };

    return variablesByType[type] || [];
  });

  copyVariable(variable: string): void {
    navigator.clipboard.writeText(variable);
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const formData: EmailTemplateFormData = {
      value: this.form.value.value,
    };

    if (this.templateId) {
      this.facade.updateTemplate(this.templateId, formData);
    }
  }

  onCancel(): void {
    this.facade.navigateBack();
  }

  openTestEmailDialog(): void {
    this.testEmailForm.reset();
    this.showTestEmailDialog.set(true);
  }

  closeTestEmailDialog(): void {
    this.showTestEmailDialog.set(false);
  }

  onSendTestEmail(): void {
    if (this.testEmailForm.invalid) {
      this.testEmailForm.markAllAsTouched();
      return;
    }

    if (this.templateId) {
      this.facade.sendTestEmail(this.templateId, this.testEmailForm.value.recipientEmail);
      this.closeTestEmailDialog();
    }
  }

  ngOnDestroy(): void {
    this.facade.ngOnDestroy();
  }
}
