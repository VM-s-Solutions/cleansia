import { TestBed } from '@angular/core/testing';
import { CustomerClient, WorkContractDto } from '@cleansia/customer-services';
import { TranslateService } from '@ngx-translate/core';
import { Subject, of, throwError } from 'rxjs';
import { WorkContractPageFacade } from './work-contract-page.facade';

const ACCEPTANCE_ID = 'acc-1';

function contract(overrides: Record<string, unknown> = {}): WorkContractDto {
  return WorkContractDto.fromJS({
    legalDocumentTextId: 'text-cs',
    legalDocumentId: 'doc-1',
    version: '2026-09-20',
    effectiveFrom: '2026-09-20',
    language: 'cs',
    title: 'Smlouva o dílo',
    contentHtml: '<p>Úvod.</p>',
    facts: {
      orderNumber: 'ORD-42',
      cleaningDateTimeUtc: '2026-09-25T08:00:00Z',
      estimatedMinutes: 120,
      totalPrice: 1200,
      currencyCode: 'CZK',
      locationApproximate: 'Praha · 120',
      countryId: 'cze-id',
      rooms: 2,
      bathrooms: 1,
      services: [{ id: 's1', name: 'Standard' }],
      packages: [],
      extraSlugs: [],
    },
    acceptance: {
      acceptedOn: '2026-09-21T10:15:00Z',
      documentVersion: '2026-09-20',
      acceptedLanguage: 'cs',
      orderEmployeeId: 'seat-1',
      employeeId: 'emp-1',
    },
    ...overrides,
  });
}

describe('WorkContractPageFacade', () => {
  let facade: WorkContractPageFacade;
  let getWorkContract: jest.Mock;
  let langChange$: Subject<{ lang: string }>;

  function build(): void {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        WorkContractPageFacade,
        { provide: CustomerClient, useValue: { orderClient: { getWorkContract } } },
        { provide: TranslateService, useValue: { currentLang: 'cs', onLangChange: langChange$ } },
      ],
    });
    facade = TestBed.inject(WorkContractPageFacade);
  }

  beforeEach(() => {
    langChange$ = new Subject();
    getWorkContract = jest.fn().mockReturnValue(of(contract()));
    build();
  });

  afterEach(() => TestBed.resetTestingModule());

  describe('which contract is read', () => {
    it('asks for the accepted contract by its acceptance id in the current language', () => {
      facade.load(ACCEPTANCE_ID);

      expect(getWorkContract).toHaveBeenCalledWith(ACCEPTANCE_ID, 'cs');
    });

    it('re-reads the text in the new language when the customer switches language', () => {
      facade.load(ACCEPTANCE_ID);
      langChange$.next({ lang: 'en' });

      expect(getWorkContract).toHaveBeenLastCalledWith(ACCEPTANCE_ID, 'en');
      expect(facade.language()).toBe('en');
    });

    it('reads once per language, not once per emission', () => {
      facade.load(ACCEPTANCE_ID);
      langChange$.next({ lang: 'cs' });

      expect(getWorkContract).toHaveBeenCalledTimes(1);
    });
  });

  describe('the three states', () => {
    it('is loading until the server answers', () => {
      getWorkContract.mockReturnValue(new Subject());
      build();

      facade.load(ACCEPTANCE_ID);

      expect(facade.loading()).toBe(true);
      expect(facade.hasError()).toBe(false);
      expect(facade.contract()).toBeNull();
    });

    it('holds the accepted contract once it arrives', () => {
      const served = contract();
      getWorkContract.mockReturnValue(of(served));
      build();

      facade.load(ACCEPTANCE_ID);

      expect(facade.loading()).toBe(false);
      expect(facade.hasError()).toBe(false);
      expect(facade.contract()).toBe(served);
    });

    // Another customer's acceptance id answers order.not_found; the page shows nothing of the order.
    it('reports the error state, with no contract, when the read is refused', () => {
      getWorkContract.mockReturnValue(throwError(() => new Error('order.not_found')));
      build();

      facade.load(ACCEPTANCE_ID);

      expect(facade.loading()).toBe(false);
      expect(facade.hasError()).toBe(true);
      expect(facade.contract()).toBeNull();
      expect(facade.factRows()).toEqual([]);
    });

    it('clears a previous failure when a retry succeeds', () => {
      getWorkContract
        .mockReturnValueOnce(throwError(() => new Error('offline')))
        .mockReturnValue(of(contract()));
      build();
      facade.load(ACCEPTANCE_ID);
      expect(facade.hasError()).toBe(true);

      facade.retry();

      expect(getWorkContract).toHaveBeenCalledTimes(2);
      expect(facade.hasError()).toBe(false);
      expect(facade.contract()).not.toBeNull();
    });

    it('drops the stale contract while a language switch is answered', () => {
      facade.load(ACCEPTANCE_ID);
      getWorkContract.mockReturnValue(new Subject());

      langChange$.next({ lang: 'en' });

      expect(facade.loading()).toBe(true);
      expect(facade.contract()).toBeNull();
    });
  });

  describe('what the page derives', () => {
    it('lists the frozen facts formatted in the UI language', () => {
      facade.load(ACCEPTANCE_ID);

      expect(facade.factRows().map((row) => row.labelKey)).toEqual([
        'pages.order_contract.facts.order_number',
        'pages.order_contract.facts.window',
        'pages.order_contract.facts.price',
        'pages.order_contract.facts.location',
        'pages.order_contract.facts.rooms_bathrooms',
        'pages.order_contract.facts.services',
      ]);
      expect(facade.factRows()[2].value).toBe('1 200 Kč');
    });

    it('states the acceptance instant in the UI language', () => {
      facade.load(ACCEPTANCE_ID);

      expect(facade.acceptedOn()).toMatch(/^21\. 09\. 2026 \d{1,2}:\d{2}$/);
    });

    it('states no acceptance instant without a contract', () => {
      getWorkContract.mockReturnValue(throwError(() => new Error('offline')));
      build();

      facade.load(ACCEPTANCE_ID);

      expect(facade.acceptedOn()).toBe('');
    });

    it('names no accepted language while the rendered text is the accepted one', () => {
      facade.load(ACCEPTANCE_ID);

      expect(facade.acceptedLanguageName()).toBeNull();
    });

    it('names the accepted language when the page renders another', () => {
      getWorkContract.mockReturnValue(
        of(contract({ language: 'en', acceptance: { acceptedOn: '2026-09-21T10:15:00Z', documentVersion: '2026-09-20', acceptedLanguage: 'cs' } })),
      );
      langChange$ = new Subject();
      build();

      facade.load(ACCEPTANCE_ID);

      expect(facade.acceptedLanguageName()).toBe('Čeština');
    });
  });
});
