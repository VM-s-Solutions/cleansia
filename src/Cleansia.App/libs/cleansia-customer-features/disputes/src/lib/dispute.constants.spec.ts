import {
  DISPUTE_DESCRIPTION_MAX_LENGTH,
  DISPUTE_DESCRIPTION_MIN_LENGTH,
} from './dispute.constants';

describe('dispute.constants', () => {
  it('pins the description length contract shared with the backend DisputeLimits', () => {
    expect(DISPUTE_DESCRIPTION_MIN_LENGTH).toBe(10);
    expect(DISPUTE_DESCRIPTION_MAX_LENGTH).toBe(2000);
  });

  it('keeps min strictly below max', () => {
    expect(DISPUTE_DESCRIPTION_MIN_LENGTH).toBeLessThan(DISPUTE_DESCRIPTION_MAX_LENGTH);
  });
});
