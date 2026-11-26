import {
  WEAR_ATTRIBUTE_KINDS,
  WearAttribute,
  WearAttributeKind,
  WearBreakdownRow,
  WearHandoutItem,
  WearPriceOption,
} from '@shared/types';
import { priceText } from '../participants/participant-price/participant-price';

// The translation key of an attribute kind's heading (Infosys shows the kind as it is stored: "model", "size", ...)
export function kindKey(kind: WearAttributeKind): string {
  return `wear.kinds.${kind}`;
}

// The attributes in Infosys's order: model, design, color, size; each kind by position
export function sortAttributes(attributes: WearAttribute[]): WearAttribute[] {
  return [...attributes].sort(
    (a, b) =>
      WEAR_ATTRIBUTE_KINDS.indexOf(a.kind) - WEAR_ATTRIBUTE_KINDS.indexOf(b.kind) ||
      a.position - b.position ||
      a.id - b.id,
  );
}

export interface KindGroup {
  kind: WearAttributeKind;
  attributes: WearAttribute[];
}

// The attributes grouped by kind, in Infosys's kind order; kinds without attributes are left out
export function byKind(attributes: WearAttribute[]): KindGroup[] {
  const sorted = sortAttributes(attributes);
  return WEAR_ATTRIBUTE_KINDS.map(kind => ({ kind, attributes: sorted.filter(a => a.kind === kind) })).filter(
    group => group.attributes.length > 0,
  );
}

// DeltagereWear::getWearName('da', false): "T-shirt-Herre-Sort", the attributes that aren't a size after the name
export function orderName(wearName: string, attributes: WearAttribute[]): string {
  return (
    wearName +
    sortAttributes(attributes)
      .filter(a => a.kind !== 'Size')
      .map(a => `-${a.nameDanish}`)
      .join('')
  );
}

// The Danish name of the size among the attributes, or null
export function sizeName(attributes: WearAttribute[]): string | null {
  return attributes.find(a => a.kind === 'Size')?.nameDanish ?? null;
}

// A row of "Bestillings-oversigt": "(1) T-shirt - Herre - Sort"
export function breakdownRowText(row: WearBreakdownRow): string {
  return `(${row.wearTypeId}) ${[row.wearTypeName, ...row.attributes.map(a => a.nameDanish)].join(' - ')}`;
}

// The heading of the list behind a number: "T-shirt - Herre - Sort - M"
export function miniListName(wearTypeName: string, attributes: WearAttribute[]): string {
  return [wearTypeName, ...attributes.map(a => a.nameDanish)].join(' - ');
}

// The query parameters of the link from a number of "Bestillings-oversigt" to the list behind it: the row's
// attributes, and the size column ("none" for "Ingen størrelse"). Infosys: ?model=1&color=2&size=3.
export function miniListQuery(row: WearBreakdownRow, size: number | 'none'): Record<string, string> {
  const query: Record<string, string> = { size: String(size) };
  if (row.attributes.length > 0) query['attributes'] = row.attributes.map(a => a.id).join(',');
  return query;
}

// A line of "Wear-udlevering" (in bold in Infosys): "T-shirt-Sort: 2 stk. i str. M"
export function handoutItemText(item: WearHandoutItem, words: { pieces: string; inSize: string }): string {
  const size = item.size ? ` ${words.inSize} ${item.size}` : '';
  return `${item.name}: ${item.amount} ${words.pieces}${size}`;
}

// A price in the select of the participant's edit page (Infosys wear/ajaxgetwear: "Deltager - 150,-"; DKK like the
// other prices)
export function priceOptionText(price: WearPriceOption): string {
  return `${price.categoryName} - ${priceText(price.price)}`;
}

// One select of the participant's edit page: a kind and its attributes, each with the variants it is in
export interface AttributeChoice {
  kind: WearAttributeKind;
  options: { attribute: WearAttribute; variants: number[] }[];
}

// imwedit.js setupWear: a select per kind any variant has, in the order model, design, color, size; its options
// are the kind's attributes in the order they first appear in the variants
export function attributeChoices(variants: WearAttribute[][]): AttributeChoice[] {
  return WEAR_ATTRIBUTE_KINDS.map(kind => {
    const options: AttributeChoice['options'] = [];
    variants.forEach((variant, index) => {
      for (const attribute of variant.filter(a => a.kind === kind)) {
        const option = options.find(o => o.attribute.id === attribute.id);
        if (option) option.variants.push(index);
        else options.push({ attribute, variants: [index] });
      }
    });
    return { kind, options };
  }).filter(choice => choice.options.length > 0);
}

// The options a select may have, given what the selects before it have chosen: those sharing a variant with all of
// them. imwedit.js hid the rest.
export function allowedOptions(
  choices: AttributeChoice[],
  chosen: (number | null)[],
  index: number,
): AttributeChoice['options'] {
  let variants: number[] | null = null;
  for (let i = 0; i < index; i++) {
    const option = choices[i].options.find(o => o.attribute.id === chosen[i]);
    if (!option) continue;
    const allowed: number[] = variants ?? option.variants;
    variants = allowed.filter(v => option.variants.includes(v));
  }
  const shared = variants;
  return shared === null
    ? choices[index].options
    : choices[index].options.filter(o => o.variants.some(v => shared.includes(v)));
}

// Keeps each select's choice when it is still allowed, and otherwise picks its first allowed option, like imwedit.js
// does after a select changes. Infosys only took the selects up to the changed one into account; here every select
// before the one being fixed counts, so the choice is always one of a variant.
export function fixChoices(choices: AttributeChoice[], chosen: (number | null)[]): (number | null)[] {
  const fixed: (number | null)[] = [];
  choices.forEach((_, index) => {
    const allowed = allowedOptions(choices, fixed, index);
    const current = chosen[index];
    fixed.push(allowed.some(o => o.attribute.id === current) ? current : (allowed[0]?.attribute.id ?? null));
  });
  return fixed;
}

// An order row on the participant's edit page; id is null for a new order
export interface WearOrderRow {
  id: number | null;
  wearPriceId: number;
  attributeIds: number[];
  name: string;
  size: string | null;
  categoryName: string;
  unitPrice: number;
  amount: number;
  isReceived: boolean;
}

// "2 stk. T-shirt-Sort str. M — (Deltager - 150 DKK)". Infosys wrote the price as "150,-".
export function orderRowText(row: WearOrderRow, words: { pieces: string; size: string }): string {
  const size = row.size ? ` ${words.size} ${row.size}` : '';
  return `${row.amount} ${words.pieces} ${row.name}${size} — (${row.categoryName} - ${priceText(row.unitPrice)})`;
}

function sameAttributes(a: number[], b: number[]): boolean {
  return a.length === b.length && a.every(id => b.includes(id));
}

// imwedit.js "Tilføj": the same wear at the same price with the same attributes adds to the row already listed,
// otherwise a new row is added. Infosys added a new row only with an amount above 0, but added any amount to a listed
// row; an amount below 1 is ignored here, as the API refuses it (fixed 2026-11-26).
export function addOrder(rows: WearOrderRow[], order: WearOrderRow): WearOrderRow[] {
  if (!Number.isInteger(order.amount) || order.amount < 1) return rows;
  const listed = rows.find(
    r => r.wearPriceId === order.wearPriceId && sameAttributes(r.attributeIds, order.attributeIds),
  );
  if (listed) return rows.map(r => (r === listed ? { ...r, amount: r.amount + order.amount } : r));
  return [...rows, order];
}
