import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { IdCards } from '@shared/types';
import { IdCardRendererService, RenderedCard } from '../id-card-renderer.service';
import { IdCardsPrintComponent } from './id-cards-print';

const data: IdCards = { cards: [], templates: [] };
const rendered: RenderedCard[] = [
  { participantId: 7, src: 'data:image/png;base64,MQ==', widthMm: 85.6, heightMm: 54 },
  { participantId: 8, src: 'data:image/png;base64,Mg==', widthMm: 85.6, heightMm: 54 },
];

describe('IdCardsPrintComponent (Infosys id-card/render)', () => {
  let httpMock: HttpTestingController;
  let render: ReturnType<typeof vi.fn>;

  beforeEach(async () => {
    render = vi.fn(() => Promise.resolve(rendered));
    await TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'id-cards', component: IdCardsPrintComponent }]),
        { provide: IdCardRendererService, useValue: { render } },
      ],
    }).compileComponents();
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    vi.restoreAllMocks();
    httpMock.verify();
  });

  const open = async (url: string) => {
    const harness = await RouterTestingHarness.create();
    const component = await harness.navigateByUrl(url, IdCardsPrintComponent);
    harness.detectChanges();
    httpMock.match('/assets/i18n/en.json').forEach(req => req.flush({}));
    return { harness, component, element: harness.routeNativeElement as HTMLElement };
  };

  const settle = async (harness: RouterTestingHarness) => {
    await new Promise(resolve => setTimeout(resolve));
    harness.detectChanges();
  };

  it('draws the cards of the participants in the URL at their printed size', async () => {
    const { harness, element } = await open('/id-cards?ids=7-8');
    expect(element.querySelector('.loading')).not.toBeNull();

    const request = httpMock.expectOne(r => r.url === '/api/id-cards');
    expect(request.request.params.get('ids')).toBe('7-8');
    request.flush(data);
    await settle(harness);

    expect(render).toHaveBeenCalledWith(data);
    const cards = [...element.querySelectorAll<HTMLImageElement>('img.card')];
    expect(cards.map(card => card.getAttribute('data-participant'))).toEqual(['7', '8']);
    expect(cards[0].getAttribute('src')).toBe('data:image/png;base64,MQ==');
    expect(cards[0].style.width).toBe('85.6mm');
    expect(cards[0].style.height).toBe('54mm');
  });

  it('opens the print dialog', async () => {
    const print = vi.spyOn(window, 'print').mockImplementation(() => undefined);
    const { harness, element } = await open('/id-cards?ids=7');
    httpMock.expectOne(r => r.url === '/api/id-cards').flush(data);
    await settle(harness);

    element.querySelector<HTMLButtonElement>('button.print')?.click();

    expect(print).toHaveBeenCalled();
  });

  it('says there are no cards when nobody is asked for', async () => {
    const { harness, element } = await open('/id-cards');
    await settle(harness);

    expect(element.querySelector('.none')).not.toBeNull();
    expect(element.querySelector('button.print')).toBeNull();
  });

  it('says there are no cards when nobody asked for has one', async () => {
    render.mockResolvedValue([]);
    const { harness, element } = await open('/id-cards?ids=9');
    httpMock.expectOne(r => r.url === '/api/id-cards').flush(data);
    await settle(harness);

    expect(element.querySelector('.none')).not.toBeNull();
  });

  it('says so when the cards could not be fetched', async () => {
    const { harness, element } = await open('/id-cards?ids=7');
    httpMock.expectOne(r => r.url === '/api/id-cards').flush(null, { status: 500, statusText: 'Server Error' });
    await settle(harness);

    expect(element.querySelector('.flash.error')?.textContent).toContain('idCards.failed');
    expect(render).not.toHaveBeenCalled();
  });
});
