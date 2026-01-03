import { CommonModule } from '@angular/common';
import { Component, inject, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import {
  RegistrationCompletionResult,
  RegistrationCompletionService,
  RegistrationCompletionStatus,
} from '@cleansia/partner-services';
import { CleansiaPartnerRoute } from '@cleansia/services';
import { TranslateModule } from '@ngx-translate/core';
import { Observable, of } from 'rxjs';

@Component({
  selector: 'cleansia-registration-lock',
  imports: [CommonModule, TranslateModule],
  templateUrl: './cleansia-registration-lock.component.html',
})
export class CleansiaRegistrationLockComponent implements OnInit {
  private readonly registrationService = inject(RegistrationCompletionService);
  private readonly router = inject(Router);

  registrationStatus$!: Observable<RegistrationCompletionResult | null>;

  ngOnInit() {
    // TODO: Get actual employee status from store/service when available
    // For now, using null to indicate no employee data available
    const employeeStatus: RegistrationCompletionStatus | null = null;
    const result =
      this.registrationService.checkRegistrationCompletion(employeeStatus);
    this.registrationStatus$ = of(result);
  }

  goToProfile() {
    this.router.navigate([CleansiaPartnerRoute.PROFILE]);
  }
}
