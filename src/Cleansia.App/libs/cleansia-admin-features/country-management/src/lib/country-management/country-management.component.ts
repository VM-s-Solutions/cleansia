import { CommonModule } from '@angular/common';
import { AfterViewInit, Component, inject, OnDestroy } from '@angular/core';
import { CountryListItem } from '@cleansia/admin-services';
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
import { CountryManagementFacade } from './country-management.facade';
import { getCountryTableDefinition } from './country-management.models';

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
    CleansiaLanguageSwitcherComponent,
    ConfirmDialogModule,
  ],
  templateUrl: './country-management.component.html',
  providers: [CountryManagementFacade, ConfirmationService],
})
export class CountryManagementComponent implements AfterViewInit, OnDestroy {
  protected readonly facade = inject(CountryManagementFacade);
  private readonly translate = inject(TranslateService);
  private readonly confirmationService = inject(ConfirmationService);

  countryTableDefinition!: TableDefinition<CountryListItem>;

  private destroy$ = new Subject<void>();

  ngAfterViewInit(): void {
    this.countryTableDefinition = getCountryTableDefinition(
      {
        onEdit: this.editCountry.bind(this),
        onDelete: this.confirmDeleteCountry.bind(this),
      },
      this.translate
    );

    this.facade.loadCountries();
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