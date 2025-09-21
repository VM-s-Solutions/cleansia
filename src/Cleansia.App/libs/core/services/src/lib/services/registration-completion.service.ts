import { Injectable, inject } from '@angular/core';
import { selectEmployeeConfirmation } from '@cleansia/stores';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { Observable, distinctUntilChanged, map, of, startWith } from 'rxjs';
import { AuthService } from './auth.service';

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
  private readonly store = inject(Store);
  private readonly authService = inject(AuthService);
  private readonly translate = inject(TranslateService);

  /**
   * Checks if the current employee has completed their registration
   * Based on the isEmployeeConfirmed property from the backend
   */
  checkRegistrationCompletion(): Observable<RegistrationCompletionResult> {
    if (!this.authService.isLoggedIn()) {
      return of({
        isComplete: false,
        hasUploadedDocuments: false,
        hasCompletedProfile: false,
        missingRequirements: [
          this.translate.instant('api.common.user_not_authenticated'),
        ],
      });
    }

    return this.store.select(selectEmployeeConfirmation).pipe(
      map((checkResult) => {
        if (!checkResult) {
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
        let hasUploadedDocuments = false;
        let hasCompletedProfile = false;

        // Use the backend RegistrationCompletionStatus properties
        hasCompletedProfile = checkResult.hasCompletedProfile;
        hasUploadedDocuments = checkResult.areDocumentsUploaded;
        const isComplete = hasCompletedProfile && hasUploadedDocuments;

        // Add specific missing requirements with translations
        if (!hasCompletedProfile) {
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
      })
    );
  }

  /**
   * Quick check if registration is complete
   */
  isRegistrationComplete(): Observable<boolean> {
    return this.checkRegistrationCompletion().pipe(
      map((status) => status.isComplete),
      distinctUntilChanged(), // Only emit when the completion status actually changes
      startWith(false) // Start with false to ensure initial state
    );
  }
}
