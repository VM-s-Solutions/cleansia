import { FormControl, FormGroup } from '@angular/forms';
import {
  PRICE_BLOCK_HALF_FILLED_ERROR,
  collectFilledPriceBlocks,
  priceBlockCompleteValidator,
  toPriceNumber,
} from './membership-plan-form.models';

function block(price: number | string | null, stripePriceId: string) {
  return new FormGroup(
    {
      price: new FormControl<number | string | null>(price),
      stripePriceId: new FormControl<string>(stripePriceId),
    },
    { validators: priceBlockCompleteValidator() }
  );
}

describe('toPriceNumber', () => {
  it('treats null and the empty string the text input hands back as blank', () => {
    expect(toPriceNumber(null)).toBeNull();
    expect(toPriceNumber('')).toBeNull();
    expect(toPriceNumber('   ')).toBeNull();
  });

  it('parses a numeric string and passes a number through', () => {
    expect(toPriceNumber('199')).toBe(199);
    expect(toPriceNumber('2030.5')).toBe(2030.5);
    expect(toPriceNumber(0)).toBe(0);
  });

  it('refuses a string that is not a number', () => {
    expect(toPriceNumber('abc')).toBeNull();
  });
});

describe('priceBlockCompleteValidator', () => {
  it('accepts a block with neither value', () => {
    expect(block(null, '').errors).toBeNull();
    expect(block('', '   ').errors).toBeNull();
  });

  it('accepts a block with both values, including a zero price', () => {
    expect(block(199, 'price_1').errors).toBeNull();
    expect(block(0, 'price_1').errors).toBeNull();
    expect(block('199', 'price_1').errors).toBeNull();
  });

  it('flags a price without a Stripe id', () => {
    expect(block(199, '').errors).toEqual({
      [PRICE_BLOCK_HALF_FILLED_ERROR]: true,
    });
  });

  it('flags a Stripe id without a price', () => {
    expect(block(null, 'price_1').errors).toEqual({
      [PRICE_BLOCK_HALF_FILLED_ERROR]: true,
    });
    expect(block('', 'price_1').errors).toEqual({
      [PRICE_BLOCK_HALF_FILLED_ERROR]: true,
    });
  });
});

describe('collectFilledPriceBlocks', () => {
  it('keeps only the blocks with both a price and a Stripe id', () => {
    expect(
      collectFilledPriceBlocks({
        CZK: { price: '199', stripePriceId: ' price_czk ' },
        EUR: { price: '9.9', stripePriceId: '' },
        PLN: { price: '', stripePriceId: 'price_pln' },
        USD: { price: null, stripePriceId: '' },
      })
    ).toEqual({ CZK: { price: 199, stripePriceId: 'price_czk' } });
  });

  it('keeps a zero price — zero is a price, blank is not', () => {
    expect(
      collectFilledPriceBlocks({ CZK: { price: 0, stripePriceId: 'price_0' } })
    ).toEqual({ CZK: { price: 0, stripePriceId: 'price_0' } });
  });

  it('returns an empty map when nothing is filled', () => {
    expect(
      collectFilledPriceBlocks({ CZK: { price: null, stripePriceId: '' } })
    ).toEqual({});
  });
});
