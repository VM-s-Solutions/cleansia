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
    CleansiaPermissionDirective,
  ],
  templateUrl: './country-management.component.html',
  providers: [CountryManagementFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CountryManagementComponent implements AfterViewInit, OnDestroy {
  private readonly cd = inject(ChangeDetectorRef);
  protected readonly facade = inject(CountryManagementFacade);
  protected readonly Policy = Policy;
  private readonly translate = inject(TranslateService);
  private readonly permissions = inject(PermissionService);

  flagTemplate = viewChild<TemplateRef<CountryListItem>>('flagTemplate');

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

  confirmSetDefaultMarket(country: CountryListItem): void {
    this.facade.setDefaultMarket(country);
  }

  confirmDeleteCountry(country: CountryListItem): void {
    this.facade.deleteCountry(country);
  }
}