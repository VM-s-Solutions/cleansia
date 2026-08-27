import {
  EmployeeEntityType,
  EmployeeItem,
  UpdateEmployeeCommand,
} from '@cleansia/partner-services';
import { ProfileFormData, ProfileFormFactory } from './profile.models';

describe('ProfileFormFactory', () => {
  const completeFormValue: ProfileFormData & { consent: boolean } = {
    employeeId: 'emp-1',
    firstName: 'Jana',
    lastName: 'Novakova',
    phone: '+420111222333',
    dateOfBirth: new Date('1990-04-17'),
    street: 'Vodickova 12',
    city: 'Praha',
    zipCode: '11000',
    countryId: 'cz',
    nationalityId: 'cz',
    passportId: 'AB1234567',
    entityType: EmployeeEntityType.NaturalPerson,
    registrationNumber: '12345678',
    emergencyName: 'Petr Novak',
    emergencyPhone: '+420333222111',
    consent: true,
  };

  it('builds a profile form with no payout control — payout details have their own contract', () => {
    const form = ProfileFormFactory.createEmployeeProfileForm();

    expect(form.get('iban')).toBeNull();
    expect(Object.keys(form.controls)).not.toContain('iban');
  });

  it('is valid once the profile fields are filled, with nothing payout-shaped left to satisfy', () => {
    const form = ProfileFormFactory.createEmployeeProfileForm();

    form.patchValue(completeFormValue);

    expect(form.valid).toBe(true);
  });

  it('maps the employee payload onto the form data', () => {
    const employee = EmployeeItem.fromJS({
      id: 'emp-1',
      firstName: 'Jana',
      lastName: 'Novakova',
      phoneNumber: '+420111222333',
      street: 'Vodickova 12',
      city: 'Praha',
      zipCode: '11000',
      countryId: 'cz',
      nationalityId: 'cz',
      passportId: 'AB1234567',
      entityType: EmployeeEntityType.NaturalPerson,
      registrationNumber: '12345678',
      emergencyContactName: 'Petr Novak',
      emergencyContactPhone: '+420333222111',
    });

    const formData = ProfileFormFactory.mapEmployeeToFormData(employee);

    expect(formData.employeeId).toBe('emp-1');
    expect(formData.firstName).toBe('Jana');
    expect(formData.zipCode).toBe('11000');
    expect(formData.emergencyName).toBe('Petr Novak');
    expect(formData).not.toHaveProperty('iban');
  });

  it('builds an update command that carries no payout identifier', () => {
    const command = ProfileFormFactory.createUpdateCommand(
      completeFormValue,
      []
    );

    expect(command).toBeInstanceOf(UpdateEmployeeCommand);
    expect(command.firstName).toBe('Jana');
    expect(command.registrationNumber).toBe('12345678');
    expect('iban' in command.toJSON()).toBe(false);
    expect(command.toJSON()).not.toHaveProperty('iban');
  });

  // Every member of a generated command is optional, so a dropped assignment type-checks.
  // These pin the serialized body instead (ADR-0031).
  describe('command bodies on the wire', () => {
    it('serializes every profile field the form collects', () => {
      const command = ProfileFormFactory.createUpdateCommand(
        completeFormValue,
        []
      );

      expect(command.toJSON()).toEqual({
        employeeId: 'emp-1',
        firstName: 'Jana',
        lastName: 'Novakova',
        phone: '+420111222333',
        birthDate: '1990-04-17',
        street: 'Vodickova 12',
        city: 'Praha',
        zipCode: '11000',
        countryId: 'cz',
        nationalityId: 'cz',
        passportId: 'AB1234567',
        entityType: EmployeeEntityType.NaturalPerson,
        registrationNumber: '12345678',
        emergencyName: 'Petr Novak',
        emergencyPhone: '+420333222111',
        documents: [],
        consent: true,
      });
    });

    /// This form does not edit availability and must not appear to. The module that pretended to was
    /// dead UI and is gone; the real editor is the dedicated UpdateAvailability endpoint.
    ///
    /// The assertion is ABSENCE, not emptiness, and the difference is a cleaner's working week:
    /// UpdateEmployee.cs treats a null availability as "unchanged" and an empty map as "clear it".
    /// If this ever starts sending `{}`, every profile save silently wipes the schedule.
    it('does not send availability at all, so the server leaves the schedule alone', () => {
      const command = ProfileFormFactory.createUpdateCommand(
        {
          ...completeFormValue,
          availability: {
            Monday: [{ start: '08:00', end: '12:00' }],
          },
        } as never,
        []
      );

      expect(command.availability).toBeUndefined();
      expect('availability' in command.toJSON()).toBe(false);
    });
  });
});
