import { FormControl } from '@angular/forms';
import { TestBed } from '@angular/core/testing';
import {
  AcceptWorkContractCommand,
  AcceptWorkContractResponse,
  PartnerClient,
  TakeOrderCommand,
  TakeOrderResponse,
  WorkContractDto,
} from '@cleansia/partner-services';
import { TranslateService } from '@ngx-translate/core';
import { DynamicDialogRef } from 'primeng/dynamicdialog';
import { of, Subject, throwError } from 'rxjs';
import { WorkContractDialogFacade } from './work-contract-dialog.facade';
import {
  WorkContractDialogMode,
  WorkContractDialogOutcome,
} from './work-contract-dialog.models';

const ORDER_ID = 'ord-1';
const ACCEPTANCE_ID = 'acc-1';
const TEXT_ID = 'text-en-1';
const NEWER_TEXT_ID = 'text-en-2';

function contract(overrides: Record<string, unknown> = {}): WorkContractDto {
  return WorkContractDto.fromJS({
    legalDocumentTextId: TEXT_ID,
    legalDocumentId: 'doc-1',
    version: '2026-09-20',
    effectiveFrom: '2026-09-20',
    language: 'en',
    title: 'Contract for work',
    contentHtml: '<h2>1. Parties</h2><p>The customer and the cleaner.</p>',
    facts: {
      orderNumber: 'CLS-42',
      cleaningDateTimeUtc: '2026-10-03T08:30:00Z',
      estimatedMinutes: 120,
      totalPrice: 1250,
      currencyCode: 'CZK',
      locationApproximate: 'Praha 6',
      countryId: 'CZ',
      rooms: 3,
      bathrooms: 1,
      services: [],
      packages: [],
      extraSlugs: [],
    },
    ...overrides,
  });
}

// The shape CleansiaApiController.HandleFailure puts on the wire for a validation refusal.
function refusal(code: string): unknown {
  return { detail: 'A validation problem occurred.', errors: { OrderId: code } };
}

describe('WorkContractDialogFacade', () => {
  let orderClient: {
    getWorkContractPreview: jest.Mock;
    getWorkContract: jest.Mock;
    takeOrder: jest.Mock;
    acceptWorkContract: jest.Mock;
  };
  let dialogRef: { close: jest.Mock };

  const createFacade = (): WorkContractDialogFacade => {
    TestBed.configureTestingModule({
      providers: [
        WorkContractDialogFacade,
        { provide: PartnerClient, useValue: { orderClient } },
        { provide: DynamicDialogRef, useValue: dialogRef },
        { provide: TranslateService, useValue: { currentLang: 'en', instant: (k: string) => k } },
      ],
    });
    return TestBed.inject(WorkContractDialogFacade);
  };

  beforeEach(() => {
    TestBed.resetTestingModule();
    orderClient = {
      getWorkContractPreview: jest.fn().mockReturnValue(of(contract())),
      getWorkContract: jest.fn(),
      takeOrder: jest.fn(),
      acceptWorkContract: jest.fn(),
    };
    dialogRef = { close: jest.fn() };
  });

  describe('loading', () => {
    it('reads the preview for the order in the UI language in take mode', () => {
      const facade = createFacade();

      facade.load({ mode: WorkContractDialogMode.Take, orderId: ORDER_ID });

      expect(orderClient.getWorkContractPreview).toHaveBeenCalledWith(ORDER_ID, 'en');
      expect(orderClient.getWorkContract).not.toHaveBeenCalled();
      expect(facade.contract()?.legalDocumentTextId).toBe(TEXT_ID);
      expect(facade.loading()).toBe(false);
      expect(facade.loadFailed()).toBe(false);
      expect(facade.factRows().map((row) => row.value)).toContain('CLS-42');
    });

    it('reads the preview in accept mode too', () => {
      const facade = createFacade();

      facade.load({ mode: WorkContractDialogMode.Accept, orderId: ORDER_ID });

      expect(orderClient.getWorkContractPreview).toHaveBeenCalledWith(ORDER_ID, 'en');
    });

    it('reads the accepted contract by its acceptance id in read mode', () => {
      orderClient.getWorkContract.mockReturnValue(
        of(
          contract({
            acceptance: {
              acceptedOn: '2026-09-21T10:00:00Z',
              documentVersion: '2026-09-20',
              acceptedLanguage: 'cs',
              orderEmployeeId: 'seat-1',
              employeeId: 'emp-1',
            },
          })
        )
      );
      const facade = createFacade();

      facade.load({ mode: WorkContractDialogMode.Read, acceptanceId: ACCEPTANCE_ID });

      expect(orderClient.getWorkContract).toHaveBeenCalledWith(ACCEPTANCE_ID, 'en');
      expect(orderClient.getWorkContractPreview).not.toHaveBeenCalled();
      expect(facade.contract()?.acceptance?.documentVersion).toBe('2026-09-20');
      // Accepted in Czech, rendered in English: the page has to say so.
      expect(facade.acceptedLanguageName()).toBe('Czech');
    });

    it('names no accepted language when it is the one rendered', () => {
      orderClient.getWorkContract.mockReturnValue(
        of(contract({ acceptance: { acceptedOn: '2026-09-21T10:00:00Z', acceptedLanguage: 'en' } }))
      );
      const facade = createFacade();

      facade.load({ mode: WorkContractDialogMode.Read, acceptanceId: ACCEPTANCE_ID });

      expect(facade.acceptedLanguageName()).toBeNull();
    });

    it('keeps loading true while the preview is in flight', () => {
      orderClient.getWorkContractPreview.mockReturnValue(new Subject<WorkContractDto>());
      const facade = createFacade();

      facade.load({ mode: WorkContractDialogMode.Take, orderId: ORDER_ID });

      expect(facade.loading()).toBe(true);
      expect(facade.contract()).toBeNull();
    });

    it('shows the "cannot be taken" notice with no text when the order has no document', () => {
      orderClient.getWorkContractPreview.mockReturnValue(
        throwError(() => refusal('legal.document_not_found'))
      );
      const facade = createFacade();

      facade.load({ mode: WorkContractDialogMode.Take, orderId: ORDER_ID });

      expect(facade.noticeKey()).toBe('api.legal.document_not_found');
      expect(facade.contract()).toBeNull();
      expect(facade.loadFailed()).toBe(false);
      expect(facade.canSubmit()).toBe(false);
    });

    it('enters the error state on any other failure and retries on demand', () => {
      orderClient.getWorkContractPreview.mockReturnValueOnce(
        throwError(() => new Error('network'))
      );
      const facade = createFacade();

      facade.load({ mode: WorkContractDialogMode.Take, orderId: ORDER_ID });

      expect(facade.loadFailed()).toBe(true);
      expect(facade.noticeKey()).toBeNull();

      orderClient.getWorkContractPreview.mockReturnValueOnce(of(contract()));
      facade.retry();

      expect(orderClient.getWorkContractPreview).toHaveBeenCalledTimes(2);
      expect(facade.loadFailed()).toBe(false);
      expect(facade.contract()?.legalDocumentTextId).toBe(TEXT_ID);
    });
  });

  describe('the tick', () => {
    it('follows the connected control', () => {
      const facade = createFacade();
      const control = new FormControl(false, { nonNullable: true });
      facade.load({ mode: WorkContractDialogMode.Take, orderId: ORDER_ID });

      facade.connectAcceptanceControl(control);
      expect(facade.canSubmit()).toBe(false);

      control.setValue(true);
      expect(facade.accepted()).toBe(true);
      expect(facade.canSubmit()).toBe(true);

      control.setValue(false);
      expect(facade.canSubmit()).toBe(false);
    });
  });

  describe('submit in take mode', () => {
    it('does not call the take without the tick', () => {
      const facade = createFacade();
      facade.load({ mode: WorkContractDialogMode.Take, orderId: ORDER_ID });

      facade.submit();

      expect(orderClient.takeOrder).not.toHaveBeenCalled();
      expect(dialogRef.close).not.toHaveBeenCalled();
    });

    it('takes the job once with the previewed text id and closes as accepted', () => {
      orderClient.takeOrder.mockReturnValue(
        of(TakeOrderResponse.fromJS({ orderId: ORDER_ID, employeeId: 'emp-1' }))
      );
      const facade = createFacade();
      facade.load({ mode: WorkContractDialogMode.Take, orderId: ORDER_ID });
      facade.accepted.set(true);

      facade.submit();

      expect(orderClient.takeOrder).toHaveBeenCalledTimes(1);
      const command: TakeOrderCommand = orderClient.takeOrder.mock.calls[0][0];
      expect(command).toBeInstanceOf(TakeOrderCommand);
      expect(command.toJSON()).toEqual({
        orderId: ORDER_ID,
        acceptedWorkContractTextId: TEXT_ID,
      });
      expect(orderClient.acceptWorkContract).not.toHaveBeenCalled();
      expect(dialogRef.close).toHaveBeenCalledWith({ outcome: WorkContractDialogOutcome.Accepted });
      expect(facade.submitting()).toBe(false);
    });

    it('ignores a second submit while the first is in flight', () => {
      orderClient.takeOrder.mockReturnValue(new Subject<TakeOrderResponse>());
      const facade = createFacade();
      facade.load({ mode: WorkContractDialogMode.Take, orderId: ORDER_ID });
      facade.accepted.set(true);

      facade.submit();
      facade.submit();

      expect(orderClient.takeOrder).toHaveBeenCalledTimes(1);
      expect(facade.submitting()).toBe(true);
    });

    it('re-runs the preview, unticks and shows the notice when the text was updated', () => {
      orderClient.takeOrder.mockReturnValue(throwError(() => refusal('contract.text_mismatch')));
      orderClient.getWorkContractPreview
        .mockReturnValueOnce(of(contract()))
        .mockReturnValueOnce(of(contract({ legalDocumentTextId: NEWER_TEXT_ID })));
      const facade = createFacade();
      const control = new FormControl(false, { nonNullable: true });
      facade.load({ mode: WorkContractDialogMode.Take, orderId: ORDER_ID });
      facade.connectAcceptanceControl(control);
      control.setValue(true);

      facade.submit();

      expect(orderClient.getWorkContractPreview).toHaveBeenCalledTimes(2);
      expect(facade.contract()?.legalDocumentTextId).toBe(NEWER_TEXT_ID);
      expect(control.value).toBe(false);
      expect(facade.accepted()).toBe(false);
      expect(facade.noticeKey()).toBe('api.contract.text_mismatch');
      expect(dialogRef.close).not.toHaveBeenCalled();
      expect(facade.canSubmit()).toBe(false);
    });

    it('closes as refused on any other refusal so the caller reconciles', () => {
      orderClient.takeOrder.mockReturnValue(throwError(() => refusal('order.no_available_spots')));
      const facade = createFacade();
      facade.load({ mode: WorkContractDialogMode.Take, orderId: ORDER_ID });
      facade.accepted.set(true);

      facade.submit();

      expect(orderClient.getWorkContractPreview).toHaveBeenCalledTimes(1);
      expect(dialogRef.close).toHaveBeenCalledWith({ outcome: WorkContractDialogOutcome.Refused });
    });

    it('takes with the re-fetched text id once the fresh preview is ticked again', () => {
      orderClient.takeOrder
        .mockReturnValueOnce(throwError(() => refusal('contract.text_mismatch')))
        .mockReturnValueOnce(
          of(TakeOrderResponse.fromJS({ orderId: ORDER_ID, employeeId: 'emp-1' }))
        );
      orderClient.getWorkContractPreview
        .mockReturnValueOnce(of(contract()))
        .mockReturnValueOnce(of(contract({ legalDocumentTextId: NEWER_TEXT_ID })));
      const facade = createFacade();
      facade.load({ mode: WorkContractDialogMode.Take, orderId: ORDER_ID });
      facade.accepted.set(true);
      facade.submit();

      facade.accepted.set(true);
      facade.submit();

      const command: TakeOrderCommand = orderClient.takeOrder.mock.calls[1][0];
      expect(command.toJSON()).toEqual({
        orderId: ORDER_ID,
        acceptedWorkContractTextId: NEWER_TEXT_ID,
      });
      expect(facade.noticeKey()).toBeNull();
      expect(dialogRef.close).toHaveBeenCalledWith({ outcome: WorkContractDialogOutcome.Accepted });
    });
  });

  describe('submit in accept mode', () => {
    it('accepts the contract once with the previewed text id and closes as accepted', () => {
      orderClient.acceptWorkContract.mockReturnValue(
        of(AcceptWorkContractResponse.fromJS({ orderId: ORDER_ID, acceptanceId: ACCEPTANCE_ID }))
      );
      const facade = createFacade();
      facade.load({ mode: WorkContractDialogMode.Accept, orderId: ORDER_ID });
      facade.accepted.set(true);

      facade.submit();

      expect(orderClient.takeOrder).not.toHaveBeenCalled();
      expect(orderClient.acceptWorkContract).toHaveBeenCalledTimes(1);
      const command: AcceptWorkContractCommand = orderClient.acceptWorkContract.mock.calls[0][0];
      expect(command).toBeInstanceOf(AcceptWorkContractCommand);
      expect(command.toJSON()).toEqual({
        orderId: ORDER_ID,
        acceptedWorkContractTextId: TEXT_ID,
      });
      expect(dialogRef.close).toHaveBeenCalledWith({ outcome: WorkContractDialogOutcome.Accepted });
    });

    it('handles the updated text the same way as the take', () => {
      orderClient.acceptWorkContract.mockReturnValue(
        throwError(() => refusal('contract.text_mismatch'))
      );
      const facade = createFacade();
      facade.load({ mode: WorkContractDialogMode.Accept, orderId: ORDER_ID });
      facade.accepted.set(true);

      facade.submit();

      expect(orderClient.getWorkContractPreview).toHaveBeenCalledTimes(2);
      expect(facade.accepted()).toBe(false);
      expect(facade.noticeKey()).toBe('api.contract.text_mismatch');
    });
  });

  describe('read mode', () => {
    it('never submits', () => {
      orderClient.getWorkContract.mockReturnValue(of(contract()));
      const facade = createFacade();
      facade.load({ mode: WorkContractDialogMode.Read, acceptanceId: ACCEPTANCE_ID });
      facade.accepted.set(true);

      facade.submit();

      expect(orderClient.takeOrder).not.toHaveBeenCalled();
      expect(orderClient.acceptWorkContract).not.toHaveBeenCalled();
      expect(facade.canSubmit()).toBe(false);
    });
  });

  it('closes without a result on cancel', () => {
    const facade = createFacade();

    facade.cancel();

    expect(dialogRef.close).toHaveBeenCalledWith();
  });
});
