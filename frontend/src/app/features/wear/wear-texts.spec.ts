import { describe, expect, it } from 'vitest';
import { WearAttribute, WearBreakdownRow } from '@shared/types';
import {
  WearOrderRow,
  addOrder,
  allowedOptions,
  attributeChoices,
  breakdownRowText,
  byKind,
  fixChoices,
  handoutItemText,
  miniListName,
  miniListQuery,
  orderName,
  orderRowText,
  priceOptionText,
  sizeName,
} from './wear-texts';

const attribute = (id: number, kind: WearAttribute['kind'], nameDanish: string, position = 1): WearAttribute => ({
  id,
  kind,
  nameDanish,
  nameEnglish: '',
  position,
});

const herre = attribute(101, 'Model', 'Herre', 1);
const dame = attribute(102, 'Model', 'Dame', 2);
const sort = attribute(111, 'Color', 'Sort', 1);
const hvid = attribute(112, 'Color', 'Hvid', 2);
const s = attribute(12, 'Size', 'S', 12);
const m = attribute(13, 'Size', 'M', 13);
const l = attribute(14, 'Size', 'L', 14);

describe('order texts (DeltagereWear::getWearName)', () => {
  it("adds the attributes that aren't a size in the order model, design, color", () => {
    expect(orderName('T-shirt', [sort, m, attribute(5, 'Design', 'Logo'), herre])).toBe('T-shirt-Herre-Logo-Sort');
    expect(orderName('Kasket', [])).toBe('Kasket');
    expect(sizeName([sort, m])).toBe('M');
    expect(sizeName([sort])).toBeNull();
  });

  it('groups attributes by kind in that order, each by position', () => {
    expect(byKind([m, sort, dame, herre, s]).map(g => [g.kind, g.attributes.map(a => a.nameDanish)])).toEqual([
      ['Model', ['Herre', 'Dame']],
      ['Color', ['Sort']],
      ['Size', ['S', 'M']],
    ]);
  });
});

describe('Bestillings-oversigt texts', () => {
  const row: WearBreakdownRow = {
    wearTypeId: 1,
    wearTypeName: 'T-shirt',
    attributes: [herre, sort],
    startsVariant: true,
    total: 3,
    noSize: 0,
    sizeCounts: [{ sizeId: 13, count: 3 }],
  };

  it('names a row "(id) wear - attribute - ..." (wearbreakdown.phtml)', () => {
    expect(breakdownRowText(row)).toBe('(1) T-shirt - Herre - Sort');
    expect(breakdownRowText({ ...row, attributes: [] })).toBe('(1) T-shirt');
    expect(miniListName('T-shirt', [herre, sort, m])).toBe('T-shirt - Herre - Sort - M');
  });

  it("links a number to the list with the row's attributes and the size column", () => {
    expect(miniListQuery(row, 13)).toEqual({ attributes: '101,111', size: '13' });
    expect(miniListQuery({ ...row, attributes: [] }, 'none')).toEqual({ size: 'none' });
  });
});

describe('Wear-udlevering texts', () => {
  it('shows "{wear}: {amount} stk." and the size', () => {
    const words = { pieces: 'stk.', inSize: 'i str.' };
    expect(handoutItemText({ name: 'T-shirt-Sort', amount: 2, size: 'M' }, words)).toBe(
      'T-shirt-Sort: 2 stk. i str. M',
    );
    expect(handoutItemText({ name: 'Kasket', amount: 1, size: null }, words)).toBe('Kasket: 1 stk.');
  });
});

describe('the participant edit page (imwedit.js setupWear)', () => {
  // Variant 0: Herre/Dame, Sort, S/M. Variant 1: Herre, Hvid, L.
  const variants = [
    [herre, dame, sort, s, m],
    [herre, hvid, l],
  ];
  const choices = attributeChoices(variants);

  it('has a select per kind, each with the attributes in the order they appear', () => {
    expect(choices.map(c => [c.kind, c.options.map(o => [o.attribute.nameDanish, o.variants])])).toEqual([
      [
        'Model',
        [
          ['Herre', [0, 1]],
          ['Dame', [0]],
        ],
      ],
      [
        'Color',
        [
          ['Sort', [0]],
          ['Hvid', [1]],
        ],
      ],
      [
        'Size',
        [
          ['S', [0]],
          ['M', [0]],
          ['L', [1]],
        ],
      ],
    ]);
  });

  it('only offers the attributes that share a variant with the choices before', () => {
    expect(
      allowedOptions(
        choices,
        [dame, null, null].map(a => a?.id ?? null),
        1,
      ).map(o => o.attribute.id),
    ).toEqual([sort.id]);
    expect(allowedOptions(choices, [herre.id, hvid.id, null], 2).map(o => o.attribute.id)).toEqual([l.id]);
  });

  it('starts each select on its first option and fixes a choice that no longer fits', () => {
    expect(fixChoices(choices, [])).toEqual([herre.id, sort.id, s.id]);
    expect(fixChoices(choices, [herre.id, hvid.id, s.id])).toEqual([herre.id, hvid.id, l.id]);
    expect(fixChoices(choices, [dame.id, hvid.id, m.id])).toEqual([dame.id, sort.id, m.id]);
  });

  const row: WearOrderRow = {
    id: null,
    wearPriceId: 1,
    attributeIds: [herre.id, sort.id, m.id],
    name: 'T-shirt-Herre-Sort',
    size: 'M',
    categoryName: 'Deltager',
    unitPrice: 150,
    amount: 2,
    isReceived: false,
  };

  it('adds the same wear, price and attributes to the listed row, and ignores an amount below 1', () => {
    const kept = { ...row, id: 7, amount: 1, isReceived: true, attributeIds: [m.id, sort.id, herre.id] };

    expect(addOrder([kept], row)).toEqual([{ ...kept, amount: 3 }]);
    expect(addOrder([kept], { ...row, wearPriceId: 2 })).toEqual([kept, { ...row, wearPriceId: 2 }]);
    expect(addOrder([kept], { ...row, attributeIds: [herre.id, sort.id, s.id] })).toHaveLength(2);
    expect(addOrder([kept], { ...row, amount: 0 })).toEqual([kept]);
    expect(addOrder([kept], { ...row, amount: -1 })).toEqual([kept]);
  });

  it('shows a row as "{amount} stk. {wear} str. {size} — ({category} - {price})"', () => {
    const words = { pieces: 'stk.', size: 'str.' };
    expect(orderRowText(row, words)).toBe('2 stk. T-shirt-Herre-Sort str. M — (Deltager - 150 DKK)');
    expect(orderRowText({ ...row, size: null, name: 'Kasket' }, words)).toBe('2 stk. Kasket — (Deltager - 150 DKK)');
    expect(priceOptionText({ id: 1, categoryName: 'Arrangør', price: 100 })).toBe('Arrangør - 100 DKK');
  });
});
