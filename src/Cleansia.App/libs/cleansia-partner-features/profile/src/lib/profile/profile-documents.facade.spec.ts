import { TestBed } from '@angular/core/testing';
import {
  DocumentType,
  PartnerClient,
  SaveMyDocumentsCommand,
} from '@cleansia/partner-services';
import {
  DialogService,
  FileValidationErrorService,
  SnackbarService,
} from '@cleansia/services';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { of } from 'rxjs';
import { ProfileDocumentsFacade } from './profile-documents.facade';

describe('ProfileDocumentsFacade', () => {
  let employeeClient: {
    saveMyDocuments: jest.Mock;
    getMyDocuments: jest.Mock;
    deleteMyDocument: jest.Mock;
  };
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock };
  let facade: ProfileDocumentsFacade;

  beforeEach(() => {
    TestBed.resetTestingModule();
    employeeClient = {
      saveMyDocuments: jest.fn().mockReturnValue(of({})),
      getMyDocuments: jest.fn().mockReturnValue(of({ documents: [] })),
      deleteMyDocument: jest.fn().mockReturnValue(of({})),
    };
    snackbar = { showSuccess: jest.fn(), showError: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        ProfileDocumentsFacade,
        { provide: PartnerClient, useValue: { employeeClient } },
        { provide: SnackbarService, useValue: snackbar },
        { provide: DialogService, useValue: { confirmTranslated: jest.fn() } },
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
});
