import { CommonModule } from '@angular/common';
import {
  AfterViewInit,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  inject,
  OnDestroy,
  TemplateRef,
  viewChild,
} from '@angular/core';
import { CurrencyListItem } from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTableComponent,
  CleansiaTitleComponent,
  TableColumn,
  TableAction,
} from '@cleansia/components';
import { Policy } from '@cleansia/services';
import { CleansiaPermissionDirective } from '@cleansia/directives';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ConfirmationService } from 'primeng/api';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { Subject, takeUntil } from 'rxjs';
import { CurrencyManagementFacade } from './currency-management.facade';
import {
  getCurrencyFlagCode,
  getCurrencyTableDefinition,
} from './currency-management.models';

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
    ConfirmDialogModule,
    CleansiaPermissionDirective,
  ],
  templateUrl: './currency-management.component.html',
  providers: [CurrencyManagementFacade, ConfirmationService],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CurrencyManagementComponent implements AfterViewInit, OnDestroy {
  private readonly cd = inject(ChangeDetectorRef);
  protected readonly facade = inject(CurrencyManagementFacade);
  protected readonly Policy = Policy;
  private readonly translate = inject(TranslateService);
  private readonly confirmationService = inject(ConfirmationService);

  flagTemplate = viewChild<TemplateRef<any>>('flagTemplate');

  currencyColumns!: TableColumn<CurrencyListItem>[];
  currencyActions!: TableAction<CurrencyListItem>[];

  // Expose helper function to template
  getCurrencyFlagCode = getCurrencyFlagCode;

  private destroy$ = new Subject<void>();

  ngAfterViewInit(): void {
    this.rebuildTableDefinitions();
    this.cd.detectChanges();

    // Rebuild tables when language changes
    this.translate.onLangChange
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.rebuildTableDefinitions();
        this.cd.detectChanges();
      });

    this.facade.loadCurrencies();
  }

  private rebuildTableDefinitions(): void {
    const tableDef = getCurrencyTableDefinition(
      {
        onEdit: this.editCurrency.bind(this),
        onDelete: this.confirmDeleteCurrency.bind(this),
        onSetDefault: this.confirmSetDefaultCurrency.bind(this),
      },
      this.translate,
      this.flagTemplate()
    );
    this.currencyColumns = tableDef.columns;
    this.currencyActions = tableDef.actions;
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

  confirmSetDefaultCurrency(currency: CurrencyListItem): void {
    this.confirmationService.confirm({
      message: this.translate.instant(
        'pages.currency_management.set_default_confirm',
        { code: currency.code }
      ),
      header: this.translate.instant('pages.currency_management.set_default'),
      icon: 'pi pi-star',
      accept: () => {
        this.facade.setDefaultCurrency(currency);
      },
    });
  }

  confirmDeleteCurrency(currency: CurrencyListItem): void {
    if (currency.isDefault) {
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