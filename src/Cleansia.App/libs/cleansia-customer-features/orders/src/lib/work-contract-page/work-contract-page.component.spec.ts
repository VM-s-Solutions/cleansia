import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { CustomerClient, WorkContractDto } from '@cleansia/customer-services';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { Observable, Subject, of, throwError } from 'rxjs';
import { WorkContractPageComponent } from './work-contract-page.component';

const ORDER_ID = 'ord-1';
const ACCEPTANCE_ID = 'acc-1';

const SERVED = WorkContractDto.fromJS({
  legalDocumentTextId: 'text-en',
  legalDocumentId: 'doc-1',
  version: '2026-09-20',
  effectiveFrom: '2026-09-20',
  language: 'en',
  title: 'Contract for Work',
  contentHtml: '<p>This contract for work is concluded between the client and the contractor.</p>',
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
    services: [{ id: 's1', name: 'Standard cleaning' }],
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
});

const DICTIONARY = {
  legal: {
    version: 'Version {{version}}',
    effective_from: 'Effective from {{date}}',
  },
  global: { actions: { retry: 'Retry' } },
  pages: {
    order_contract: {
      eyebrow: 'Contract for work',
      title: 'Contract for work',
      back: 'Back to the order',
      facts: {
        title: 'The job as accepted',
        order_number: 'Order number',
        window: 'Date and time',
        price: 'Price',
        location: 'Location',
        rooms_bathrooms: 'Rooms / bathrooms',
        services: 'Services',
        packages: 'Packages',
        extras: 'Extras',
      },
      accepted_on: 'Accepted by the cleaner on {{date}}, version {{version}}',
      accepted_in_language: 'Accepted in {{language}}',
      load_error: "We couldn't load this contract.",
    },
  },
};

describe('WorkContractPageComponent', () => {
  let fixture: ComponentFixture<WorkContractPageComponent>;
  let getWorkContract: jest.Mock;

  const host = (): HTMLElement => fixture.nativeElement as HTMLElement;
  const text = (selector: string): string | undefined =>
    host().querySelector(selector)?.textContent?.replace(/\s+/g, ' ').trim();

  async function render(answer: Observable<WorkContractDto>): Promise<void> {
    getWorkContract = jest.fn().mockReturnValue(answer);
    await TestBed.configureTestingModule({
      imports: [WorkContractPageComponent, TranslateModule.forRoot()],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: {
                get: (key: string) => ({ orderId: ORDER_ID, acceptanceId: ACCEPTANCE_ID })[key] ?? null,
              },
            },
          },
        },
        { provide: CustomerClient, useValue: { orderClient: { getWorkContract } } },
      ],
    }).compileComponents();
    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('en', DICTIONARY);
    translate.use('en');

    fixture = TestBed.createComponent(WorkContractPageComponent);
    fixture.detectChanges();
  }

  afterEach(() => TestBed.resetTestingModule());

  describe('the accepted contract', () => {
    beforeEach(() => render(of(SERVED)));

    it('reads the acceptance named in the route, in the UI language', () => {
      expect(getWorkContract).toHaveBeenCalledWith(ACCEPTANCE_ID, 'en');
    });

    it('claims the one h1 for the served title', () => {
      const headings = host().querySelectorAll('h1');

      expect(headings.length).toBe(1);
      expect(headings[0].textContent?.trim()).toBe('Contract for Work');
    });

    it('states the version and the effective date of the accepted document', () => {
      expect(text('.cl-lgl__version')).toBe('Version 2026-09-20');
      expect(text('.cl-lgl__updated')).toBe('Effective from September 20, 2026');
    });

    it('lists the frozen facts, not the live order', () => {
      const rows = Array.from(host().querySelectorAll('.cl-lgl__fact')).map((row) => ({
        label: row.querySelector('dt')?.textContent?.trim(),
        value: row.querySelector('dd')?.textContent?.trim(),
      }));

      expect(rows).toEqual([
        { label: 'Order number', value: 'ORD-42' },
        { label: 'Date and time', value: expect.stringMatching(/2026/) },
        { label: 'Price', value: 'CZK 1,200' },
        { label: 'Location', value: 'Praha · 120' },
        { label: 'Rooms / bathrooms', value: '2 / 1' },
        { label: 'Services', value: 'Standard cleaning' },
      ]);
    });

    it('states when the cleaner accepted, which version, and the language it was accepted in', () => {
      const acceptance = text('.cl-lgl__acceptance');

      expect(acceptance).toContain('Accepted by the cleaner on');
      expect(acceptance).toContain('version 2026-09-20');
      expect(acceptance).toContain('Accepted in Czech');
    });

    it('renders the server-rendered HTML as the content', () => {
      expect(host().querySelector('.cl-lgl__content p')?.textContent).toBe(
        'This contract for work is concluded between the client and the contractor.',
      );
    });

    it('links back to the order it belongs to', () => {
      expect(host().querySelector<HTMLAnchorElement>('.cl-lgl__back')?.getAttribute('href')).toBe(
        `/orders/${ORDER_ID}`,
      );
    });

    it('shows neither the skeleton nor the error', () => {
      expect(host().querySelector('p-skeleton')).toBeNull();
      expect(host().querySelector('.cl-lgl__error')).toBeNull();
    });
  });

  describe('while the server has not answered', () => {
    beforeEach(() => render(new Subject<WorkContractDto>()));

    it('shows the skeleton and nothing of the contract', () => {
      expect(host().querySelector('p-skeleton')).not.toBeNull();
      expect(host().querySelector('.cl-lgl__content')).toBeNull();
      expect(host().querySelector('.cl-lgl__fact')).toBeNull();
      expect(host().querySelector('.cl-lgl__error')).toBeNull();
    });

    it('names the page from its own key until the served title arrives', () => {
      expect(text('h1')).toBe('Contract for work');
      expect(host().querySelector('.cl-lgl__version')).toBeNull();
    });
  });

  // Another customer's acceptance id is refused as order.not_found; nothing of the order shows.
  describe('when the read is refused', () => {
    beforeEach(() => render(throwError(() => new Error('order.not_found'))));

    it('shows the error and nothing of the contract', () => {
      expect(text('.cl-lgl__error')).toContain("We couldn't load this contract.");
      expect(host().querySelector('.cl-lgl__content')).toBeNull();
      expect(host().querySelector('.cl-lgl__fact')).toBeNull();
      expect(host().querySelector('.cl-lgl__acceptance')).toBeNull();
      expect(host().querySelector('p-skeleton')).toBeNull();
    });

    it('reads the contract again when the retry button is pressed', () => {
      getWorkContract.mockReturnValue(of(SERVED));

      host().querySelector<HTMLButtonElement>('.cl-lgl__error button')?.click();
      fixture.detectChanges();

      expect(getWorkContract).toHaveBeenCalledTimes(2);
      expect(host().querySelector('.cl-lgl__error')).toBeNull();
      expect(text('.cl-lgl__version')).toBe('Version 2026-09-20');
    });
  });
});
