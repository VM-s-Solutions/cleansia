import { TestBed } from '@angular/core/testing';
import {
  DocumentType,
  PartnerClient,
  SaveMyDocumentsCommand,
} from '@cleansia/partner-services';
import {
  FileValidationErrorService,
  SnackbarService,
} from '@cleansia/services';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { ProfileDocumentsFacade } from './profile-documents.facade';

describe('ProfileDocumentsFacade', () => {
  let employeeClient: {
    saveMyDocuments: jest.Mock;
    getMyDocuments: jest.Mock;
    requestMyDocumentDeletion: jest.Mock;
  };
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock };
  let facade: ProfileDocumentsFacade;

  beforeEach(() => {
    TestBed.resetTestingModule();
    employeeClient = {
      saveMyDocuments: jest.fn().mockReturnValue(of({})),
      getMyDocuments: jest.fn().mockReturnValue(of({ documents: [] })),
      requestMyDocumentDeletion: jest.fn().mockReturnValue(of({})),
    };
    snackbar = { showSuccess: jest.fn(), showError: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        ProfileDocumentsFacade,
        { provide: PartnerClient, useValue: { employeeClient } },
        { provide: SnackbarService, useValue: snackbar },
        {
          provide: FileValidationErrorService,
          useValue: { handleFileValidationErrors: jest.fn() },
        },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
        { provide: Store, useValue: { dispatch: jest.fn() } },
      ],
    });

    facade = TestBed.inject(ProfileDocumentsFacade);
  });

  async function stagePassportScan(): Promise<void> {
    const file = new File(['scan-bytes'], 'passport.png', { type: 'image/png' });
    await facade.onEmployeeDocumentFilesSelected([file], DocumentType.Passport);
  }

  it('refuses to call the endpoint with nothing staged', async () => {
    await facade.saveEmployeeDocuments();

    expect(employeeClient.saveMyDocuments).not.toHaveBeenCalled();
    expect(snackbar.showError).toHaveBeenCalledWith(
      'global.messages.documents.no_documents_to_save'
    );
  });

  it('clears the staged list and re-reads once the save lands', async () => {
    await stagePassportScan();
    expect(facade.hasStagedDocuments()).toBe(true);

    await facade.saveEmployeeDocuments();

    expect(snackbar.showSuccess).toHaveBeenCalledWith(
      'global.messages.documents.upload_success'
    );
    expect(facade.hasStagedDocuments()).toBe(false);
    expect(employeeClient.getMyDocuments).toHaveBeenCalled();
    expect(facade.documentsSaving()).toBe(false);
  });

  // Every member of a generated command is optional, so a dropped assignment type-checks.
  // This pins the serialized body instead (ADR-0031).
  it('serializes each staged document with its type and its file', async () => {
    await stagePassportScan();

    await facade.saveEmployeeDocuments();

    const command: SaveMyDocumentsCommand =
      employeeClient.saveMyDocuments.mock.calls[0][0];
    expect(command).toBeInstanceOf(SaveMyDocumentsCommand);
    expect(command.toJSON()).toEqual({
      documents: [
        {
          documentType: DocumentType.Passport,
          file: {
            fileName: 'passport.png',
            base64Content: expect.stringContaining('data:image/png;base64,'),
            contentType: 'image/png',
          },
        },
      ],
    });
  });
  // The whole reason the delete endpoint was replaced: asking removes NOTHING, so the list the
  // cleaner is looking at must not change. The button it replaced soft-deleted on the spot, which
  // flipped AreDocumentsUploaded and re-engaged the registration lock — one click, no way back.
  it('leaves the document list alone when a removal is requested', async () => {
    employeeClient.getMyDocuments.mockReturnValue(
      of({ documents: [{ documentId: 'doc-1', fileName: 'passport.png' }] })
    );
    await facade.loadEmployeeDocuments();

    await facade.requestDocumentDeletion('doc-1', 'Wrong file');

    expect(employeeClient.requestMyDocumentDeletion).toHaveBeenCalledWith(
      'doc-1',
      expect.objectContaining({ reason: 'Wrong file' })
    );
    expect(facade.documents()).toHaveLength(1);
    expect(facade.deletionRequestInFlight()).toBe(false);
    expect(snackbar.showSuccess).toHaveBeenCalled();
  });

  // Every member of a generated body is optional, so a dropped assignment type-checks. Pin the
  // serialized shape instead (ADR-0031).
  it('sends the reason the server requires', async () => {
    await facade.requestDocumentDeletion('doc-1', 'It expired last week');

    const body = employeeClient.requestMyDocumentDeletion.mock.calls[0][1];
    expect(body.toJSON()).toEqual({ reason: 'It expired last week' });
  });

  // A refusal has to leave the flag down, or the dialog's confirm button stays disabled forever and
  // the cleaner cannot retry with a better reason.
  it('clears the in-flight flag when the request is refused', async () => {
    employeeClient.requestMyDocumentDeletion.mockReturnValue(
      throwError(() => new Error('already requested'))
    );

    await expect(
      facade.requestDocumentDeletion('doc-1', 'Wrong file')
    ).rejects.toBeTruthy();
    expect(facade.deletionRequestInFlight()).toBe(false);
  });
});
