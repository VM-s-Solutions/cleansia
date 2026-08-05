import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import {
  AdminClient,
  CreateEmailTemplateTranslationCommand,
  EmailType,
  SendTestEmailByTypeCommand,
  UpdateEmailTemplateCommand,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { EmailTypeDetailFacade } from './email-type-detail.facade';

describe('EmailTypeDetailFacade', () => {
  let facade: EmailTypeDetailFacade;
  let typeDetailsMock: jest.Mock;
  let updateMock: jest.Mock;
  let createMock: jest.Mock;
  let deleteMock: jest.Mock;
  let sendTestByTypeMock: jest.Mock;
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock };

  beforeEach(() => {
    TestBed.resetTestingModule();
    typeDetailsMock = jest.fn().mockReturnValue(of(null));
    updateMock = jest.fn().mockReturnValue(of({ id: 'tpl-1' }));
    createMock = jest.fn().mockReturnValue(of({ id: 'tpl-1' }));
    deleteMock = jest.fn().mockReturnValue(of({ id: 'tpl-1' }));
    sendTestByTypeMock = jest.fn().mockReturnValue(of({ id: 'tpl-1' }));
    snackbar = { showSuccess: jest.fn(), showError: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        EmailTypeDetailFacade,
        {
          provide: AdminClient,
          useValue: {
            adminEmailTemplateClient: {
              typeDetails: typeDetailsMock,
              update: updateMock,
              create: createMock,
              delete: deleteMock,
            },
            emailTemplateTypesClient: { sendTest: sendTestByTypeMock },
          },
        },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
        { provide: Router, useValue: { navigate: jest.fn() } },
      ],
    });

    facade = TestBed.inject(EmailTypeDetailFacade);
  });

  it('starts empty with no language selected', () => {
    expect(facade.emailTypeDetail()).toBeNull();
    expect(facade.selectedLanguageCode()).toBeNull();
    expect(facade.selectedTranslation).toBeNull();
    expect(facade.loading()).toBe(false);
  });

  it('auto-selects the first translation once the detail loads', () => {
    typeDetailsMock.mockReturnValue(
      of({ translations: [{ languageCode: 'cs' }, { languageCode: 'en' }] })
    );

    facade.loadEmailTypeDetail(EmailType.OrderReceipt);

    expect(facade.selectedLanguageCode()).toBe('cs');
    expect(facade.selectedTranslation).toEqual({ languageCode: 'cs' });
    expect(facade.loading()).toBe(false);
  });

  it('leaves nothing selected when the type carries no translations', () => {
    typeDetailsMock.mockReturnValue(of({ translations: [] }));

    facade.loadEmailTypeDetail(EmailType.OrderReceipt);

    expect(facade.selectedLanguageCode()).toBeNull();
    expect(facade.selectedTranslation).toBeNull();
  });

  it('settles loading and holds nothing when the detail read fails', () => {
    typeDetailsMock.mockReturnValue(throwError(() => new Error('boom')));

    facade.loadEmailTypeDetail(EmailType.OrderReceipt);

    expect(facade.emailTypeDetail()).toBeNull();
    expect(facade.loading()).toBe(false);
  });

  it('runs the completion callback and clears saving on both save branches', () => {
    const onComplete = jest.fn();

    facade.updateTranslation('tpl-1', 'body', onComplete);
    expect(onComplete).toHaveBeenCalledTimes(1);
    expect(facade.saving()).toBe(false);

    updateMock.mockReturnValue(throwError(() => new Error('boom')));
    facade.updateTranslation('tpl-1', 'body', onComplete);
    expect(onComplete).toHaveBeenCalledTimes(2);
    expect(facade.saving()).toBe(false);
    expect(snackbar.showSuccess).toHaveBeenCalledTimes(1);
  });

  it('re-reads the type detail after a create lands, and not when it fails', () => {
    facade.createTranslation(EmailType.OrderReceipt, 'lang-1', 'key', 'body');
    expect(typeDetailsMock).toHaveBeenCalledTimes(1);

    createMock.mockReturnValue(throwError(() => new Error('boom')));
    facade.createTranslation(EmailType.OrderReceipt, 'lang-1', 'key', 'body');
    expect(typeDetailsMock).toHaveBeenCalledTimes(1);
    expect(facade.creating()).toBe(false);
  });

  it('re-reads the type detail after a delete lands, and not when it fails', () => {
    facade.deleteTranslation('tpl-1', EmailType.OrderReceipt);
    expect(typeDetailsMock).toHaveBeenCalledTimes(1);

    deleteMock.mockReturnValue(throwError(() => new Error('boom')));
    facade.deleteTranslation('tpl-1', EmailType.OrderReceipt);
    expect(typeDetailsMock).toHaveBeenCalledTimes(1);
    expect(facade.deleting()).toBe(false);
  });

  describe('command bodies on the wire', () => {
    it('serializes a translation update with the template id and the body', () => {
      facade.updateTranslation('tpl-1', '<p>Hi</p>');

      const command: UpdateEmailTemplateCommand = updateMock.mock.calls[0][1];
      expect(command).toBeInstanceOf(UpdateEmailTemplateCommand);
      expect(command.toJSON()).toEqual({
        emailTemplateId: 'tpl-1',
        value: '<p>Hi</p>',
      });
    });

    it('serializes a by-type test send with the numeric email type, the language and the recipient', () => {
      facade.sendTestEmail(EmailType.OrderReceipt, 'cs', 'ops@cleansia.cz');

      const command: SendTestEmailByTypeCommand =
        sendTestByTypeMock.mock.calls[0][1];
      expect(command).toBeInstanceOf(SendTestEmailByTypeCommand);
      expect(command.toJSON()).toEqual({
        emailType: EmailType.OrderReceipt,
        languageCode: 'cs',
        recipientEmail: 'ops@cleansia.cz',
      });
    });

    it('serializes a translation create with the type, the language id, the key and the body', () => {
      facade.createTranslation(
        EmailType.PeriodClosed,
        'lang-1',
        'period.closed.subject',
        'Your period is closed'
      );

      const command: CreateEmailTemplateTranslationCommand =
        createMock.mock.calls[0][0];
      expect(command).toBeInstanceOf(CreateEmailTemplateTranslationCommand);
      expect(command.toJSON()).toEqual({
        emailType: EmailType.PeriodClosed,
        languageId: 'lang-1',
        key: 'period.closed.subject',
        value: 'Your period is closed',
      });
    });
  });
});
