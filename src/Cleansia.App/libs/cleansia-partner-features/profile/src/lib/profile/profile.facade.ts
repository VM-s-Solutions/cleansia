import { Injectable, inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MessageService } from 'primeng/api';

@Injectable()
export class ProfileFacade {
  private readonly formBuilder = inject(FormBuilder);
  private readonly messageService = inject(MessageService);

  readonly formGroup: FormGroup = this.formBuilder.group({
    fullName: ['', [Validators.required]],
    dateOfBirth: [null, [Validators.required]],
    street: ['', [Validators.required, Validators.maxLength(255)]],
    city: ['', [Validators.required, Validators.maxLength(100)]],
    zipCode: ['', [Validators.required, Validators.maxLength(20)]],
    countryId: ['', [Validators.required]],
    phone: ['', [Validators.required]],
    email: ['', [Validators.required, Validators.email]],
    nationalId: ['', [Validators.required]],
    taxId: [''],
    iban: ['', [Validators.required]],
    emergencyName: [''],
    emergencyPhone: [''],
    consent: [false, [Validators.requiredTrue]],
  });

  onDocumentUpload(event: any): void {
    this.messageService.add({
      severity: 'success',
      summary: 'Success',
      detail: 'Documents uploaded successfully',
    });
  }

  onSubmit(): void {
    if (this.formGroup.valid) {
      console.log('Form Data:', this.formGroup.value);
      this.messageService.add({
        severity: 'info',
        summary: 'Submitted',
        detail: 'Your onboarding information has been submitted.',
      });
    } else {
      this.messageService.add({
        severity: 'error',
        summary: 'Error',
        detail: 'Please fill in all required fields.',
      });
      this.markAllFieldsAsTouched();
    }
  }

  private markAllFieldsAsTouched(): void {
    Object.keys(this.formGroup.controls).forEach(key => {
      this.formGroup.get(key)?.markAsTouched();
    });
  }
}