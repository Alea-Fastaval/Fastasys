// The cut-out of the photo upload page (Infosys photo-cropper.js with cropit: 213 x 295 pixels, the image fills the
// frame, zoom from "fill" to the image's own size, rotation in quarter turns)

export const PHOTO_WIDTH = 213;
export const PHOTO_HEIGHT = 295;

export type QuarterTurn = 0 | 90 | 180 | 270;

// The image's place in the frame: turned, scaled, and with its (turned) top left corner at x, y
export interface Crop {
  rotation: QuarterTurn;
  scale: number;
  x: number;
  y: number;
}

export interface Size {
  width: number;
  height: number;
}

// The image's size after the quarter turns
export function turnedSize(image: Size, rotation: QuarterTurn): Size {
  return rotation % 180 === 0 ? image : { width: image.height, height: image.width };
}

// cropit's minZoom "fill": the smallest scale at which the image covers the frame. Its maxZoom 1: the image's own
// size, or the fill scale for an image smaller than the frame.
export function zoomRange(image: Size, rotation: QuarterTurn): { min: number; max: number } {
  const turned = turnedSize(image, rotation);
  const min = Math.max(PHOTO_WIDTH / turned.width, PHOTO_HEIGHT / turned.height);
  return { min, max: Math.max(min, 1) };
}

// Keeps the image covering the frame: no gap at any edge
export function clampCrop(crop: Crop, image: Size): Crop {
  const turned = turnedSize(image, crop.rotation);
  const width = turned.width * crop.scale;
  const height = turned.height * crop.scale;
  return {
    ...crop,
    x: Math.min(0, Math.max(PHOTO_WIDTH - width, crop.x)),
    y: Math.min(0, Math.max(PHOTO_HEIGHT - height, crop.y)),
  };
}

// A newly chosen (or turned) image: filling the frame, centred
export function initialCrop(image: Size, rotation: QuarterTurn = 0): Crop {
  const turned = turnedSize(image, rotation);
  const scale = zoomRange(image, rotation).min;
  return {
    rotation,
    scale,
    x: (PHOTO_WIDTH - turned.width * scale) / 2,
    y: (PHOTO_HEIGHT - turned.height * scale) / 2,
  };
}

// A new zoom that keeps the point in the frame's centre where it is
export function zoomTo(crop: Crop, scale: number, image: Size): Crop {
  const { min, max } = zoomRange(image, crop.rotation);
  const next = Math.min(max, Math.max(min, scale));
  const centreX = (PHOTO_WIDTH / 2 - crop.x) / crop.scale;
  const centreY = (PHOTO_HEIGHT / 2 - crop.y) / crop.scale;
  return clampCrop(
    { ...crop, scale: next, x: PHOTO_WIDTH / 2 - centreX * next, y: PHOTO_HEIGHT / 2 - centreY * next },
    image,
  );
}

// cropit's rotateCW / rotateCCW: a quarter turn, filling the frame again
export function turn(crop: Crop, image: Size, clockwise: boolean): Crop {
  const rotation = ((crop.rotation + (clockwise ? 90 : 270)) % 360) as QuarterTurn;
  return initialCrop(image, rotation);
}

// Dragging the image by dx, dy
export function moveCrop(crop: Crop, dx: number, dy: number, image: Size): Crop {
  return clampCrop({ ...crop, x: crop.x + dx, y: crop.y + dy }, image);
}

// Draws the cut-out on a canvas of PHOTO_WIDTH x PHOTO_HEIGHT
export function drawCrop(context: CanvasRenderingContext2D, image: CanvasImageSource & Size, crop: Crop): void {
  const turned = turnedSize(image, crop.rotation);
  context.clearRect(0, 0, PHOTO_WIDTH, PHOTO_HEIGHT);
  context.save();
  context.translate(crop.x + (turned.width * crop.scale) / 2, crop.y + (turned.height * crop.scale) / 2);
  context.rotate((crop.rotation * Math.PI) / 180);
  const width = image.width * crop.scale;
  const height = image.height * crop.scale;
  context.drawImage(image, -width / 2, -height / 2, width, height);
  context.restore();
}
