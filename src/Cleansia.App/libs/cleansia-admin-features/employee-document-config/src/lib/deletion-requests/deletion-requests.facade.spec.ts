import { TestBed } from '@angular/core/testing';
import {
  AdminClient,
  DeletionRequestsClient,
  DocumentDeletionRequestDto,
  DocumentDeletionRequestStatus,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { Subject, of, throwError } from 'rxjs';
import { DeletionRequestsFacade } from './deletion-requests.facade';

describe('DeletionRequestsFacade', () => {
  let facade: DeletionRequestsFacade;
  let deletionRequests: jest.Mock;
  let resolve: jest.Mock;
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock };

  beforeEach(() => {
    TestBed.resetTestingModule();
    deletionRequests = jest.fn().mockReturnValue(of([]));
    resolve = jest.fn().mockReturnValue(of({ requestId: 'r1' }));
    snackbar = { showSuccess: jest.fn(), showError: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        DeletionRequestsFacade,
        {
          provide: AdminClient,
          useValue: { adminEmployeeDocumentClient: { deletionRequests } },
        },
        // Provided separately because NSwag put `resolve` on its own generated client and
        // AdminClient does not carry it — a spec that only stubs AdminClient would fail to inject.
        { provide: DeletionRequestsClient, useValue: { resolve } },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (key: string) => key } },
      ],
    });

    facade = TestBed.inject(DeletionRequestsFacade);
  });

  it('opens on Pending, because the queue is a to-do list', () => {
    expect(facade.status()).toBe(DocumentDeletionRequestStatus.Pending);

    facade.load();

    expect(deletionRequests).toHaveBeenCalledWith(DocumentDeletionRequestStatus.Pending);
  });

  it('asks for every status when the filter is cleared', () => {
    facade.selectStatus(null);

    expect(deletionRequests).toHaveBeenLastCalledWith(undefined);
  });

  it('drops a response the filter has already moved past', () => {
    // Still in flight when the filter changes — the shape of a stale answer arriving late.
    const inFlight = new Subject<DocumentDeletionRequestDto[]>();
    deletionRequests.mockReturnValueOnce(inFlight.asObservable());

    facade.load();
    facade.selectStatus(DocumentDeletionRequestStatus.Approved);

    const stale = new DocumentDeletionRequestDto();
    stale.id = 'stale';
    inFlight.next([stale]);
    inFlight.complete();

    expect(facade.requests().some((request) => request.id === 'stale')).toBe(false);
  });

  it('reports an approval and a rejection differently', () => {
    facade.resolve('r1', true, 'looks fine');
    expect(snackbar.showSuccess).toHaveBeenLastCalledWith(
      'pages.document_deletion_requests.messages.approve_success',
    );

    facade.resolve('r1', false, null);
    expect(snackbar.showSuccess).toHaveBeenLastCalledWith(
      'pages.document_deletion_requests.messages.reject_success',
    );
  });

  it('sends blank notes as undefined rather than an empty string', () => {
    facade.resolve('r1', true, null);

    const request = resolve.mock.calls[0][1];
    expect(request.approve).toBe(true);
    expect(request.notes).toBeUndefined();
  });

  it('says nothing and clears the in-flight flag when the call fails', () => {
    resolve.mockReturnValueOnce(throwError(() => new Error('boom')));

    facade.resolve('r1', true, null);

    expect(snackbar.showSuccess).not.toHaveBeenCalled();
    expect(facade.resolving()).toBe(false);
  });
});
