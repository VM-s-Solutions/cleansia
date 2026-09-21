import { TestBed } from '@angular/core/testing';
import { AdminClient, DocumentRequirementDto } from '@cleansia/admin-services';
import { DialogService, SnackbarService } from '@cleansia/services';
import { of } from 'rxjs';
import { DocumentRequirementsFacade } from './document-requirements.facade';

describe('DocumentRequirementsFacade', () => {
  let facade: DocumentRequirementsFacade;
  let requirementsGetMock: jest.Mock;
  let requirementsDeleteMock: jest.Mock;
  let confirmMock: jest.Mock;
  let snackbar: { showSuccessTranslated: jest.Mock };

  const requirements = [DocumentRequirementDto.fromJS({ id: 'req-1', countryId: 'c-1' })];

  beforeEach(() => {
    TestBed.resetTestingModule();
    requirementsGetMock = jest.fn().mockReturnValue(of(requirements));
    requirementsDeleteMock = jest.fn().mockReturnValue(of({ id: 'req-1' }));
    confirmMock = jest.fn().mockReturnValue(of(true));
    snackbar = { showSuccessTranslated: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        DocumentRequirementsFacade,
        {
          provide: AdminClient,
          useValue: {
            adminCountryClient: { getOverview: jest.fn().mockReturnValue(of([])) },
            adminEmployeeDocumentClient: {
              requirementsGet: requirementsGetMock,
              requirementsDelete: requirementsDeleteMock,
            },
          },
        },
        { provide: SnackbarService, useValue: snackbar },
        { provide: DialogService, useValue: { confirmTranslated: confirmMock } },
      ],
    });

    facade = TestBed.inject(DocumentRequirementsFacade);
    facade.selectCountry('c-1');
  });

  it('reads the selected country\'s requirements', () => {
    expect(requirementsGetMock).toHaveBeenCalledWith('c-1');
    expect(facade.requirements()).toEqual(requirements);
    expect(facade.loading()).toBe(false);
  });

  describe('deleteRequirement', () => {
    it('asks in red with a delete label, deletes, toasts and re-reads the country', () => {
      facade.deleteRequirement('req-1');

      expect(confirmMock).toHaveBeenCalledWith(
        'pages.document_requirements.delete_confirm',
        'pages.document_requirements.delete_confirm_title',
        undefined,
        { danger: true, acceptLabelKey: 'global.actions.delete' }
      );
      expect(requirementsDeleteMock).toHaveBeenCalledWith('req-1');
      expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith('pages.document_requirements.messages.delete_success');
      expect(requirementsGetMock).toHaveBeenCalledTimes(2);
      expect(facade.saving()).toBe(false);
    });

    it('does nothing when the confirmation is declined', () => {
      confirmMock.mockReturnValue(of(false));

      facade.deleteRequirement('req-1');

      expect(requirementsDeleteMock).not.toHaveBeenCalled();
      expect(requirementsGetMock).toHaveBeenCalledTimes(1);
      expect(facade.saving()).toBe(false);
    });
  });
});
