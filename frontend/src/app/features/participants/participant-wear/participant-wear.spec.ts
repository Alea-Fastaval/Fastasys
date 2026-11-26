import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { describe, it, expect } from 'vitest';
import { ParticipantWearOrder } from '@shared/types';
import { ParticipantWearComponent, wearOrderText } from './participant-wear';

const shirt: ParticipantWearOrder = { amount: 2, name: 'T-shirt-Sort', size: 'L', unitPrice: 150, isReceived: false };
const mug: ParticipantWearOrder = { amount: 1, name: 'Krus', size: null, unitPrice: 60, isReceived: true };

describe('wearOrderText (an order in the Infosys Wear box)', () => {
  it('shows the amount, name, size, total and price for one piece', () => {
    // Infosys wrote "300,- (150,-)"; DKK since 2026-11-21
    expect(wearOrderText(shirt, 'stk.', 'str.')).toBe('2 stk. T-shirt-Sort, str. L — 300 DKK (150 DKK)');
  });

  it('leaves the size out when the order has none', () => {
    expect(wearOrderText(mug, 'stk.', 'str.')).toBe('1 stk. Krus — 60 DKK (60 DKK)');
  });
});

describe('ParticipantWearComponent', () => {
  const render = async (orders: ParticipantWearOrder[], canEdit = false) => {
    await TestBed.configureTestingModule({
      imports: [ParticipantWearComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();
    const fixture = TestBed.createComponent(ParticipantWearComponent);
    fixture.componentRef.setInput('orders', orders);
    fixture.componentRef.setInput('participantId', 7);
    fixture.componentRef.setInput('canEdit', canEdit);
    fixture.detectChanges();
    TestBed.inject(HttpTestingController)
      .match('/assets/i18n/en.json')
      .forEach(req => req.flush({}));
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  };

  it('shows one line per order with whether it has been handed out', async () => {
    const element = await render([shirt, mug]);

    const lines = [...element.querySelectorAll('.wear-orders li')].map(li =>
      li.textContent?.replace(/\s+/g, ' ').trim(),
    );
    expect(lines).toEqual([
      '2 participants.wear.pieces T-shirt-Sort, participants.wear.size L — 300 DKK (150 DKK) — (participants.wear.notReceived)',
      '1 participants.wear.pieces Krus — 60 DKK (60 DKK) — (participants.wear.received)',
    ]);
  });

  it('shows an empty box for a participant without orders, like Infosys', async () => {
    const element = await render([]);

    expect(element.querySelectorAll('.wear-orders li').length).toBe(0);
  });

  it('links "Ret/slet" to the edit page for users who may change wear', async () => {
    // Infosys deltager/updatemadwear/:id:
    const element = await render([shirt], true);

    expect(element.querySelector('a.edit-wear')?.getAttribute('href')).toBe('/participants/7/entrance-food-wear');
  });

  it('has no "Ret/slet" link for other users', async () => {
    const element = await render([shirt]);

    expect(element.querySelector('a.edit-wear')).toBeNull();
  });
});
