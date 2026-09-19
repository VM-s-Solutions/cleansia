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
import { CountryListItem } from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTableComponent,
  CleansiaTitleComponent,
  TableColumn,
  TableAction,
} from '@cleansia/components';
import { PermissionService, Policy } from '@cleansia/services';
import { CleansiaPermissionDirective } from '@cleansia/directives';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ConfirmationService } from 'primeng/api';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { TagModule } from 'primeng/tag';
import { Subject, takeUntil } from 'rxjs';
import { CountryManagementFacade } from './country-management.facade';
import {
  getCountryFlagCode,
  getCountryTableDefinition,
} from './country-management.models';

@Component({
  selector: 'cleansia-admin-country-management',
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
    TagModule,
    CleansiaPermissionDirective,
  ],
  templateUrl: './country-management.component.html',
  providers: [CountryManagementFacade, ConfirmationService],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CountryManagementComponent implements AfterViewInit, OnDestroy {
  private readonly cd = inject(ChangeDetectorRef);
  protected readonly facade = inject(CountryManagementFacade);
  protected readonly Policy = Policy;
  private readonly translate = inject(TranslateService);
  private readonly permissions = inject(PermissionService);
  private readonly confirmationService = inject(ConfirmationService);

  flagTemplate = viewChild<TemplateRef<CountryListItem>>('flagTemplate');
  defaultMarketTemplate = viewChild<TemplateRef<CountryListItem>>(
    'defaultMarketTemplate'
  );

  countryColumns!: TableColumn<CountryListItem>[];
  countryActions!: TableAction<CountryListItem>[];

  // Expose helper function to template
  getCountryFlagCode = getCountryFlagCode;

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

    this.facade.loadCountries();
  }

  private rebuildTableDefinitions(): void {
    const tableDef = getCountryTableDefinition(
      {
        onEdit: this.editCountry.bind(this),
        onDelete: this.confirmDeleteCountry.bind(this),
        onSetDefaultMarket: this.confirmSetDefaultMarket.bind(this),
      },
      this.translate,
      this.permissions,
      this.flagTemplate(),
      this.defaultMarketTemplate()
    );
    this.countryColumns = tableDef.columns;
    this.countryActions = tableDef.actions;
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  createCountry(): void {
    this.facade.navigateToCreateCountry();
  }

  editCountry(country: CountryListItem): void {
    this.facade.navigateToEditCountry(country);
  }

  confirmSetDefaultMarket(country: CountryListItem): void {
    this.confirmationService.confirm({
      message: this.translate.instant(
        'pages.country_management.set_default_market_confirm',
        { name: country.name }
      ),
      header: this.translate.instant('pages.country_management.set_default_market'),
      icon: 'pi pi-star',
      accept: () => {
        this.facade.setDefaultMarket(country);
      },
    });
  }

  confirmDeleteCountry(country: CountryListItem): void {
    this.confirmationService.confirm({
      message: this.translate.instant('pages.country_management.delete_confirm'),
      header: this.translate.instant('pages.country_management.delete_country'),
      icon: 'pi pi-exclamation-triangle',
      accept: () => {
        this.facade.deleteCountry(country);
      },
    });
  }
}