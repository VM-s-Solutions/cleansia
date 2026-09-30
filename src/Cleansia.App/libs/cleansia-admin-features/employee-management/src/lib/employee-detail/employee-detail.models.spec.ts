import { RATE_TEMPLATES } from './employee-detail.models';

describe('RATE_TEMPLATES', () => {
  it('offers the three templates the server accepts, each at its share of the list price', () => {
    expect(RATE_TEMPLATES.map(({ value, multiplier }) => [value, multiplier])).toEqual([
      ['standard', 0.5],
      ['experienced', 0.6],
      ['expert', 0.7],
    ]);
  });

  it('labels each template by its own name', () => {
    expect(RATE_TEMPLATES.map(({ labelKey }) => labelKey)).toEqual([
      'pages.employee_detail.grade_standard',
      'pages.employee_detail.grade_experienced',
      'pages.employee_detail.grade_expert',
    ]);
  });
});
