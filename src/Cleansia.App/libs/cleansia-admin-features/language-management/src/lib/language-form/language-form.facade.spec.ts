import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import {
  AdminClient,
  CreateLanguageCommand,
  UpdateLanguageCommand,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { LanguageFormData, LanguageFormFacade } from './language-form.facade';

describe('LanguageFormFacade', () => {
  let facade: LanguageFormFacade;
  let createMock: jest.Mock;
  let updateMock: jest.Mock;
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock };
  let navigate: jest.Mock;

  const formData: LanguageFormData = { code: 'uk', name: 'Українська' };

  beforeEach(() => {
    TestBed.resetTestingModule();
    createMock = jest.fn().mockReturnValue(of({ id: 'lng-1' }));
    updateMock = jest.fn().mockReturnValue(of({ id: 'lng-1' }));
    snackbar = { showSuccess: jest.fn(), showError: jest.fn() };
    navigate = jest.fn();

    TestBed.configureTestingModule({
      providers: [
        LanguageFormFacade,
        {
          provide: AdminClient,
          useValue: {
            adminLanguageClient: {
              create: createMock,
              update: updateMock,
              details: jest.fn().mockReturnValue(of(null)),
            },
          },
        },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
        { provide: Router, useValue: { navigate } },
      ],
    });

    facade = TestBed.inject(LanguageFormFacade);
  });

  it('reports success and returns to the list once a create lands', () => {
    facade.createLanguage(formData);

    expect(snackbar.showSuccess).toHaveBeenCalledWith(
      'pages.language_form.messages.create_success'
    );
    expect(navigate).toHaveBeenCalled();
    expect(facade.saving()).toBe(false);
  });

  it('clears saving and stays on the form when a create fails', () => {
    createMock.mockReturnValue(throwError(() => new Error('boom')));

    facade.createLanguage(formData);

    expect(facade.saving()).toBe(false);
    expect(navigate).not.toHaveBeenCalled();
  });

  // Every member of a generated command is optional, so a dropped assignment type-checks.
  // These pin the serialized body instead (ADR-0031).
  describe('command bodies on the wire', () => {
    it('serializes a create with the code and the name', () => {
      facade.createLanguage(formData);

      const command: CreateLanguageCommand = createMock.mock.calls[0][0];
      expect(command).toBeInstanceOf(CreateLanguageCommand);
      expect(command.toJSON()).toEqual({ code: 'uk', name: 'Українська' });
    });

    it('serializes an update with the language id and the name, never the code', () => {
      facade.updateLanguage('lng-1', formData);

      const command: UpdateLanguageCommand = updateMock.mock.calls[0][1];
      expect(command).toBeInstanceOf(UpdateLanguageCommand);
      // The code is the FK every translation bundle is keyed on, so it is not editable.
      expect(command.toJSON()).toEqual({
        languageId: 'lng-1',
        name: 'Українська',
      });
    });
  });
});
