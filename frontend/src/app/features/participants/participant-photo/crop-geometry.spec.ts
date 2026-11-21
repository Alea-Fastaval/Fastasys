import { describe, it, expect } from 'vitest';
import {
  PHOTO_HEIGHT,
  PHOTO_WIDTH,
  clampCrop,
  drawCrop,
  initialCrop,
  moveCrop,
  turn,
  turnedSize,
  zoomRange,
  zoomTo,
} from './crop-geometry';

// A landscape phone photo
const photo = { width: 4000, height: 3000 };

describe('the photo cut-out (Infosys photo-cropper.js with cropit, 213 x 295)', () => {
  it('turns the size with a quarter turn', () => {
    expect(turnedSize(photo, 90)).toEqual({ width: 3000, height: 4000 });
    expect(turnedSize(photo, 180)).toEqual(photo);
  });

  it("zooms from filling the frame to the image's own size", () => {
    const range = zoomRange(photo, 0);
    expect(range.min).toBeCloseTo(PHOTO_HEIGHT / 3000);
    expect(range.max).toBe(1);
  });

  it('cannot zoom out of a small image', () => {
    const small = { width: 100, height: 100 };
    const range = zoomRange(small, 0);
    expect(range.min).toBeCloseTo(2.95);
    expect(range.max).toBe(range.min);
  });

  it('starts filling the frame, centred', () => {
    const crop = initialCrop(photo);
    const scale = PHOTO_HEIGHT / 3000;
    expect(crop.scale).toBeCloseTo(scale);
    expect(crop.y).toBeCloseTo(0);
    expect(crop.x).toBeCloseTo((PHOTO_WIDTH - 4000 * scale) / 2);
  });

  it('keeps the image covering the frame when it is dragged', () => {
    const crop = initialCrop(photo);
    expect(moveCrop(crop, 1000, 0, photo).x).toBe(0);
    expect(moveCrop(crop, 0, -50, photo).y).toBe(0);
    const left = moveCrop(crop, -1000, 0, photo);
    expect(left.x).toBeCloseTo(PHOTO_WIDTH - 4000 * crop.scale);
  });

  it('zooms around the centre of the frame and stays within the range', () => {
    const crop = initialCrop(photo);
    const zoomed = zoomTo(crop, 0.5, photo);
    expect(zoomed.scale).toBe(0.5);
    // The frame's centre shows the same point of the image
    expect((PHOTO_WIDTH / 2 - zoomed.x) / zoomed.scale).toBeCloseTo((PHOTO_WIDTH / 2 - crop.x) / crop.scale);
    expect(zoomTo(crop, 5, photo).scale).toBe(1);
    expect(zoomTo(crop, 0.001, photo).scale).toBeCloseTo(crop.scale);
  });

  it('turns a quarter at a time and fills the frame again', () => {
    const crop = initialCrop(photo);
    const right = turn(crop, photo, true);
    expect(right.rotation).toBe(90);
    // Turned, the photo is 3000 x 4000
    expect(right.scale).toBeCloseTo(Math.max(PHOTO_WIDTH / 3000, PHOTO_HEIGHT / 4000));
    expect(turn(crop, photo, false).rotation).toBe(270);
    expect(turn(turn(right, photo, true), photo, true).rotation).toBe(270);
  });

  it('clamps a crop that leaves a gap', () => {
    const crop = { rotation: 0 as const, scale: 0.1, x: 50, y: 50 };
    expect(clampCrop(crop, photo)).toMatchObject({ x: 0, y: 0 });
  });

  it('draws the turned image around its centre', () => {
    const calls: string[] = [];
    const context = {
      clearRect: () => calls.push('clear'),
      save: () => undefined,
      restore: () => undefined,
      translate: (x: number, y: number) => calls.push(`translate ${x},${y}`),
      rotate: (angle: number) => calls.push(`rotate ${Math.round((angle * 180) / Math.PI)}`),
      drawImage: (_image: unknown, x: number, y: number, w: number, h: number) =>
        calls.push(`image ${x},${y} ${w}x${h}`),
    } as unknown as CanvasRenderingContext2D;
    const image = { ...photo } as unknown as CanvasImageSource & typeof photo;

    // Turned, the image is 300 x 400 at scale 0.1, with its top left corner in the frame's
    drawCrop(context, image, { rotation: 90, scale: 0.1, x: 0, y: 0 });

    expect(calls).toEqual(['clear', 'translate 150,200', 'rotate 90', 'image -200,-150 400x300']);
  });
});
