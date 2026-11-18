import { describe, it, expect } from 'vitest';
import { IdCard, IdCardTemplate } from '@shared/types';
import { LINE_HEIGHT, QUIET_ZONE, drawIdCard, ean8Modules, layoutText, wordwrap } from './id-card-drawing';

// A measure where every character is 10 pixels wide
const measure = (text: string) => [...text].length * 10;

describe('ean8Modules (the bars of an EAN-8 barcode)', () => {
  it('has the guards and the left and right codes of the digits', () => {
    // 2701234 with check digit 5 (the participant 1234 in 2027)
    const modules = ean8Modules('27012345');

    expect(modules).toHaveLength(67);
    expect(modules.slice(0, 3)).toBe('101');
    // 2 in the left code
    expect(modules.slice(3, 10)).toBe('0010011');
    expect(modules.slice(31, 36)).toBe('01010');
    // 2 in the right code
    expect(modules.slice(36, 43)).toBe('1101100');
    expect(modules.slice(64)).toBe('101');
  });

  it('refuses what is not 8 digits', () => {
    expect(() => ean8Modules('1234567')).toThrow();
    expect(() => ean8Modules('1234567a')).toThrow();
  });
});

describe('wordwrap (PHP wordwrap without cutting words)', () => {
  it('breaks at spaces into lines of at most the width', () => {
    expect(wordwrap('Anna Testesen Hansen', 10)).toEqual(['Anna', 'Testesen', 'Hansen']);
    expect(wordwrap('Dr. Anna Hansen', 9)).toEqual(['Dr. Anna', 'Hansen']);
  });

  it('keeps a word longer than the width whole', () => {
    expect(wordwrap('Brætspilsdesigner', 5)).toEqual(['Brætspilsdesigner']);
  });
});

describe('layoutText (Infosys IdTemplateRenderer::addTextElement)', () => {
  it('centres a text that fits on one line, with its baseline at the top of the box', () => {
    expect(layoutText('Anna', 200, measure)).toEqual([{ text: 'Anna', x: 80, y: 0 }]);
  });

  it('wraps a longer text and centres each line, 35 pixels apart', () => {
    // 20 characters (200 px) in a 120 px box: wrap at floor(120 / 200 * 20) = 12 characters
    const lines = layoutText('Anna Testesen Hansen', 120, measure);

    expect(lines).toEqual([
      { text: 'Anna', x: 40, y: 0 },
      { text: 'Testesen', x: 20, y: LINE_HEIGHT },
      { text: 'Hansen', x: 30, y: 2 * LINE_HEIGHT },
    ]);
  });

  it('lets a word wider than the box stick out on both sides', () => {
    expect(layoutText('Brætspilsdesigner Hansen', 100, measure)[0]).toEqual({
      text: 'Brætspilsdesigner',
      x: -35,
      y: 0,
    });
  });

  it('draws an empty text as one empty line', () => {
    expect(layoutText('', 100, measure)).toEqual([{ text: '', x: 50, y: 0 }]);
  });
});

// Records what drawIdCard asks the canvas to do
function recordingContext() {
  const calls: string[] = [];
  const context = {
    drawImage: (_image: unknown, x: number, y: number, w: number, h: number) => calls.push(`image ${x},${y} ${w}x${h}`),
    fillRect: (x: number, y: number, w: number, h: number) => calls.push(`rect ${x},${y} ${w}x${h}`),
    fillText: (text: string, x: number, y: number) => calls.push(`text "${text}" ${x},${y}`),
    measureText: (text: string) => ({ width: measure(text) }),
    translate: (x: number, y: number) => calls.push(`translate ${x},${y}`),
    rotate: (angle: number) => calls.push(`rotate ${Math.round((angle * 180) / Math.PI)}`),
    save: () => undefined,
    restore: () => undefined,
    font: '',
    fillStyle: '',
    textBaseline: '',
  };
  return { calls, context: context as unknown as CanvasRenderingContext2D };
}

const card: IdCard = {
  participantId: 1234,
  templateId: 1,
  hasPhoto: true,
  barcode: '27012345',
  texts: {
    Id: '1234',
    Name: 'Anna',
    DoctorName: 'Dr. Anna',
    OrganizerArea: 'Kiosken',
    Game: '',
    YouthClub: '',
  },
};

const template: IdCardTemplate = {
  id: 1,
  name: 'Arrangør27',
  width: 1011,
  height: 638,
  hasBackground: true,
  items: [
    { type: 'Photo', x: 30, y: 40, width: 213, height: 295, rotation: 90, dataSource: null },
    { type: 'Text', x: 300, y: 100, width: 200, height: 50, rotation: 90, dataSource: 'Name' },
    { type: 'Barcode', x: 300, y: 400, width: 162, height: 100, rotation: 0, dataSource: null },
  ],
};

describe('drawIdCard (Infosys IdTemplateRenderer::processTemplate)', () => {
  it('draws the background at the card size, then the items in their order', () => {
    const { calls, context } = recordingContext();

    drawIdCard(context, template, card, { background: {} as CanvasImageSource, photo: {} as CanvasImageSource });

    expect(calls[0]).toBe('image 0,0 1011x638');
    // The photo stretched into its box, without the rotation (as Infosys)
    expect(calls[1]).toBe('image 30,40 213x295');
    // The text turns around the start of its line; the line is centred in the box
    expect(calls.slice(2, 5)).toEqual(['translate 380,100', 'rotate 90', 'text "Anna" 0,0']);
    // The barcode: a white box, then the bars, 2 pixels per module with 7 modules of quiet zone (162 / 81)
    expect(calls[5]).toBe('rect 300,400 162x100');
    expect(calls[6]).toBe(`rect ${300 + QUIET_ZONE * 2},400 2x100`);
  });

  it('leaves the photo out when the participant has none', () => {
    const { calls, context } = recordingContext();

    drawIdCard(context, template, { ...card, hasPhoto: false }, { background: {} as CanvasImageSource, photo: null });

    expect(calls.filter(call => call.startsWith('image'))).toEqual(['image 0,0 1011x638']);
  });
});
