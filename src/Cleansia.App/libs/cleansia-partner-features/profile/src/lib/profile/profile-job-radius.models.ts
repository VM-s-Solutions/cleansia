import { FormControl, FormGroup, NonNullableFormBuilder } from '@angular/forms';
import { UpdateJobRadiusCommand } from '@cleansia/partner-services';

/**
 * Mirrors `Cleansia.Core.Domain.Orders.JobProximity`; outside this range the server refuses the
 * save with `employee.job_radius_out_of_range`.
 */
export const JOB_RADIUS_MIN_KM = 1;
export const JOB_RADIUS_MAX_KM = 500;

export const JOB_RADIUS_STARTING_KM = 25;

export interface JobRadiusFormValue {
  limitEnabled: boolean;
  radiusKm: string;
}

export type JobRadiusFormGroup = FormGroup<{
  limitEnabled: FormControl<boolean>;
  radiusKm: FormControl<string>;
}>;

export function createJobRadiusForm(
  fb: NonNullableFormBuilder
): JobRadiusFormGroup {
  return fb.group({
    limitEnabled: fb.control(false),
    radiusKm: fb.control(String(JOB_RADIUS_STARTING_KM)),
  });
}

/**
 * An absent radius is the country-wide board, which is a standing choice rather than an unset
 * field — so it turns the limit off and leaves a distance ready for the cleaner who changes
 * their mind, instead of showing a zero.
 */
export function toJobRadiusFormValue(
  radiusKm: number | undefined
): JobRadiusFormValue {
  const stored = radiusKm ?? undefined;

  return {
    limitEnabled: stored !== undefined,
    radiusKm: String(stored ?? JOB_RADIUS_STARTING_KM),
  };
}

export function canSubmitJobRadius(value: JobRadiusFormValue): boolean {
  if (!value.limitEnabled) {
    return true;
  }

  const parsed = parseRadiusKm(value.radiusKm);

  return (
    parsed !== undefined &&
    parsed >= JOB_RADIUS_MIN_KM &&
    parsed <= JOB_RADIUS_MAX_KM
  );
}

/** `undefined` is "give me the country-wide board back". A `0` would be refused as out of range. */
export function resolveJobRadiusKm(
  value: JobRadiusFormValue
): number | undefined {
  return value.limitEnabled ? parseRadiusKm(value.radiusKm) : undefined;
}

export function createUpdateJobRadiusCommand(
  employeeId: string,
  value: JobRadiusFormValue
): UpdateJobRadiusCommand {
  const command = new UpdateJobRadiusCommand();
  command.employeeId = employeeId;
  command.radiusKm = resolveJobRadiusKm(value);

  return command;
}

function parseRadiusKm(raw: string): number | undefined {
  const trimmed = raw.trim();

  return /^\d+$/.test(trimmed) ? Number(trimmed) : undefined;
}
