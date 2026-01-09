import { CommonModule } from '@angular/common';
import { AfterViewInit, Component, inject, OnDestroy } from '@angular/core';
import { CurrencyListItem } from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaLanguageSwitcherComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTableComponent,
  CleansiaTitleComponent,
  TableDefinition,
} from '@cleansia/components';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ConfirmationService } from 'primeng/api';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { Subject } from 'rxjs';
import { CurrencyManagementFacade } from './currency-management.facade';
import { getCurrencyTableDefinition } from './currency-management.models';

@Component({
  selector: 'cleansia-admin-currency-management',
  standalone: true,
  imports: [
    CommonModule,
    CleansiaButtonComponent,
    TranslatePipe,
    CleansiaTableComponent,
    CleansiaTitleComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaLanguageSwitcherComponent,
    ConfirmDialogModule,
  ],
  templateUrl: './currency-management.component.html',
  providers: [CurrencyManagementFacade, ConfirmationService],
})
export class CurrencyManagementComponent implements AfterViewInit, OnDestroy {
  protected readonly facade = inject(CurrencyManagementFacade);
  private readonly translate = inject(TranslateService);
  private readonly confirmationService = inject(ConfirmationService);

  currencyTableDefinition!: TableDefinition<CurrencyListItem>;

  private destroy$ = new Subject<void>();

  ngAfterViewInit(): void {
    this.currencyTableDefinition = getCurrencyTableDefinition(
      {
        onEdit: this.editCurrency.bind(this),
        onDelete: this.confirmDeleteCurrency.bind(this),
      },
      this.translate
    );

    this.facade.loadCurrencies();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  createCurrency(): void {
    this.facade.navigateToCreateCurrency();
  }

  editCurrency(currency: CurrencyListItem): void {
    this.facade.navigateToEditCurrency(currency);
  }

  confirmDeleteCurrency(currency: CurrencyListItem): void {
    if ((currency as any).isDefault) {
      this.confirmationService.confirm({
        message: this.translate.instant('pages.currency_management.cannot_delete_default'),
        header: this.translate.instant('pages.currency_management.delete_currency'),
        icon: 'pi pi-exclamation-triangle',
        rejectVisible: false,
        acceptLabel: this.translate.instant('global.actions.ok'),
      });
      return;
    }

    this.confirmationService.confirm({
      message: this.translate.instant('pages.currency_management.delete_confirm'),
      header: this.translate.instant('pages.currency_management.delete_currency'),
      icon: 'pi pi-exclamation-triangle',
      accept: () => {
        this.facade.deleteCurrency(currency);
      },
    });
  }
}