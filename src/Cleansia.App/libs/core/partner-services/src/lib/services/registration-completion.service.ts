import { Injectable, inject } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { ContractStatus, RegistrationCompletionStatus } from '../client/partner-client';
import { PartnerAuthService } from './partner-auth.service';

export interface RegistrationCompletionResult {
  isComplete: boolean;
  hasUploadedDocuments: boolean;
  hasCompletedProfile: boolean;
  hasSetAvailability: boolean;
  missingRequirements: string[];
  contractStatus: ContractStatus | null;
  awaitingApproval: boolean;
  isRejected: boolean;
  rejectionReason: string | null;
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
        hasSetAvailability: false,
        missingRequirements: [
          this.translate.instant('api.common.user_not_authenticated'),
        ],
        contractStatus: null,
        awaitingApproval: false,
        isRejected: false,
        rejectionReason: null,
      };
    }

    if (!employeeStatus) {
      return {
        isComplete: false,
        hasUploadedDocuments: false,
        hasCompletedProfile: false,
        hasSetAvailability: false,
        missingRequirements: [
          this.translate.instant('api.employee.data_not_available'),
        ],
        contractStatus: null,
        awaitingApproval: false,
        isRejected: false,
        rejectionReason: null,
      };
    }

    const missingRequirements: string[] = [];
    const hasUploadedDocuments = employeeStatus.areDocumentsUploaded;
    const hasCompletedProfile = employeeStatus.hasCompletedProfile;
    const hasSetAvailability = employeeStatus.hasSetAvailability ?? false;
    const contractStatus = employeeStatus.contractStatus ?? null;
    const awaitingApproval =
      hasCompletedProfile && hasUploadedDocuments && hasSetAvailability && contractStatus === ContractStatus.Pending;
    const isRejected = contractStatus === ContractStatus.Rejected;
    const isComplete =
      hasCompletedProfile &&
      hasUploadedDocuments &&
      hasSetAvailability &&
      (contractStatus === ContractStatus.Approved || contractStatus === ContractStatus.Active);

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
      hasSetAvailability,
      missingRequirements,
      contractStatus,
      awaitingApproval,
      isRejected,
      rejectionReason: employeeStatus.rejectionReason ?? null,
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
