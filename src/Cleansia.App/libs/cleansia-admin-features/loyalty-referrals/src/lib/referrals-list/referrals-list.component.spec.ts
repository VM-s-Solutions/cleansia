import { signal, WritableSignal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AdminReferralListItem, ReferralStatus } from '@cleansia/admin-services';
import { PermissionService } from '@cleansia/services';
import { TranslateModule } from '@ngx-translate/core';
import { ReferralInterventionMode } from '../referral-intervention-dialog/referral-intervention-dialog.models';
import { ReferralsListComponent } from './referrals-list.component';
import { ReferralsListFacade } from './referrals-list.facade';

describe('ReferralsListComponent', () => {
  let component: ReferralsListComponent;
  let facade: {
    lang: WritableSignal<string>;
    loadReferrals: jest.Mock;
    reverseReferral: jest.Mock;
    rejectReferral: jest.Mock;
    forceQualifyReferral: jest.Mock;
    releaseReferral: jest.Mock;
  };

  const qualified = AdminReferralListItem.fromJS({ id: 'ref-1', status: ReferralStatus.Qualified });
  const accepted = AdminReferralListItem.fromJS({ id: 'ref-2', status: ReferralStatus.Accepted });
  const held = AdminReferralListItem.fromJS({ id: 'ref-3', status: ReferralStatus.Accepted, holdReasons: 'address' });

  beforeEach(async () => {
    facade = {
      lang: signal('cs'),
      loadReferrals: jest.fn(),
      reverseReferral: jest.fn(),
      rejectReferral: jest.fn(),
      forceQualifyReferral: jest.fn(),
      releaseReferral: jest.fn(),
    };

    await TestBed.configureTestingModule({
      imports: [ReferralsListComponent, TranslateModule.forRoot()],
      providers: [{ provide: PermissionService, useValue: { hasPolicy: () => true } }],
    })
      .overrideComponent(ReferralsListComponent, {
        set: { template: '', imports: [], providers: [{ provide: ReferralsListFacade, useValue: facade }] },
      })
      .compileComponents();

    component = TestBed.createComponent(ReferralsListComponent).componentInstance;
  });

  const submit = (row: AdminReferralListItem, mode: ReferralInterventionMode) => {
    component.openIntervention(row, mode);
    component.onInterventionSubmit({ mode, reason: 'checked' });
  };

  it.each([
    ['reverse', qualified, 'reverseReferral'],
    ['forceQualify', accepted, 'forceQualifyReferral'],
    ['release', held, 'releaseReferral'],
    ['reject', held, 'rejectReferral'],
  ] as const)('sends %s to the facade intent that carries the hold state the row showed', (mode, row, intent) => {
    submit(row, mode);

    expect(facade[intent]).toHaveBeenCalledWith(row.id, 'checked', expect.any(Function));
    const others = (['reverseReferral', 'rejectReferral', 'forceQualifyReferral', 'releaseReferral'] as const).filter(
      (name) => name !== intent
    );
    for (const name of others) {
      expect(facade[name]).not.toHaveBeenCalled();
    }
  });
});
