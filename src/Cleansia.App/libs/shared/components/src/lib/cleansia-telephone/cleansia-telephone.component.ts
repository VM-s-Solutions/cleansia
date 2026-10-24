import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  HostListener,
  OnInit,
  forwardRef,
  inject,
  input,
  output,
  viewChild,
} from '@angular/core';
import { FormsModule, NG_VALUE_ACCESSOR } from '@angular/forms';
import { ErrorPipe } from '@cleansia/pipes';
import {
  CountryPhoneCode,
  CountryPhoneCodeService,
} from '@cleansia/services';
import { FloatLabelModule } from 'primeng/floatlabel';
import { CleansiaBaseFormInputComponent } from '../cleansia-base-form';

// Whitespace, and the zero-width and direction marks a contacts app puts around a copied number.
const LEADING_BLANKS = /^[\s\u200B-\u200F\u202A-\u202E\u2060-\u2069]+/;

@Component({
  selector: 'cleansia-telephone',
  templateUrl: './cleansia-telephone.component.html',
  styleUrl: './cleansia-telephone.component.scss',
  standalone: true,
  imports: [CommonModule, ErrorPipe, FormsModule, FloatLabelModule],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => CleansiaTelephoneComponent),
      multi: true,
    },
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaTelephoneComponent
  extends CleansiaBaseFormInputComponent
  implements OnInit
{
  private phoneService = inject(CountryPhoneCodeService);
  private elementRef = inject(ElementRef);

  /**
   * `null` renders the label ABOVE the control instead of floating it into the
   * border — the shape the approved customer design draws. The default stays
   * 'on' so the partner and admin forms are untouched.
   */
  floatVariant = input<'on' | 'in' | 'over' | null>('on');
  id = input<string>(
    'cleansia-tel-' + Math.random().toString(36).substring(2)
  );
  defaultCountryFlag = input<string>('cz');

  valueChanges = output<string>();

  searchInput = viewChild<ElementRef>('searchInput');

  countries: CountryPhoneCode[] = [];
  filteredCountries: CountryPhoneCode[] = [];
  selectedCountry!: CountryPhoneCode;
  phoneNumber = '';
  isDropdownOpen = false;
  searchTerm = '';

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: Event): void {
    if (!this.elementRef.nativeElement.contains(event.target)) {
      this.closeDropdown();
    }
  }

  override ngOnInit(): void {
    super.ngOnInit();
    this.countries = this.phoneService.getCountries();
    this.filteredCountries = [...this.countries];
    this.selectedCountry =
      this.countries.find((c) => c.flag === this.defaultCountryFlag()) ??
      this.phoneService.getDefaultCountry();
  }

  override writeValue(value: string): void {
    if (!value) {
      this.phoneNumber = '';
      return;
    }

    const matched = this.phoneService.findByPhoneValue(value);
    if (matched) {
      this.selectedCountry = matched;
      this.phoneNumber = value.slice(matched.code.length).replace(/\s/g, '');
    } else {
      this.phoneNumber = value.replace(/[^\d]/g, '');
    }
  }

  selectCountry(country: CountryPhoneCode): void {
    this.selectedCountry = country;
    this.closeDropdown();
    this.emitValue();
  }

  // A pasted or autofilled number often carries its own country code: read it as the country, not as
  // the first digits of the national number.
  onPhoneInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    const international = this.readCountryCode(input.value);
    if (international) {
      this.selectedCountry = international.country;
    }
    const digits = (international?.national ?? input.value).replace(/[^\d]/g, '');
    this.phoneNumber = digits;
    input.value = digits;
    this.emitValue();
  }

  toggleDropdown(event: Event): void {
    event.stopPropagation();
    if (this.disabled()) return;
    this.isDropdownOpen = !this.isDropdownOpen;
    if (this.isDropdownOpen) {
      this.searchTerm = '';
      this.filteredCountries = [...this.countries];
      setTimeout(() => this.searchInput()?.nativeElement.focus());
    }
  }

  closeDropdown(): void {
    this.isDropdownOpen = false;
    this.searchTerm = '';
  }

  filterCountries(event: Event): void {
    this.searchTerm = (event.target as HTMLInputElement).value.toLowerCase();
    if (!this.searchTerm) {
      this.filteredCountries = [...this.countries];
      return;
    }
    this.filteredCountries = this.countries.filter(
      (c) =>
        c.name.toLowerCase().includes(this.searchTerm) ||
        c.code.includes(this.searchTerm)
    );
  }

  private readCountryCode(raw: string): { country: CountryPhoneCode; national: string } | null {
    const value = raw.replace(LEADING_BLANKS, '');
    const matched = this.phoneService.findByPhoneValue(value);
    if (!matched) return null;
    const country = matched.code === this.selectedCountry.code ? this.selectedCountry : matched;
    return { country, national: value.slice(matched.code.length) };
  }

  private emitValue(): void {
    const fullValue = this.phoneNumber
      ? `${this.selectedCountry.code} ${this.phoneNumber}`
      : '';
    this.onChange(fullValue);
    this.valueChanges.emit(fullValue);
  }
}
