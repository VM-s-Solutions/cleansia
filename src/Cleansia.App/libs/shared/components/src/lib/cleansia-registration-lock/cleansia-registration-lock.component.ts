import { CommonModule } from '@angular/common';
import { Component, inject, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import {
  CleansiaPartnerRoute,
  RegistrationCompletionResult,
  RegistrationCompletionService,
} from '@cleansia/services';
import { TranslateModule } from '@ngx-translate/core';
import { Observable } from 'rxjs';

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
    this.registrationStatus$ =
      this.registrationService.checkRegistrationCompletion();
  }

  goToProfile() {
    this.router.navigate([CleansiaPartnerRoute.PROFILE]);
  }
}
