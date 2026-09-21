import { TestBed } from '@angular/core/testing';
import { TranslateService } from '@ngx-translate/core';
import { Confirmation, ConfirmationService } from 'primeng/api';
import { DialogService } from './dialog.service';

describe('DialogService', () => {
  let service: DialogService;
  let confirmMock: jest.Mock;
  let instantMock: jest.Mock;

  const lastConfirmation = (): Confirmation => confirmMock.mock.calls[0][0] as Confirmation;

  beforeEach(() => {
    confirmMock = jest.fn();
    instantMock = jest.fn((key: string, params?: Record<string, unknown>) =>
      params ? `${key}:${JSON.stringify(params)}` : `[${key}]`
    );

    TestBed.configureTestingModule({
      providers: [
        DialogService,
        { provide: ConfirmationService, useValue: { confirm: confirmMock } },
        { provide: TranslateService, useValue: { instant: instantMock } },
      ],
    });

    service = TestBed.inject(DialogService);
  });

  describe('confirmTranslated', () => {
    it('translates the message with its params, the header and both button labels', () => {
      service.confirmTranslated('pages.x.confirm', 'pages.x.title', { name: 'Ada' }).subscribe();

      const confirmation = lastConfirmation();
      expect(confirmation.message).toBe('pages.x.confirm:{"name":"Ada"}');
      expect(confirmation.header).toBe('[pages.x.title]');
      expect(confirmation.acceptLabel).toBe('[global.actions.confirm]');
      expect(confirmation.rejectLabel).toBe('[global.actions.cancel]');
    });

    it('falls back to the shared confirm header when none is given', () => {
      service.confirmTranslated('pages.x.confirm').subscribe();

      expect(lastConfirmation().header).toBe('[global.dialog.confirm]');
    });

    it('renders the accept button as the primary action and the reject as text', () => {
      service.confirmTranslated('pages.x.confirm').subscribe();

      const confirmation = lastConfirmation();
      expect(confirmation.acceptButtonProps).toEqual({ severity: 'primary' });
      expect(confirmation.rejectButtonProps).toEqual({ text: true });
      expect(confirmation.icon).toBe('pi pi-exclamation-triangle');
    });

    it('turns the accept button red for a destructive act and lets the caller name it', () => {
      service
        .confirmTranslated('pages.x.confirm', undefined, undefined, {
          danger: true,
          acceptLabelKey: 'pages.x.delete',
          icon: 'pi pi-trash',
        })
        .subscribe();

      const confirmation = lastConfirmation();
      expect(confirmation.acceptButtonProps).toEqual({ severity: 'danger' });
      expect(confirmation.acceptLabel).toBe('[pages.x.delete]');
      expect(confirmation.icon).toBe('pi pi-trash');
    });

    it('emits true once on accept and completes', () => {
      const seen: boolean[] = [];
      let completed = false;
      service.confirmTranslated('pages.x.confirm').subscribe({
        next: (value) => seen.push(value),
        complete: () => (completed = true),
      });

      lastConfirmation().accept?.();

      expect(seen).toEqual([true]);
      expect(completed).toBe(true);
    });

    it('emits false once on reject, which is also what closing the dialog fires', () => {
      const seen: boolean[] = [];
      service.confirmTranslated('pages.x.confirm').subscribe((value) => seen.push(value));

      lastConfirmation().reject?.();

      expect(seen).toEqual([false]);
    });
  });

  describe('confirmDelete', () => {
    it('is the destructive shape with the delete label and the item name in the message', () => {
      service.confirmDelete('Ada').subscribe();

      const confirmation = lastConfirmation();
      expect(confirmation.message).toBe('global.dialog.confirm_delete_item:{"item":"Ada"}');
      expect(confirmation.header).toBe('[global.dialog.delete]');
      expect(confirmation.acceptLabel).toBe('[global.actions.delete]');
      expect(confirmation.acceptButtonProps).toEqual({ severity: 'danger' });
      expect(confirmation.icon).toBe('pi pi-trash');
    });

    it('asks generically when no item name is given', () => {
      service.confirmDelete().subscribe();

      expect(lastConfirmation().message).toBe('[global.dialog.confirm_delete]');
    });
  });
});
