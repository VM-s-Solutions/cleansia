import { Injectable } from '@angular/core';
import { CountryPhoneCode } from './country-phone-code.model';

@Injectable({
  providedIn: 'root',
})
export class CountryPhoneCodeService {
  private countries: CountryPhoneCode[];

  constructor() {
    this.countries = [
      { name: 'Afghanistan', code: '+93', flag: 'af' },
      { name: 'Albania', code: '+355', flag: 'al' },
      { name: 'Algeria', code: '+213', flag: 'dz' },
      { name: 'Andorra', code: '+376', flag: 'ad' },
      { name: 'Angola', code: '+244', flag: 'ao' },
      { name: 'Argentina', code: '+54', flag: 'ar' },
      { name: 'Armenia', code: '+374', flag: 'am' },
      { name: 'Australia', code: '+61', flag: 'au' },
      { name: 'Austria', code: '+43', flag: 'at' },
      { name: 'Azerbaijan', code: '+994', flag: 'az' },
      { name: 'Bahrain', code: '+973', flag: 'bh' },
      { name: 'Bangladesh', code: '+880', flag: 'bd' },
      { name: 'Belarus', code: '+375', flag: 'by' },
      { name: 'Belgium', code: '+32', flag: 'be' },
      { name: 'Bolivia', code: '+591', flag: 'bo' },
      { name: 'Bosnia and Herzegovina', code: '+387', flag: 'ba' },
      { name: 'Brazil', code: '+55', flag: 'br' },
      { name: 'Bulgaria', code: '+359', flag: 'bg' },
      { name: 'Cambodia', code: '+855', flag: 'kh' },
      { name: 'Canada', code: '+1', flag: 'ca' },
      { name: 'Chile', code: '+56', flag: 'cl' },
      { name: 'China', code: '+86', flag: 'cn' },
      { name: 'Colombia', code: '+57', flag: 'co' },
      { name: 'Costa Rica', code: '+506', flag: 'cr' },
      { name: 'Croatia', code: '+385', flag: 'hr' },
      { name: 'Cuba', code: '+53', flag: 'cu' },
      { name: 'Cyprus', code: '+357', flag: 'cy' },
      { name: 'Czech Republic', code: '+420', flag: 'cz' },
      { name: 'Denmark', code: '+45', flag: 'dk' },
      { name: 'Dominican Republic', code: '+1-809', flag: 'do' },
      { name: 'Ecuador', code: '+593', flag: 'ec' },
      { name: 'Egypt', code: '+20', flag: 'eg' },
      { name: 'El Salvador', code: '+503', flag: 'sv' },
      { name: 'Estonia', code: '+372', flag: 'ee' },
      { name: 'Ethiopia', code: '+251', flag: 'et' },
      { name: 'Finland', code: '+358', flag: 'fi' },
      { name: 'France', code: '+33', flag: 'fr' },
      { name: 'Georgia', code: '+995', flag: 'ge' },
      { name: 'Germany', code: '+49', flag: 'de' },
      { name: 'Ghana', code: '+233', flag: 'gh' },
      { name: 'Greece', code: '+30', flag: 'gr' },
      { name: 'Guatemala', code: '+502', flag: 'gt' },
      { name: 'Honduras', code: '+504', flag: 'hn' },
      { name: 'Hong Kong', code: '+852', flag: 'hk' },
      { name: 'Hungary', code: '+36', flag: 'hu' },
      { name: 'Iceland', code: '+354', flag: 'is' },
      { name: 'India', code: '+91', flag: 'in' },
      { name: 'Indonesia', code: '+62', flag: 'id' },
      { name: 'Iran', code: '+98', flag: 'ir' },
      { name: 'Iraq', code: '+964', flag: 'iq' },
      { name: 'Ireland', code: '+353', flag: 'ie' },
      { name: 'Israel', code: '+972', flag: 'il' },
      { name: 'Italy', code: '+39', flag: 'it' },
      { name: 'Jamaica', code: '+1-876', flag: 'jm' },
      { name: 'Japan', code: '+81', flag: 'jp' },
      { name: 'Jordan', code: '+962', flag: 'jo' },
      { name: 'Kazakhstan', code: '+7', flag: 'kz' },
      { name: 'Kenya', code: '+254', flag: 'ke' },
      { name: 'Kosovo', code: '+383', flag: 'xk' },
      { name: 'Kuwait', code: '+965', flag: 'kw' },
      { name: 'Latvia', code: '+371', flag: 'lv' },
      { name: 'Lebanon', code: '+961', flag: 'lb' },
      { name: 'Libya', code: '+218', flag: 'ly' },
      { name: 'Liechtenstein', code: '+423', flag: 'li' },
      { name: 'Lithuania', code: '+370', flag: 'lt' },
      { name: 'Luxembourg', code: '+352', flag: 'lu' },
      { name: 'Malaysia', code: '+60', flag: 'my' },
      { name: 'Malta', code: '+356', flag: 'mt' },
      { name: 'Mexico', code: '+52', flag: 'mx' },
      { name: 'Moldova', code: '+373', flag: 'md' },
      { name: 'Monaco', code: '+377', flag: 'mc' },
      { name: 'Mongolia', code: '+976', flag: 'mn' },
      { name: 'Montenegro', code: '+382', flag: 'me' },
      { name: 'Morocco', code: '+212', flag: 'ma' },
      { name: 'Netherlands', code: '+31', flag: 'nl' },
      { name: 'New Zealand', code: '+64', flag: 'nz' },
      { name: 'Nigeria', code: '+234', flag: 'ng' },
      { name: 'North Macedonia', code: '+389', flag: 'mk' },
      { name: 'Norway', code: '+47', flag: 'no' },
      { name: 'Oman', code: '+968', flag: 'om' },
      { name: 'Pakistan', code: '+92', flag: 'pk' },
      { name: 'Panama', code: '+507', flag: 'pa' },
      { name: 'Paraguay', code: '+595', flag: 'py' },
      { name: 'Peru', code: '+51', flag: 'pe' },
      { name: 'Philippines', code: '+63', flag: 'ph' },
      { name: 'Poland', code: '+48', flag: 'pl' },
      { name: 'Portugal', code: '+351', flag: 'pt' },
      { name: 'Qatar', code: '+974', flag: 'qa' },
      { name: 'Romania', code: '+40', flag: 'ro' },
      { name: 'Russia', code: '+7', flag: 'ru' },
      { name: 'Saudi Arabia', code: '+966', flag: 'sa' },
      { name: 'Serbia', code: '+381', flag: 'rs' },
      { name: 'Singapore', code: '+65', flag: 'sg' },
      { name: 'Slovakia', code: '+421', flag: 'sk' },
      { name: 'Slovenia', code: '+386', flag: 'si' },
      { name: 'South Africa', code: '+27', flag: 'za' },
      { name: 'South Korea', code: '+82', flag: 'kr' },
      { name: 'Spain', code: '+34', flag: 'es' },
      { name: 'Sri Lanka', code: '+94', flag: 'lk' },
      { name: 'Sweden', code: '+46', flag: 'se' },
      { name: 'Switzerland', code: '+41', flag: 'ch' },
      { name: 'Taiwan', code: '+886', flag: 'tw' },
      { name: 'Thailand', code: '+66', flag: 'th' },
      { name: 'Tunisia', code: '+216', flag: 'tn' },
      { name: 'Turkey', code: '+90', flag: 'tr' },
      { name: 'Ukraine', code: '+380', flag: 'ua' },
      { name: 'United Arab Emirates', code: '+971', flag: 'ae' },
      { name: 'United Kingdom', code: '+44', flag: 'gb' },
      { name: 'United States', code: '+1', flag: 'us' },
      { name: 'Uruguay', code: '+598', flag: 'uy' },
      { name: 'Uzbekistan', code: '+998', flag: 'uz' },
      { name: 'Venezuela', code: '+58', flag: 've' },
      { name: 'Vietnam', code: '+84', flag: 'vn' },
    ];
  }

  getCountries(): CountryPhoneCode[] {
    return this.countries;
  }

  getDefaultCountry(): CountryPhoneCode {
    return this.countries.find((c) => c.flag === 'cz')!;
  }

  findByPhoneValue(phoneValue: string): CountryPhoneCode | undefined {
    if (!phoneValue || !phoneValue.startsWith('+')) {
      return undefined;
    }
    // Sort by code length descending to match longer codes first (e.g., +421 before +42)
    const sorted = [...this.countries].sort(
      (a, b) => b.code.length - a.code.length
    );
    return sorted.find((c) => phoneValue.startsWith(c.code));
  }
}
