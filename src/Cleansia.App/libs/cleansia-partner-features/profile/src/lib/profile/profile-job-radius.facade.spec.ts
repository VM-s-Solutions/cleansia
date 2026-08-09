import { TestBed } from '@angular/core/testing';
import {
  EmployeeItem,
  PartnerClient,
  UpdateJobRadiusCommand,
  UpdateJobRadiusResponse,
} from '@cleansia/partner-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { Subject, of, throwError } from 'rxjs';
import { ProfileJobRadiusFacade } from './profile-job-radius.facade';
import { JOB_RADIUS_STARTING_KM } from './profile-job-radius.models';

describe('ProfileJobRadiusFacade', () => {
  let employeeClient: { updateJobRadius: jest.Mock };
  let snackbar: {
    showSuccess: jest.Mock;
    showError: jest.Mock;
    showApiError: jest.Mock;
  };

  const employeeWithRadius = (jobRadiusKm: number | null): EmployeeItem =>
    EmployeeItem.fromJS({ id: 'emp-1', jobRadiusKm });

  const createFacade = (): ProfileJobRadiusFacade => {
    TestBed.configureTestingModule({
      providers: [
        ProfileJobRadiusFacade,
        { provide: PartnerClient, useValue: { employeeClient } },
        { provide: SnackbarService, useValue: snackbar },
        {
          provide: TranslateService,
          useValue: { instant: (key: string) => key, currentLang: 'en' },
        },
      ],
    });

    return TestBed.inject(ProfileJobRadiusFacade);
  };

  const sentCommand = (): UpdateJobRadiusCommand =>
    employeeClient.updateJobRadius.mock.calls[0][0] as UpdateJobRadiusCommand;

  beforeEach(() => {
    employeeClient = {
      updateJobRadius: jest
        .fn()
        .mockImplementation((command: UpdateJobRadiusCommand) =>
          of(
            UpdateJobRadiusResponse.fromJS({
              employeeId: command.employeeId,
              radiusKm: command.radiusKm ?? null,
            })
          )
        ),
    };
    snackbar = {
      showSuccess: jest.fn(),
      showError: jest.fn(),
      showApiError: jest.fn(),
    };
  });

  describe('seeding from the profile read', () => {
    it('renders the saved distance with the limit on', () => {
      const facade = createFacade();

      facade.seed(employeeWithRadius(120));

      expect(facade.loaded()).toBe(true);
      expect(facade.loadFailed()).toBe(false);
      expect(facade.formGroup.getRawValue()).toEqual({
        limitEnabled: true,
        radiusKm: '120',
      });
    });

    it('renders an unset radius as the country-wide choice, with a distance ready', () => {
      const facade = createFacade();

      facade.seed(employeeWithRadius(null));

      expect(facade.limitEnabled()).toBe(false);
      expect(facade.formGroup.getRawValue()).toEqual({
        limitEnabled: false,
        radiusKm: String(JOB_RADIUS_STARTING_KM),
      });
    });

    it('renders the error state when the profile read never arrived', () => {
      const facade = createFacade();

      facade.markUnavailable();

      expect(facade.loadFailed()).toBe(true);
      expect(facade.loaded()).toBe(false);
    });

    it('clears the error state when a later read succeeds', () => {
      const facade = createFacade();
      facade.markUnavailable();

      facade.seed(employeeWithRadius(30));

      expect(facade.loadFailed()).toBe(false);
      expect(facade.loaded()).toBe(true);
    });
  });

  describe('save', () => {
    it('sends the chosen distance', () => {
      const facade = createFacade();
      facade.seed(employeeWithRadius(null));

      facade.formGroup.setValue({ limitEnabled: true, radiusKm: '42' });
      facade.onSubmit();

      const command = sentCommand();
      expect(command).toBeInstanceOf(UpdateJobRadiusCommand);
      expect(command.toJSON()).toEqual({ employeeId: 'emp-1', radiusKm: 42 });
      expect(snackbar.showSuccess).toHaveBeenCalledWith(
        'global.messages.profile.job_radius_saved'
      );
      expect(facade.saving()).toBe(false);
    });

    it('clears a saved radius back to the country-wide board without sending a zero', () => {
      const facade = createFacade();
      facade.seed(employeeWithRadius(120));

      facade.formGroup.controls.limitEnabled.setValue(false);
      facade.onSubmit();

      const body = sentCommand().toJSON();
      expect(body.radiusKm).toBeUndefined();
      expect(body.radiusKm).not.toBe(0);
    });

    it('re-renders what the server stored, so a cleared radius shows as country-wide', () => {
      const facade = createFacade();
      facade.seed(employeeWithRadius(120));

      facade.formGroup.controls.limitEnabled.setValue(false);
      facade.onSubmit();

      expect(facade.formGroup.getRawValue()).toEqual({
        limitEnabled: false,
        radiusKm: String(JOB_RADIUS_STARTING_KM),
      });
    });

    it('re-renders a distance the server rounded or corrected rather than what was typed', () => {
      employeeClient.updateJobRadius.mockReturnValue(
        of(UpdateJobRadiusResponse.fromJS({ employeeId: 'emp-1', radiusKm: 60 }))
      );
      const facade = createFacade();
      facade.seed(employeeWithRadius(null));

      facade.formGroup.setValue({ limitEnabled: true, radiusKm: '61' });
      facade.onSubmit();

      expect(facade.formGroup.controls.radiusKm.value).toBe('60');
    });

    it('does not confirm a refused save, and clears the saving flag', () => {
      // The refusal reaches the cleaner as the shared interceptor's
      // api.employee.job_radius_out_of_range toast; a second one would double up.
      employeeClient.updateJobRadius.mockReturnValue(
        throwError(() => ({
          errors: { RadiusKm: 'employee.job_radius_out_of_range' },
        }))
      );
      const facade = createFacade();
      facade.seed(employeeWithRadius(null));

      facade.formGroup.setValue({ limitEnabled: true, radiusKm: '400' });
      facade.onSubmit();

      expect(snackbar.showSuccess).not.toHaveBeenCalled();
      expect(snackbar.showApiError).not.toHaveBeenCalled();
      expect(facade.saving()).toBe(false);
    });

    it('keeps what the cleaner typed when the save is refused, so it can be corrected', () => {
      employeeClient.updateJobRadius.mockReturnValue(
        throwError(() => ({
          errors: { RadiusKm: 'employee.job_radius_out_of_range' },
        }))
      );
      const facade = createFacade();
      facade.seed(employeeWithRadius(120));

      facade.formGroup.setValue({ limitEnabled: true, radiusKm: '400' });
      facade.onSubmit();

      expect(facade.formGroup.getRawValue()).toEqual({
        limitEnabled: true,
        radiusKm: '400',
      });
    });

    it('refuses to save over a profile it could not read', () => {
      const facade = createFacade();
      facade.markUnavailable();

      facade.formGroup.setValue({ limitEnabled: true, radiusKm: '42' });
      facade.onSubmit();

      expect(employeeClient.updateJobRadius).not.toHaveBeenCalled();
    });

    it('reports the profile is not loaded rather than sending an empty employee id', () => {
      const facade = createFacade();
      facade.seed(EmployeeItem.fromJS({ jobRadiusKm: 30 }));

      facade.onSubmit();

      expect(employeeClient.updateJobRadius).not.toHaveBeenCalled();
      expect(snackbar.showError).toHaveBeenCalledWith(
        'global.messages.profile.not_loaded'
      );
    });

    it('does not send a distance the server would refuse', () => {
      const facade = createFacade();
      facade.seed(employeeWithRadius(null));

      facade.formGroup.setValue({ limitEnabled: true, radiusKm: '501' });

      expect(facade.canSubmit()).toBe(false);
      facade.onSubmit();
      expect(employeeClient.updateJobRadius).not.toHaveBeenCalled();
    });

    it('lets the country-wide choice through however the distance box was left', () => {
      const facade = createFacade();
      facade.seed(employeeWithRadius(120));

      facade.formGroup.setValue({ limitEnabled: false, radiusKm: '' });

      expect(facade.canSubmit()).toBe(true);
    });

    it('ignores a second click while the first save is in flight', () => {
      const inFlight = new Subject<UpdateJobRadiusResponse>();
      employeeClient.updateJobRadius.mockReturnValue(inFlight);
      const facade = createFacade();
      facade.seed(employeeWithRadius(null));
      facade.formGroup.setValue({ limitEnabled: true, radiusKm: '42' });

      facade.onSubmit();
      facade.onSubmit();

      expect(employeeClient.updateJobRadius).toHaveBeenCalledTimes(1);
      expect(facade.saving()).toBe(true);

      inFlight.next(UpdateJobRadiusResponse.fromJS({ employeeId: 'emp-1' }));
      inFlight.complete();

      expect(facade.saving()).toBe(false);
    });
  });
});
