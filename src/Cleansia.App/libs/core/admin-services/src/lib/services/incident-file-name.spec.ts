import { incidentFileName } from './incident-file-name';

describe('incidentFileName', () => {
  it('prints the UTC day, the shape the server names the file by', () => {
    expect(incidentFileName('user-1', new Date('2026-09-14T23:30:00Z'))).toBe(
      'incident-user-1-20260914.pdf'
    );
  });
});
