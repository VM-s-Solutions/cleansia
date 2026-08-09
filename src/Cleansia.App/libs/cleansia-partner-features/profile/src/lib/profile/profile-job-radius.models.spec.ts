import { FormBuilder } from '@angular/forms';
import {
  EmployeeItem,
  UpdateJobRadiusCommand,
} from '@cleansia/partner-services';
import {
  JOB_RADIUS_MAX_KM,
  JOB_RADIUS_MIN_KM,
  JOB_RADIUS_STARTING_KM,
  JobRadiusFormValue,
  canSubmitJobRadius,
  createJobRadiusForm,
  createUpdateJobRadiusCommand,
  resolveJobRadiusKm,
  toJobRadiusFormValue,
} from './profile-job-radius.models';

describe('job radius models', () => {
  const value = (partial: Partial<JobRadiusFormValue>): JobRadiusFormValue => ({
    limitEnabled: true,
    radiusKm: '25',
    ...partial,
  });

  describe('bounds', () => {
    it('mirrors JobProximity — 1 and 500, not 1 and 100', () => {
      expect(JOB_RADIUS_MIN_KM).toBe(1);
      expect(JOB_RADIUS_MAX_KM).toBe(500);
    });

    it('starts a never-set cleaner at a normal commute rather than at a bound', () => {
      expect(JOB_RADIUS_STARTING_KM).toBe(25);
      expect(JOB_RADIUS_STARTING_KM).toBeGreaterThan(JOB_RADIUS_MIN_KM);
      expect(JOB_RADIUS_STARTING_KM).toBeLessThan(JOB_RADIUS_MAX_KM);
    });
  });

  describe('toJobRadiusFormValue', () => {
    it('renders a stored radius with the limit on', () => {
      expect(toJobRadiusFormValue(120)).toEqual({
        limitEnabled: true,
        radiusKm: '120',
      });
    });

    it('reads an unset radius as the country-wide choice, not as zero', () => {
      expect(toJobRadiusFormValue(undefined)).toEqual({
        limitEnabled: false,
        radiusKm: String(JOB_RADIUS_STARTING_KM),
      });
    });

    it('reads a null off the wire the same way — the generated member is typed number|undefined but decodes null', () => {
      const employee = EmployeeItem.fromJS({ id: 'emp-1', jobRadiusKm: null });

      expect(toJobRadiusFormValue(employee.jobRadiusKm)).toEqual({
        limitEnabled: false,
        radiusKm: String(JOB_RADIUS_STARTING_KM),
      });
    });

    it('keeps a stored value the slider bounds would have clamped, so nothing is silently rewritten', () => {
      expect(toJobRadiusFormValue(900).radiusKm).toBe('900');
    });
  });

  describe('canSubmitJobRadius', () => {
    it('always accepts the country-wide choice, whatever the distance box holds', () => {
      expect(
        canSubmitJobRadius(value({ limitEnabled: false, radiusKm: '' }))
      ).toBe(true);
      expect(
        canSubmitJobRadius(value({ limitEnabled: false, radiusKm: 'abc' }))
      ).toBe(true);
    });

    it('accepts both bounds', () => {
      expect(canSubmitJobRadius(value({ radiusKm: '1' }))).toBe(true);
      expect(canSubmitJobRadius(value({ radiusKm: '500' }))).toBe(true);
    });

    it('refuses a zero — the server calls it out of range and it would match nothing', () => {
      expect(canSubmitJobRadius(value({ radiusKm: '0' }))).toBe(false);
    });

    it('refuses anything past the bounds', () => {
      expect(canSubmitJobRadius(value({ radiusKm: '501' }))).toBe(false);
      expect(canSubmitJobRadius(value({ radiusKm: '-5' }))).toBe(false);
    });

    it('refuses what is not a whole number of kilometres', () => {
      expect(canSubmitJobRadius(value({ radiusKm: '' }))).toBe(false);
      expect(canSubmitJobRadius(value({ radiusKm: '  ' }))).toBe(false);
      expect(canSubmitJobRadius(value({ radiusKm: '25.5' }))).toBe(false);
      expect(canSubmitJobRadius(value({ radiusKm: '2e2' }))).toBe(false);
      expect(canSubmitJobRadius(value({ radiusKm: 'abc' }))).toBe(false);
    });
  });

  describe('resolveJobRadiusKm', () => {
    it('is undefined for the country-wide choice — never 0, which the server refuses', () => {
      const resolved = resolveJobRadiusKm(value({ limitEnabled: false }));

      expect(resolved).toBeUndefined();
      expect(resolved).not.toBe(0);
    });

    it('carries the chosen distance', () => {
      expect(resolveJobRadiusKm(value({ radiusKm: '80' }))).toBe(80);
    });

    it('never turns unparseable input into a number', () => {
      expect(resolveJobRadiusKm(value({ radiusKm: 'abc' }))).toBeUndefined();
      expect(resolveJobRadiusKm(value({ radiusKm: '' }))).toBeUndefined();
    });
  });

  describe('createUpdateJobRadiusCommand', () => {
    it('sends the chosen distance', () => {
      const command = createUpdateJobRadiusCommand(
        'emp-1',
        value({ radiusKm: '42' })
      );

      expect(command).toBeInstanceOf(UpdateJobRadiusCommand);
      expect(command.toJSON()).toEqual({ employeeId: 'emp-1', radiusKm: 42 });
    });

    it('clears the radius without ever sending a zero', () => {
      const command = createUpdateJobRadiusCommand(
        'emp-1',
        value({ limitEnabled: false, radiusKm: '25' })
      );

      const body = command.toJSON();
      expect(body.employeeId).toBe('emp-1');
      expect(body.radiusKm).toBeUndefined();
      expect(body.radiusKm).not.toBe(0);
    });
  });

  describe('createJobRadiusForm', () => {
    it('opens on the country-wide choice with a starting distance ready', () => {
      const form = createJobRadiusForm(new FormBuilder().nonNullable);

      expect(form.getRawValue()).toEqual({
        limitEnabled: false,
        radiusKm: String(JOB_RADIUS_STARTING_KM),
      });
    });
  });
});
