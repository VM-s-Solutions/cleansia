import { TestBed } from '@angular/core/testing';
import {
  AdminClient,
  GetEmployeeDocumentsRequest,
  RejectDocumentCommand,
  SortDefinition,
  SortDirection,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { DialogService } from 'primeng/dynamicdialog';
import { of, throwError } from 'rxjs';
import { EmployeeDocumentsFacade } from './employee-documents.facade';

describe('EmployeeDocumentsFacade', () => {
  let facade: EmployeeDocumentsFacade;
  let getPagedMock: jest.Mock;
  let approveMock: jest.Mock;
  let rejectMock: jest.Mock;
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock };

  beforeEach(() => {
    TestBed.resetTestingModule();
    getPagedMock = jest.fn().mockReturnValue(of({ data: [], total: 0 }));
    approveMock = jest.fn().mockReturnValue(of({ documentId: 'doc-1' }));
    rejectMock = jest.fn().mockReturnValue(of({ documentId: 'doc-1' }));
    snackbar = { showSuccess: jest.fn(), showError: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        EmployeeDocumentsFacade,
        {
          provide: AdminClient,
          useValue: {
            adminEmployeeDocumentClient: {
              getPaged: getPagedMock,
              approve: approveMock,
              reject: rejectMock,
            },
          },
        },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
        { provide: DialogService, useValue: { open: jest.fn() } },
      ],
    });

    facade = TestBed.inject(EmployeeDocumentsFacade);
  });

  it('starts empty with nothing loading', () => {
    expect(facade.documents()).toEqual([]);
    expect(facade.documentsLoading()).toBe(false);
  });

  it('settles loading and keeps the list empty when the read fails', () => {
    getPagedMock.mockReturnValue(throwError(() => new Error('boom')));

    facade.loadEmployeeDocuments('emp-1');

    expect(facade.documents()).toEqual([]);
    expect(facade.documentsLoading()).toBe(false);
  });

  it('holds the returned documents', () => {
    getPagedMock.mockReturnValue(of({ data: [{ id: 'doc-1' }], total: 1 }));

    facade.loadEmployeeDocuments('emp-1');

    expect(facade.documents()).toEqual([{ id: 'doc-1' }]);
  });

  it('re-reads the documents after an approve lands, and not when it fails', () => {
    facade.approveDocument('doc-1', 'emp-1');
    expect(getPagedMock).toHaveBeenCalledTimes(1);
    expect(snackbar.showSuccess).toHaveBeenCalledWith(
      'pages.employee_detail.messages.document_approve_success'
    );

    approveMock.mockReturnValue(throwError(() => new Error('boom')));
    facade.approveDocument('doc-1', 'emp-1');
    expect(getPagedMock).toHaveBeenCalledTimes(1);
  });

  it('does not re-read when the approve carries no employee id', () => {
    facade.approveDocument('doc-1', undefined);

    expect(getPagedMock).not.toHaveBeenCalled();
  });

  describe('request bodies on the wire', () => {
    it('serializes the document page request with the filter, the sort and the window', () => {
      facade.loadEmployeeDocuments('emp-1');

      const request: GetEmployeeDocumentsRequest = getPagedMock.mock.calls[0][0];
      expect(request).toBeInstanceOf(GetEmployeeDocumentsRequest);
      expect(request.sort?.[0]).toBeInstanceOf(SortDefinition);
      expect(request.toJSON()).toEqual({
        sort: [{ field: 'CreatedOn', direction: SortDirection.Descending }],
        offset: 0,
        limit: 100,
        filter: {
          isActive: true,
          employeeId: 'emp-1',
          documentType: undefined,
          status: undefined,
          latestVersionOnly: true,
        },
      });
    });

    it('serializes a document rejection with the id and the reason as notes', () => {
      facade.rejectDocument('doc-1', 'illegible scan', 'emp-1');

      const command: RejectDocumentCommand = rejectMock.mock.calls[0][1];
      expect(command).toBeInstanceOf(RejectDocumentCommand);
      expect(command.toJSON()).toEqual({
        documentId: 'doc-1',
        notes: 'illegible scan',
      });
    });
  });
});
