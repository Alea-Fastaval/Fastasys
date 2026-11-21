import { IdCard, IdCardTemplate, IdCardTemplateItem } from '@shared/types';

// Drawing an ID card in the browser (Infosys drew it on the server: IdTemplateRenderer, agreed 2026-11-18). The card is
// the template's background at its own size in pixels, with the template's items on it.

// Infosys IdTemplateRenderer::FONT_SIZE and its line height. Infosys's font (Alice_in_Wonderland_3.ttf) isn't in its
// repository; the cards use the app's font.
export const FONT_SIZE = 30;
export const LINE_HEIGHT = 35;
export const CARD_FONT = `${FONT_SIZE}px Roboto, 'Helvetica Neue', sans-serif`;

// Infosys prints at 300 DPI ("Render size (at 300 DPI)"): a card's size in millimetres
export const MM_PER_PIXEL = 25.4 / 300;

// The ten digits' EAN left-hand (odd parity) codes; the right-hand codes are their inverse
const LEFT_CODES = [
  '0001101',
  '0011001',
  '0010011',
  '0111101',
  '0100011',
  '0110001',
  '0101111',
  '0111011',
  '0110111',
  '0001011',
];
const RIGHT_CODES = LEFT_CODES.map(code => [...code].map(bit => (bit === '0' ? '1' : '0')).join(''));

// The 67 modules (1 = bar) of an EAN-8 number: start guard, four left digits, centre guard, four right digits, end guard
export function ean8Modules(code: string): string {
  if (!/^\d{8}$/.test(code)) throw new Error('An EAN-8 number has 8 digits');
  const digits = [...code].map(Number);
  const left = digits.slice(0, 4).map(digit => LEFT_CODES[digit]);
  const right = digits.slice(4).map(digit => RIGHT_CODES[digit]);
  return `101${left.join('')}01010${right.join('')}101`;
}

// The quiet zone on each side of the bars, in modules (the EAN-8 minimum)
export const QUIET_ZONE = 7;

// PHP wordwrap($text, $width) without cutting words: greedy lines of at most width characters, breaking at spaces; a
// word longer than width gets a line of its own
export function wordwrap(text: string, width: number): string[] {
  const lines: string[] = [];
  let line: string | null = null;
  for (const word of text.split(' ')) {
    if (line === null) {
      line = word;
    } else if ([...line].length + 1 + [...word].length <= width) {
      line += ` ${word}`;
    } else {
      lines.push(line);
      line = word;
    }
  }
  if (line !== null) lines.push(line);
  return lines;
}

// One line of a text item: where it starts (x from the box's left edge, y from the box's top edge to the baseline)
export interface TextLine {
  text: string;
  x: number;
  y: number;
}

// Infosys IdTemplateRenderer::addTextElement: a text that fits the box's width is one centred line. A longer one is
// wrapped at about as many characters as fit (the box's width / the text's width * its length), one line at a time,
// each centred, 35 pixels apart. The first line's baseline is at the box's top edge, as in Infosys (the text sits above
// the box's top edge). The box's height doesn't limit the lines.
export function layoutText(text: string, boxWidth: number, measure: (text: string) => number): TextLine[] {
  const lines: TextLine[] = [];
  let rest = text;
  let y = 0;
  // Each pass takes at least one word, so this always ends; the limit guards against a measure that misbehaves
  for (let pass = 0; pass <= text.length; pass++) {
    const width = measure(rest);
    let line: string;
    if (width <= boxWidth) {
      line = rest;
      rest = '';
    } else {
      const wrapped = wordwrap(rest, Math.floor((boxWidth / width) * [...rest].length));
      line = wrapped[0];
      rest = wrapped.slice(1).join(' ').trim();
    }

    lines.push({ text: line, x: boxWidth / 2 - measure(line) / 2, y });
    y += LINE_HEIGHT;
    if (!rest) break;
  }
  return lines;
}

// The images a card needs; photo is null when the participant has none (Infosys leaves the photo item out then)
export interface CardImages {
  background: CanvasImageSource;
  photo: CanvasImageSource | null;
}

// Draws the card on a canvas the size of the template (Infosys IdTemplateRenderer::processTemplate)
export function drawIdCard(
  context: CanvasRenderingContext2D,
  template: IdCardTemplate,
  card: IdCard,
  images: CardImages,
): void {
  context.drawImage(images.background, 0, 0, template.width, template.height);
  for (const item of template.items) {
    switch (item.type) {
      case 'Photo':
        // Infosys copies the photo into the box, stretched, without the rotation
        if (images.photo) context.drawImage(images.photo, item.x, item.y, item.width, item.height);
        break;
      case 'Barcode':
        drawBarcode(context, card.barcode, item);
        break;
      case 'Text':
        if (item.dataSource) drawText(context, card.texts[item.dataSource] ?? '', item);
        break;
    }
  }
}

// Infosys draws the EAN-8 image (bars only) stretched into the box, without the rotation
function drawBarcode(context: CanvasRenderingContext2D, barcode: string, item: IdCardTemplateItem): void {
  const modules = ean8Modules(barcode);
  const moduleWidth = item.width / (modules.length + 2 * QUIET_ZONE);
  context.fillStyle = '#fff';
  context.fillRect(item.x, item.y, item.width, item.height);
  context.fillStyle = '#000';
  [...modules].forEach((bit, index) => {
    if (bit === '1') {
      context.fillRect(item.x + (QUIET_ZONE + index) * moduleWidth, item.y, moduleWidth, item.height);
    }
  });
}

// Infosys imagettftext with the angle -rotation: each line turns clockwise around its own starting point
function drawText(context: CanvasRenderingContext2D, text: string, item: IdCardTemplateItem): void {
  context.font = CARD_FONT;
  context.fillStyle = '#000';
  context.textBaseline = 'alphabetic';
  for (const line of layoutText(text, item.width, value => context.measureText(value).width)) {
    context.save();
    context.translate(item.x + line.x, item.y + line.y);
    context.rotate((item.rotation * Math.PI) / 180);
    context.fillText(line.text, 0, 0);
    context.restore();
  }
}
