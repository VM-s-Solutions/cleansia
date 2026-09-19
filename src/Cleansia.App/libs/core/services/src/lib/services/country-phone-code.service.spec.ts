import { CountryPhoneCodeService } from './country-phone-code.service';

describe('CountryPhoneCodeService', () => {
  const service = new CountryPhoneCodeService();

  it('defaults to the Czech Republic', () => {
    expect(service.getDefaultCountry()).toEqual({ name: 'Czech Republic', code: '+420', flag: 'cz' });
  });

  it('matches the longest dialling code first, so +421 is Slovakia rather than a +42 prefix', () => {
    expect(service.findByPhoneValue('+421900123456')?.flag).toBe('sk');
    expect(service.findByPhoneValue('900123456')).toBeUndefined();
  });
});
