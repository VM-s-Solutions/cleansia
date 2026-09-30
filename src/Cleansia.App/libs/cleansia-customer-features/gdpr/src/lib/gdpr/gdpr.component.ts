import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { CleansiaButtonComponent, CleansiaTitleComponent } from '@cleansia/components';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { FormsModule } from '@angular/forms';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService } from 'primeng/api';
import { GdprFacade } from './gdpr.facade';

@Component({
  selector: 'cleansia-customer-gdpr',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TranslatePipe,
    ToggleSwitchModule,
    ConfirmDialogModule,
    CleansiaButtonComponent,
    CleansiaTitleComponent,
  ],
  templateUrl: './gdpr.component.html',
  providers: [ConfirmationService, GdprFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GdprComponent implements OnInit {
  private readonly translate = inject(TranslateService);
  private readonly confirmService = inject(ConfirmationService);
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
    this.confirmService.confirm({
      message: this.translate.instant('pages.gdpr.delete_confirm_message'),
      header: this.translate.instant('pages.gdpr.delete_confirm_title'),
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: this.translate.instant('pages.gdpr.delete_confirm_yes'),
      rejectLabel: this.translate.instant('global.actions.cancel'),
      accept: () => {
        this.facade.deleteAccount();
      },
    });
  }
}
