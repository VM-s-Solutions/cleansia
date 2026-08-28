import { DocumentType } from '@cleansia/admin-services';

/**
 * The translation key each document type reads as.
 *
 * The same eleven keys the employee-detail screen already uses, so the two screens name a type
 * identically — a requirement called "Work permit" in one place and something else in the other
 * would read as two different rules.
 *
 * A map rather than a switch: this file also drives the type picker's options, and enumerating a
 * switch is not something a template can do.
 */
export const DOCUMENT_TYPE_LABEL_KEYS: Readonly<Record<DocumentType, string>> = {
  [DocumentType.IdentityCard]: 'pages.employee_detail.document_types.identity_card',
  [DocumentType.Passport]: 'pages.employee_detail.document_types.passport',
  [DocumentType.DriversLicense]: 'pages.employee_detail.document_types.drivers_license',
  [DocumentType.WorkPermit]: 'pages.employee_detail.document_types.work_permit',
  [DocumentType.Contract]: 'pages.employee_detail.document_types.contract',
  [DocumentType.Certificate]: 'pages.employee_detail.document_types.certificate',
  [DocumentType.BankStatement]: 'pages.employee_detail.document_types.bank_statement',
  [DocumentType.TaxDocument]: 'pages.employee_detail.document_types.tax_document',
  [DocumentType.InsuranceDocument]: 'pages.employee_detail.document_types.insurance_document',
  [DocumentType.Other]: 'pages.employee_detail.document_types.other',
};
