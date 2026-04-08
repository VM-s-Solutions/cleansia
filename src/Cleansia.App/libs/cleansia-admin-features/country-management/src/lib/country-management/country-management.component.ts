import { CommonModule } from '@angular/common';
import {
  AfterViewInit,
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
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ConfirmationService } from 'primeng/api';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
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
  ],
  templateUrl: './country-management.component.html',
  providers: [CountryManagementFacade, ConfirmationService],
})
export class CountryManagementComponent implements AfterViewInit, OnDestroy {
  private readonly cd = inject(ChangeDetectorRef);
  protected readonly facade = inject(CountryManagementFacade);
  private readonly translate = inject(TranslateService);
  private readonly confirmationService = inject(ConfirmationService);

  flagTemplate = viewChild<TemplateRef<any>>('flagTemplate');

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
      },
      this.translate,
      this.flagTemplate()
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