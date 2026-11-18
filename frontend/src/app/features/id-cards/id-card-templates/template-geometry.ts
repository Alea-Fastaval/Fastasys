import { IdCardDataSource, IdCardItemType, IdCardTemplateItem } from '@shared/types';

// The rules of Infosys's template editor (public/js/idtemplate.js) that move and size items

export interface CardSize {
  width: number;
  height: number;
}

// Infosys moveElement: moves the item and keeps it on the card; an item wider or taller than the card is shrunk to fit
export function moveItem(item: IdCardTemplateItem, dx: number, dy: number, card: CardSize): IdCardTemplateItem {
  let { x, y, width, height } = item;
  x = Math.max(0, x + dx);
  y = Math.max(0, y + dy);

  if (x + width > card.width) {
    x = card.width - width;
    if (x < 0) {
      width += x;
      x = 0;
    }
  }

  if (y + height > card.height) {
    y = card.height - height;
    if (y < 0) {
      height += y;
      y = 0;
    }
  }

  return { ...item, x, y, width, height };
}

// Infosys constrainTemplateElements, after a new background: items that stick out are moved back onto the card
export function constrainItem(item: IdCardTemplateItem, card: CardSize): IdCardTemplateItem {
  let { x, y, width, height } = item;
  if (width + x > card.width) x -= width + x - card.width;
  if (x < 0) {
    width += x;
    x = 0;
  }
  if (height + y > card.height) y -= height + y - card.height;
  if (y < 0) {
    height += y;
    y = 0;
  }
  return { ...item, x, y, width, height };
}

// Infosys manualPositionHandler: a rotation of 360 or more wraps around, a negative one gets 360 added
export function normalizeRotation(rotation: number): number {
  if (rotation >= 360) return rotation % 360;
  if (rotation < 0) return rotation + 360;
  return rotation;
}

// The typed position, size and rotation of the selected item (Infosys manualPositionHandler): the position goes
// through moveItem, so the item stays on the card
export function applyManualPosition(
  item: IdCardTemplateItem,
  typed: { x: number; y: number; width: number; height: number; rotation: number },
  card: CardSize,
): IdCardTemplateItem {
  const moved = moveItem(item, typed.x - item.x, typed.y - item.y, card);
  return { ...moved, width: typed.width, height: typed.height, rotation: normalizeRotation(typed.rotation) };
}

// Infosys addPhotoBlock, addBarcodeBlock and addTextBlock: a new item in the top left corner. A text item shows the
// participant's ID until another data source is chosen.
export function newItem(type: IdCardItemType): IdCardTemplateItem {
  const sizes: Record<IdCardItemType, [number, number]> = { Photo: [213, 295], Barcode: [200, 100], Text: [200, 50] };
  const [width, height] = sizes[type];
  const dataSource: IdCardDataSource | null = type === 'Text' ? 'Id' : null;
  return { type, x: 0, y: 0, width, height, rotation: 0, dataSource };
}

// Infosys updateSizeIndicator: "Render size (at 300 DPI)" in whole centimetres and millimetres. Infosys's
// Math.round(x, 1) meant one decimal, but JavaScript's Math.round has no second argument, so it rounds to whole numbers.
export function renderSize(card: CardSize): { widthCm: number; heightCm: number; widthMm: number; heightMm: number } {
  return {
    widthCm: Math.round((card.width / 300) * 2.54),
    heightCm: Math.round((card.height / 300) * 2.54),
    widthMm: Math.round((card.width / 30) * 2.54),
    heightMm: Math.round((card.height / 30) * 2.54),
  };
}

// The SVG transform that turns the item around its centre, like Infosys's preview (makeRotationPoint)
export function itemTransform(item: IdCardTemplateItem): string | null {
  if (!item.rotation) return null;
  return `rotate(${item.rotation} ${item.x + item.width / 2} ${item.y + item.height / 2})`;
}
