import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import {
  AdminClient,
  SendTestEmailCommand,
  UpdateEmailTemplateCommand,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { EmailTemplateFormFacade } from './email-template-form.facade';

describe('EmailTemplateFormFacade', () => {
  let facade: EmailTemplateFormFacade;
  let detailsMock: jest.Mock;
  let updateMock: jest.Mock;
  let sendTestMock: jest.Mock;
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock };
  let navigate: jest.Mock;

  beforeEach(() => {
    TestBed.resetTestingModule();
    detailsMock = jest.fn().mockReturnValue(of(null));
    updateMock = jest.fn().mockReturnValue(of({ id: 'tpl-1' }));
    sendTestMock = jest.fn().mockReturnValue(of({ id: 'tpl-1' }));
    snackbar = { showSuccess: jest.fn(), showError: jest.fn() };
    navigate = jest.fn();

    TestBed.configureTestingModule({
      providers: [
        EmailTemplateFormFacade,
        {
          provide: AdminClient,
          useValue: {
            adminEmailTemplateClient: {
              details: detailsMock,
              update: updateMock,
              sendTest: sendTestMock,
            },
          },
        },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
        { provide: Router, useValue: { navigate } },
      ],
    });

    facade = TestBed.inject(EmailTemplateFormFacade);
  });

  it('starts empty with nothing loading', () => {
    expect(facade.template()).toBeNull();
    expect(facade.loading()).toBe(false);
    expect(facade.saving()).toBe(false);
    expect(facade.sendingTestEmail()).toBe(false);
  });

  it('settles loading and leaves the template null when the detail read fails', () => {
    detailsMock.mockReturnValue(throwError(() => new Error('boom')));

    facade.loadTemplate('tpl-1');

    expect(facade.template()).toBeNull();
    expect(facade.loading()).toBe(false);
  });

  it('reports success and returns to the list once a save lands', () => {
    facade.updateTemplate('tpl-1', { value: '<p>Hi</p>' });

    expect(snackbar.showSuccess).toHaveBeenCalledWith(
      'pages.template_management.messages.save_success'
    );
    expect(navigate).toHaveBeenCalled();
    expect(facade.saving()).toBe(false);
  });

  it('clears saving and stays on the form when a save fails', () => {
    updateMock.mockReturnValue(throwError(() => new Error('boom')));

    facade.updateTemplate('tpl-1', { value: '<p>Hi</p>' });

    expect(facade.saving()).toBe(false);
    expect(navigate).not.toHaveBeenCalled();
    expect(snackbar.showSuccess).not.toHaveBeenCalled();
  });

  it('clears the test-email flag whether the send lands or fails', () => {
    facade.sendTestEmail('tpl-1', 'ops@cleansia.cz');
    expect(facade.sendingTestEmail()).toBe(false);

    sendTestMock.mockReturnValue(throwError(() => new Error('boom')));
    facade.sendTestEmail('tpl-1', 'ops@cleansia.cz');
    expect(facade.sendingTestEmail()).toBe(false);
  });

  describe('command bodies on the wire', () => {
    it('serializes a template update with the template id and the body', () => {
      facade.updateTemplate('tpl-1', { value: '<p>Hi</p>' });

      const command: UpdateEmailTemplateCommand = updateMock.mock.calls[0][1];
      expect(command).toBeInstanceOf(UpdateEmailTemplateCommand);
      expect(command.toJSON()).toEqual({
        emailTemplateId: 'tpl-1',
        value: '<p>Hi</p>',
      });
    });

    it('serializes a test send with the template id and the recipient', () => {
      facade.sendTestEmail('tpl-1', 'ops@cleansia.cz');

      const command: SendTestEmailCommand = sendTestMock.mock.calls[0][1];
      expect(command).toBeInstanceOf(SendTestEmailCommand);
      expect(command.toJSON()).toEqual({
        emailTemplateId: 'tpl-1',
        recipientEmail: 'ops@cleansia.cz',
      });
    });
  });
});
