import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { describe, it, expect } from 'vitest';
import { ParticipantSleepingPlace } from '@shared/types';
import { ParticipantSleepingComponent, sleepingDayText } from './participant-sleeping';

// Infosys saves a night as 22:00 to 10:00 the next day, Copenhagen time (UTC+1 until the clocks go forward on
// Sunday 28 March 2027)
const wednesdayNight: ParticipantSleepingPlace = {
  starts: '2027-03-24T21:00:00Z',
  ends: '2027-03-25T09:00:00Z',
  room: 'Lokale A1',
};
const wholeConvention: ParticipantSleepingPlace = {
  starts: '2027-03-24T21:00:00Z',
  // Monday 10:00 after the clocks have gone forward (UTC+2)
  ends: '2027-03-29T08:00:00Z',
  room: 'Gymnastiksal',
};

describe('sleepingDayText (a day in the Infosys Soveplads box)', () => {
  it("shows the English day name and the day/month, like PHP date('l, j/n')", () => {
    expect(sleepingDayText(wednesdayNight.starts)).toBe('Wednesday, 24/3');
    expect(sleepingDayText(wednesdayNight.ends)).toBe('Thursday, 25/3');
  });

  it('takes the day in Copenhagen, not in UTC', () => {
    // 23:30 UTC on Wednesday is 00:30 on Thursday in Copenhagen
    expect(sleepingDayText('2027-03-24T23:30:00Z')).toBe('Thursday, 25/3');
    // 22:30 UTC on Sunday 28 March is Monday there, after the clocks have gone forward
    expect(sleepingDayText('2027-03-28T22:30:00Z')).toBe('Monday, 29/3');
  });
});

describe('ParticipantSleepingComponent', () => {
  const render = async (places: ParticipantSleepingPlace[] | null) => {
    await TestBed.configureTestingModule({
      imports: [ParticipantSleepingComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    const fixture = TestBed.createComponent(ParticipantSleepingComponent);
    fixture.componentRef.setInput('places', places);
    fixture.detectChanges();
    TestBed.inject(HttpTestingController)
      .match('/assets/i18n/en.json')
      .forEach(req => req.flush({}));
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  };

  it('says that sleeping places are not ported yet while there is no data', async () => {
    const element = await render(null);

    expect(element.querySelector('.not-ported')?.textContent).toContain('participants.sleeping.notPorted');
    expect(element.querySelector('.sleeping-places')).toBeNull();
  });

  it('shows one row per period with the room', async () => {
    const element = await render([wednesdayNight, wholeConvention]);

    const headers = [...element.querySelectorAll('.sleeping-places th')].map(th => th.textContent?.trim());
    expect(headers).toEqual(['participants.sleeping.period', 'participants.sleeping.room']);
    const rows = [...element.querySelectorAll('.sleeping-places tbody tr')].map(tr =>
      [...tr.querySelectorAll('td')].map(td => td.textContent?.replace(/\s+/g, ' ').trim()),
    );
    expect(rows).toEqual([
      ['Wednesday, 24/3 -> Thursday, 25/3', 'Lokale A1'],
      ['Wednesday, 24/3 -> Monday, 29/3', 'Gymnastiksal'],
    ]);
    expect(element.querySelector('.not-ported')).toBeNull();
  });

  it('shows the table without rows for a participant without a sleeping place, like Infosys', async () => {
    const element = await render([]);

    expect(element.querySelectorAll('.sleeping-places th').length).toBe(2);
    expect(element.querySelectorAll('.sleeping-places tbody tr').length).toBe(0);
    expect(element.querySelector('.not-ported')).toBeNull();
  });
});
