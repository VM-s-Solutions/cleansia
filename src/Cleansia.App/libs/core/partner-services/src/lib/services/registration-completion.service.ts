import { Injectable, inject } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { RegistrationCompletionStatus } from '../client/partner-client';
import { PartnerAuthService } from './partner-auth.service';

export interface RegistrationCompletionResult {
  isComplete: boolean;
  hasUploadedDocuments: boolean;
  hasCompletedProfile: boolean;
  missingRequirements: string[];
}

@Injectable({
  providedIn: 'root',
})
export class RegistrationCompletionService {
  private readonly authService = inject(PartnerAuthService);
  private readonly translate = inject(TranslateService);

  /**
   * Checks registration completion status based on provided employee data
   * @param employeeStatus - The employee's registration completion status from the store
   */
  checkRegistrationCompletion(
    employeeStatus: RegistrationCompletionStatus | null
  ): RegistrationCompletionResult {
    if (!this.authService.isLoggedIn()) {
      return {
        isComplete: false,
        hasUploadedDocuments: false,
        hasCompletedProfile: false,
        missingRequirements: [
          this.translate.instant('api.common.user_not_authenticated'),
        ],
      };
    }

    if (!employeeStatus) {
      return {
        isComplete: false,
        hasUploadedDocuments: false,
        hasCompletedProfile: false,
        missingRequirements: [
          this.translate.instant('api.employee.data_not_available'),
        ],
      };
    }

    const missingRequirements: string[] = [];
    const hasUploadedDocuments = employeeStatus.areDocumentsUploaded;
    const hasCompletedProfile = employeeStatus.hasCompletedProfile;
    const isComplete = hasCompletedProfile && hasUploadedDocuments;

    // Add specific missing fields from the API
    if (
      employeeStatus.missingFields &&
      employeeStatus.missingFields.length > 0
    ) {
      for (const field of employeeStatus.missingFields) {
        missingRequirements.push(field);
      }
    } else if (!hasCompletedProfile) {
      missingRequirements.push(
        this.translate.instant('api.employee.profile_incomplete')
      );
    }

    if (!hasUploadedDocuments) {
      missingRequirements.push(
        this.translate.instant('api.employee.documents_missing')
      );
    }

    return {
      isComplete,
      hasUploadedDocuments,
      hasCompletedProfile,
      missingRequirements,
    };
  }

  /**
   * Quick check if registration is complete based on provided employee data
   */
  isRegistrationComplete(
    employeeStatus: RegistrationCompletionStatus | null
  ): boolean {
    return this.checkRegistrationCompletion(employeeStatus).isComplete;
  }
}
