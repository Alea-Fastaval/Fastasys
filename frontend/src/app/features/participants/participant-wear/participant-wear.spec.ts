import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { describe, it, expect } from 'vitest';
import { ParticipantWearOrder } from '@shared/types';
import { ParticipantWearComponent, wearOrderText } from './participant-wear';

const shirt: ParticipantWearOrder = { amount: 2, name: 'T-shirt-Sort', size: 'L', unitPrice: 150, isReceived: false };
const mug: ParticipantWearOrder = { amount: 1, name: 'Krus', size: null, unitPrice: 60, isReceived: true };

describe('wearOrderText (an order in the Infosys Wear box)', () => {
  it('shows the amount, name, size, total and price for one piece', () => {
    expect(wearOrderText(shirt, 'stk.', 'str.')).toBe('2 stk. T-shirt-Sort, str. L — 300,- (150,-)');
  });

  it('leaves the size out when the order has none', () => {
    expect(wearOrderText(mug, 'stk.', 'str.')).toBe('1 stk. Krus — 60,- (60,-)');
  });
});

describe('ParticipantWearComponent', () => {
  const render = async (orders: ParticipantWearOrder[] | null) => {
    await TestBed.configureTestingModule({
      imports: [ParticipantWearComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    const fixture = TestBed.createComponent(ParticipantWearComponent);
    fixture.componentRef.setInput('orders', orders);
    fixture.detectChanges();
    TestBed.inject(HttpTestingController)
      .match('/assets/i18n/en.json')
      .forEach(req => req.flush({}));
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  };

  it('says that wear is not ported yet while there is no data', async () => {
    const element = await render(null);

    expect(element.querySelector('.not-ported')?.textContent).toContain('participants.wear.notPorted');
    expect(element.querySelector('.wear-orders')).toBeNull();
  });

  it('shows one line per order with whether it has been handed out', async () => {
    const element = await render([shirt, mug]);

    const lines = [...element.querySelectorAll('.wear-orders li')].map(li =>
      li.textContent?.replace(/\s+/g, ' ').trim(),
    );
    expect(lines).toEqual([
      '2 participants.wear.pieces T-shirt-Sort, participants.wear.size L — 300,- (150,-) — (participants.wear.notReceived)',
      '1 participants.wear.pieces Krus — 60,- (60,-) — (participants.wear.received)',
    ]);
    expect(element.querySelector('.not-ported')).toBeNull();
  });

  it('shows an empty box for a participant without orders, like Infosys', async () => {
    const element = await render([]);

    expect(element.querySelectorAll('.wear-orders li').length).toBe(0);
    expect(element.querySelector('.not-ported')).toBeNull();
  });
});
