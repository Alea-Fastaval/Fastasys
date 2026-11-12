import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { describe, it, expect } from 'vitest';
import { ParticipantPrice } from '@shared/types';
import { ParticipantPriceComponent, priceDifferences, priceText } from './participant-price';

// Totals as the API works them out (ParticipantPriceDto): 400 + 150 + 90 + 200 + 75 + 50 and the same with 30
const price: ParticipantPrice = {
  originalPrice: 900,
  entry: 400,
  food: 150,
  wear: 200,
  signedUpActivities: 90,
  assignedActivities: 30,
  other: 75,
  alea: 50,
  signupTotal: 965,
  checkInTotal: 905,
};

describe('priceText (an amount in the Infosys Pris breakdown)', () => {
  it('shows whole kroner followed by ",-", like Infosys', () => {
    expect(priceText(450)).toBe('450,-');
    expect(priceText(0)).toBe('0,-');
    expect(priceText(12500)).toBe('12500,-');
  });

  it('keeps the minus of a negative amount', () => {
    expect(priceText(-150)).toBe('-150,-');
  });
});

describe('priceDifferences (the lines Infosys works out in its template)', () => {
  it('subtracts what the participant has paid from the totals', () => {
    expect(priceDifferences(price, 450)).toEqual({ signup: 515, unassignedActivities: 60, checkIn: 455 });
  });

  it('is negative when the participant has paid more than the total', () => {
    expect(priceDifferences(price, 1000).signup).toBe(-35);
    expect(priceDifferences(price, 1000).checkIn).toBe(-95);
  });

  it('is negative for activities when the participant was given more paid seats than they signed up for', () => {
    expect(priceDifferences({ ...price, signedUpActivities: 0, assignedActivities: 30 }, 0).unassignedActivities).toBe(
      -30,
    );
  });
});

describe('ParticipantPriceComponent', () => {
  let current: ComponentFixture<ParticipantPriceComponent> | undefined;
  const fixture = (): ComponentFixture<ParticipantPriceComponent> => {
    if (!current) throw new Error('Nothing rendered');
    return current;
  };
  const render = async (value: ParticipantPrice | null, amountPaid: number, dictionary: object = {}) => {
    await TestBed.configureTestingModule({
      imports: [ParticipantPriceComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    current = TestBed.createComponent(ParticipantPriceComponent);
    current.componentRef.setInput('price', value);
    current.componentRef.setInput('amountPaid', amountPaid);
    current.detectChanges();
    TestBed.inject(HttpTestingController)
      .match('/assets/i18n/en.json')
      .forEach(req => req.flush(dictionary));
    current.detectChanges();
    return current.nativeElement as HTMLElement;
  };
  const rowText = (row: Element) =>
    [...row.querySelectorAll('td')].map(td => td.textContent?.replace(/\s+/g, ' ').trim()).filter(text => text);

  it('says that prices are not ported yet while there is no data', async () => {
    const element = await render(null, 450);

    expect(element.querySelector('.not-ported')?.textContent).toContain('participants.price.notPorted');
    expect(element.querySelector('.price-breakdown')).toBeNull();
  });

  it('shows the lines in the Infosys order with the totals and differences', async () => {
    const element = await render(price, 450);

    const rows = [...element.querySelectorAll('.price-breakdown tr')].map(rowText);
    expect(rows).toEqual([
      ['participants.price.originalPrice', '900,-'],
      [],
      ['participants.price.entry', '400,-'],
      ['participants.price.food', '150,-'],
      ['participants.price.wear', '200,-'],
      ['participants.price.activities', '90,-'],
      ['participants.price.other', '75,-'],
      ['participants.price.alea', '50,-'],
      ['participants.price.signupTotal', '965,-'],
      ['participants.price.amountPaid', '450,-'],
      ['participants.price.signupDifference', '515,-'],
      [],
      ['participants.price.unassignedActivities', '60,-'],
      ['participants.price.checkInTotal', '905,-'],
      ['participants.price.checkInDifference', '455,-'],
    ]);
    expect(element.querySelector('.not-ported')).toBeNull();
  });

  it('underlines Alea and the differences, and highlights the differences, like Infosys', async () => {
    const element = await render(price, 450);

    const underlined = [...element.querySelectorAll('.price-breakdown tr.underline')].map(row => row.className);
    expect(underlined).toEqual([
      'alea underline',
      'signup-difference underline difference',
      'check-in-difference underline difference',
    ]);
  });

  it('puts the check-in total and the amount paid into the label of the last line', async () => {
    const element = await render(price, 450, {
      participants: { price: { checkInDifference: 'Difference ved check-ind ({{total}} - {{paid}}):' } },
    });

    expect(element.querySelector('.check-in-difference td')?.textContent?.trim()).toBe(
      'Difference ved check-ind (905 - 450):',
    );
  });

  it('follows the amount paid when it changes, e.g. after Forudbetalt is edited on the page', async () => {
    const element = await render(price, 450);
    expect(element.querySelector('.signup-difference .amount')?.textContent?.trim()).toBe('515,-');

    fixture().componentRef.setInput('amountPaid', 965);
    fixture().detectChanges();

    expect(element.querySelector('.amount-paid .amount')?.textContent?.trim()).toBe('965,-');
    expect(element.querySelector('.signup-difference .amount')?.textContent?.trim()).toBe('0,-');
  });
});
