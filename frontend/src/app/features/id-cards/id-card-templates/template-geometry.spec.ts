import { describe, it, expect } from 'vitest';
import { IdCardTemplateItem } from '@shared/types';
import {
  applyManualPosition,
  constrainItem,
  itemTransform,
  moveItem,
  newItem,
  normalizeRotation,
  renderSize,
} from './template-geometry';

const card = { width: 1000, height: 600 };
const item = (overrides: Partial<IdCardTemplateItem> = {}): IdCardTemplateItem => ({
  type: 'Photo',
  x: 100,
  y: 100,
  width: 200,
  height: 100,
  rotation: 0,
  dataSource: null,
  ...overrides,
});

describe('moveItem (Infosys moveElement)', () => {
  it('moves the item', () => {
    expect(moveItem(item(), 50, -20, card)).toMatchObject({ x: 150, y: 80 });
  });

  it('keeps it on the card', () => {
    expect(moveItem(item(), -500, -500, card)).toMatchObject({ x: 0, y: 0 });
    expect(moveItem(item(), 2000, 2000, card)).toMatchObject({ x: 800, y: 500, width: 200, height: 100 });
  });

  it('shrinks an item larger than the card', () => {
    expect(moveItem(item({ width: 1200 }), 0, 0, card)).toMatchObject({ x: 0, width: 1000 });
  });
});

describe('constrainItem (Infosys constrainTemplateElements, after a new background)', () => {
  it('moves an item that sticks out back onto the card', () => {
    expect(constrainItem(item({ x: 900, y: 550 }), card)).toMatchObject({ x: 800, y: 500, width: 200, height: 100 });
  });

  it('leaves an item on the card alone', () => {
    expect(constrainItem(item(), card)).toEqual(item());
  });
});

describe('normalizeRotation (Infosys manualPositionHandler)', () => {
  it('wraps 360 and more, and adds 360 to a negative rotation once', () => {
    expect(normalizeRotation(370)).toBe(10);
    expect(normalizeRotation(360)).toBe(0);
    expect(normalizeRotation(-90)).toBe(270);
    expect(normalizeRotation(45)).toBe(45);
  });
});

describe('applyManualPosition', () => {
  it('moves through moveItem and sets the size and rotation as typed', () => {
    expect(applyManualPosition(item(), { x: 950, y: 10, width: 300, height: 50, rotation: -90 }, card)).toMatchObject({
      x: 800,
      y: 10,
      width: 300,
      height: 50,
      rotation: 270,
    });
  });
});

describe('newItem (Infosys addPhotoBlock, addBarcodeBlock, addTextBlock)', () => {
  it('has Infosys sizes in the top left corner, and a text shows the ID', () => {
    expect(newItem('Photo')).toEqual({
      type: 'Photo',
      x: 0,
      y: 0,
      width: 213,
      height: 295,
      rotation: 0,
      dataSource: null,
    });
    expect(newItem('Barcode')).toMatchObject({ width: 200, height: 100, dataSource: null });
    expect(newItem('Text')).toMatchObject({ width: 200, height: 50, dataSource: 'Id' });
  });
});

describe('renderSize (Infosys updateSizeIndicator)', () => {
  it('gives whole centimetres and millimetres at 300 DPI', () => {
    // 1011 px = 8.56 cm = 85.6 mm
    expect(renderSize({ width: 1011, height: 638 })).toEqual({ widthCm: 9, heightCm: 5, widthMm: 86, heightMm: 54 });
  });
});

describe('itemTransform (the preview turns an item around its centre)', () => {
  it('is null without a rotation', () => {
    expect(itemTransform(item())).toBeNull();
  });

  it('rotates around the centre', () => {
    expect(itemTransform(item({ rotation: 90 }))).toBe('rotate(90 200 150)');
  });
});
