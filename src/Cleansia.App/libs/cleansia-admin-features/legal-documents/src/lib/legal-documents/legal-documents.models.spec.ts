import {
  LegalDocumentAudience,
  LegalDocumentTextSummaryDto,
  LegalDocumentType,
  LegalDocumentVersionDto,
} from '@cleansia/admin-services';
import { TranslateService } from '@ngx-translate/core';
import {
  formatEffectiveDate,
  getAudienceLabelKey,
  getLegalTextTableDefinition,
  getLegalVersionTableDefinition,
  getTypeLabelKey,
  pickPreviewLanguage,
} from './legal-documents.models';

const translate = { instant: (key: string) => key } as unknown as TranslateService;

const texts = (...languages: string[]): LegalDocumentTextSummaryDto[] =>
  languages.map((language) =>
    LegalDocumentTextSummaryDto.fromJS({ language, title: language.toUpperCase(), contentHash: `hash-${language}` })
  );

describe('legal-documents models', () => {
  describe('getAudienceLabelKey / getTypeLabelKey', () => {
    it('maps each wire enum to its own translation key', () => {
      expect(getAudienceLabelKey(LegalDocumentAudience.Customer)).toBe(
        'pages.legal_documents.audience.customer'
      );
      expect(getAudienceLabelKey(LegalDocumentAudience.Employee)).toBe(
        'pages.legal_documents.audience.employee'
      );
      expect(getTypeLabelKey(LegalDocumentType.TermsOfService)).toBe(
        'pages.legal_documents.type.terms_of_service'
      );
      expect(getTypeLabelKey(LegalDocumentType.PrivacyPolicy)).toBe(
        'pages.legal_documents.type.privacy_policy'
      );
    });
  });

  describe('formatEffectiveDate', () => {
    it('renders a date-only wire value without shifting the day for the local zone', () => {
      expect(formatEffectiveDate(new Date('2026-09-14'))).toBe('14/09/2026');
    });

    it('is blank for a missing date', () => {
      expect(formatEffectiveDate(undefined)).toBe('');
    });
  });

  describe('pickPreviewLanguage', () => {
    it('prefers the requested language when the version carries it', () => {
      expect(pickPreviewLanguage(texts('en', 'cs', 'sk'), 'cs')).toBe('cs');
    });

    it('falls back to English when the requested language is absent', () => {
      expect(pickPreviewLanguage(texts('en', 'cs'), 'uk')).toBe('en');
    });

    it('falls back to the first text when there is no English either', () => {
      expect(pickPreviewLanguage(texts('cs', 'sk'), 'uk')).toBe('cs');
    });

    it('is null for a version with no texts', () => {
      expect(pickPreviewLanguage([], 'en')).toBeNull();
      expect(pickPreviewLanguage(undefined, 'en')).toBeNull();
    });
  });

  describe('getLegalVersionTableDefinition', () => {
    const version = LegalDocumentVersionDto.fromJS({
      id: 'doc-1',
      audience: LegalDocumentAudience.Customer,
      type: LegalDocumentType.PrivacyPolicy,
      countryId: undefined,
      countryIsoCode: undefined,
      effectiveFrom: '2026-09-14',
      version: '2026-09-14',
      isInForce: true,
      texts: texts('en', 'cs'),
    });

    it('renders audience, type, market fallback, date, in-force and languages through their columns', () => {
      const { columns } = getLegalVersionTableDefinition({ onPreview: jest.fn() }, translate);
      const valueOf = (id: string) => columns.find((c) => c.id === id)?.getValue?.(version);

      expect(valueOf('audience')).toBe('pages.legal_documents.audience.customer');
      expect(valueOf('type')).toBe('pages.legal_documents.type.privacy_policy');
      expect(valueOf('market')).toBe('pages.legal_documents.all_markets');
      expect(valueOf('effectiveFrom')).toBe('14/09/2026');
      expect(valueOf('isInForce')).toBe('global.yes');
      expect(valueOf('languages')).toBe('en, cs');
    });

    it('shows the market ISO code when the version is market-specific', () => {
      const { columns } = getLegalVersionTableDefinition({ onPreview: jest.fn() }, translate);
      const market = columns.find((c) => c.id === 'market');

      expect(
        market?.getValue?.(LegalDocumentVersionDto.fromJS({ ...version, countryId: 'cz', countryIsoCode: 'CZ' }))
      ).toBe('CZ');
    });

    it('has a single preview action that hands the row back', () => {
      const onPreview = jest.fn();
      const { actions } = getLegalVersionTableDefinition({ onPreview }, translate);

      expect(actions).toHaveLength(1);
      actions[0].onClick(version);
      expect(onPreview).toHaveBeenCalledWith(version);
    });
  });

  describe('getLegalTextTableDefinition', () => {
    it('lists language, title and hash, and hides the show action on the language already shown', () => {
      const onShow = jest.fn();
      const [en, cs] = texts('en', 'cs');
      const { columns, actions } = getLegalTextTableDefinition(
        { onShow, isShown: (row) => row.language === 'en' },
        translate
      );

      expect(columns.map((c) => c.id)).toEqual(['language', 'title', 'contentHash']);
      expect(actions).toHaveLength(1);
      expect(actions[0].visible?.(en)).toBe(false);
      expect(actions[0].visible?.(cs)).toBe(true);
      actions[0].onClick(cs);
      expect(onShow).toHaveBeenCalledWith(cs);
    });
  });
});
