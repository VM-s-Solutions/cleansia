import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { CleansiaButtonComponent, CleansiaTitleComponent } from '@cleansia/components';
import { TranslatePipe } from '@ngx-translate/core';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { FormsModule } from '@angular/forms';
import { GdprFacade } from './gdpr.facade';

@Component({
  selector: 'cleansia-customer-gdpr',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TranslatePipe,
    ToggleSwitchModule,
    CleansiaButtonComponent,
    CleansiaTitleComponent,
  ],
  templateUrl: './gdpr.component.html',
  providers: [GdprFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GdprComponent implements OnInit {
  protected readonly facade = inject(GdprFacade);

  // Re-expose facade signals/methods for template usage without refactoring the markup.
  readonly isAuthenticated = this.facade.isAuthenticated;
  readonly loadingConsents = this.facade.loadingConsents;
  readonly exporting = this.facade.exporting;
  readonly deleting = this.facade.deleting;

  ngOnInit(): void {
    if (this.isAuthenticated()) {
      this.facade.loadConsents();
    } else {
      this.facade.loadingConsents.set(false);
    }
  }

  exportData(): void {
    this.facade.exportData();
  }

  deleteAccount(): void {
    this.facade.confirmDeleteAccount();
  }
}
