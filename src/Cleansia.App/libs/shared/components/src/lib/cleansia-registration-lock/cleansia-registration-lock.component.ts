import { CommonModule } from '@angular/common';
import { Component, inject, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import {
  RegistrationCompletionResult,
  RegistrationCompletionService,
  RegistrationCompletionStatus,
} from '@cleansia/partner-services';
import {
  checkEmployeeCurrent,
  selectEmployeeConfirmation,
} from '@cleansia/partner-stores';
import { CleansiaPartnerRoute } from '@cleansia/services';
import { Store } from '@ngrx/store';
import { TranslateModule } from '@ngx-translate/core';
import { map, Observable } from 'rxjs';

export interface RegistrationCategory {
  key: string;
  translationKey: string;
  icon: string;
  status: 'done' | 'pending' | 'missing';
  details: string[];
}

export interface EnhancedRegistrationStatus {
  result: RegistrationCompletionResult;
  completedSteps: number;
  totalSteps: number;
  categories: RegistrationCategory[];
}

@Component({
  selector: 'cleansia-registration-lock',
  imports: [CommonModule, TranslateModule],
  templateUrl: './cleansia-registration-lock.component.html',
})
export class CleansiaRegistrationLockComponent implements OnInit {
  private readonly registrationService = inject(RegistrationCompletionService);
  private readonly router = inject(Router);
  private readonly store = inject(Store);

  registrationStatus$!: Observable<EnhancedRegistrationStatus | null>;

  ngOnInit() {
    this.store.dispatch(checkEmployeeCurrent());
    this.registrationStatus$ = this.store
      .select(selectEmployeeConfirmation)
      .pipe(
        map((employeeStatus) => {
          const result = this.registrationService.checkRegistrationCompletion(
            employeeStatus ?? null
          );
          return this.buildEnhancedStatus(result, employeeStatus ?? null);
        })
      );
  }

  goToProfile() {
    this.router.navigate([CleansiaPartnerRoute.PROFILE]);
  }

  getStatusIcon(status: 'done' | 'pending' | 'missing'): string {
    switch (status) {
      case 'done':
        return 'pi pi-check-circle';
      case 'pending':
        return 'pi pi-clock';
      case 'missing':
        return 'pi pi-times-circle';
    }
  }

  private buildEnhancedStatus(
    result: RegistrationCompletionResult,
    employeeStatus: RegistrationCompletionStatus | null
  ): EnhancedRegistrationStatus {
    const categories: RegistrationCategory[] = [];

    const profileMissing = employeeStatus?.missingFields ?? [];
    categories.push({
      key: 'profile',
      translationKey: 'registrationLock.categories.profile',
      icon: 'pi pi-user',
      status: result.hasCompletedProfile ? 'done' : 'missing',
      details: result.hasCompletedProfile ? [] : profileMissing,
    });

    categories.push({
      key: 'documents',
      translationKey: 'registrationLock.categories.documents',
      icon: 'pi pi-file',
      status: result.hasUploadedDocuments ? 'done' : 'missing',
      details: result.hasUploadedDocuments ? [] : ['registrationLock.categories.documentsRequired'],
    });

    let approvalStatus: 'done' | 'pending' | 'missing';
    const approvalDetails: string[] = [];
    if (result.isComplete) {
      approvalStatus = 'done';
    } else if (result.isRejected) {
      approvalStatus = 'missing';
      approvalDetails.push('registrationLock.categories.approvalRejected');
      if (result.rejectionReason) {
        approvalDetails.push(result.rejectionReason);
      }
    } else if (result.awaitingApproval) {
      approvalStatus = 'pending';
      approvalDetails.push('registrationLock.categories.approvalAwaitingReview');
    } else {
      approvalStatus = 'missing';
      approvalDetails.push('registrationLock.categories.approvalCompleteProfileFirst');
    }
    categories.push({
      key: 'approval',
      translationKey: 'registrationLock.categories.approval',
      icon: 'pi pi-shield',
      status: approvalStatus,
      details: approvalDetails,
    });

    const completedSteps = categories.filter((c) => c.status === 'done').length;
    const totalSteps = categories.length;

    return { result, completedSteps, totalSteps, categories };
  }
}
