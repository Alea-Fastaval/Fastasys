import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { describe, it, expect } from 'vitest';
import { ParticipantHeroSignup } from '@shared/types';
import { Weekday } from '@shared/utils';
import { ParticipantHeroSignupsComponent, heroSignupPeriodText, weekdayOf } from './participant-hero-signups';

const danish: Record<Weekday, string> = {
  Monday: 'Mandag',
  Tuesday: 'Tirsdag',
  Wednesday: 'Onsdag',
  Thursday: 'Torsdag',
  Friday: 'Fredag',
  Saturday: 'Lørdag',
  Sunday: 'Søndag',
};

const kitchen: ParticipantHeroSignup = { category: 'Forplejning', date: '2027-03-24', hours: '7-12' };
const sunday: ParticipantHeroSignup = { category: 'Rengøring', date: '2027-03-28', hours: '17-22' };

describe('weekdayOf (the day of a date in Denmark)', () => {
  it('gives the day of the date', () => {
    expect(weekdayOf('2027-03-24')).toBe('Wednesday');
  });

  it('gives the same day on the day the clocks go forward and back', () => {
    expect(weekdayOf('2027-03-28')).toBe('Sunday');
    expect(weekdayOf('2027-10-31')).toBe('Sunday');
  });
});

describe('heroSignupPeriodText (a period in the Infosys Heltestyrken, Tilmeldinger box)', () => {
  it("shows the day's name and the hours, like getMeaningfulPeriod with danishDayNames", () => {
    expect(heroSignupPeriodText(kitchen, weekday => danish[weekday])).toBe('Onsdag 7-12');
    expect(heroSignupPeriodText(sunday, weekday => danish[weekday])).toBe('Søndag 17-22');
  });
});

describe('ParticipantHeroSignupsComponent', () => {
  const render = async (signups: ParticipantHeroSignup[] | null) => {
    await TestBed.configureTestingModule({
      imports: [ParticipantHeroSignupsComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    const fixture = TestBed.createComponent(ParticipantHeroSignupsComponent);
    fixture.componentRef.setInput('signups', signups);
    fixture.detectChanges();
    TestBed.inject(HttpTestingController)
      .match('/assets/i18n/en.json')
      .forEach(req => req.flush({ common: { weekdays: danish } }));
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  };

  it('says that the hero force is not ported yet while there is no data', async () => {
    const element = await render(null);

    expect(element.querySelector('.not-ported')?.textContent).toContain('participants.heroForceNotPorted');
    expect(element.querySelector('.hero-table')).toBeNull();
  });

  it('shows one row per signup with the category in bold and the period', async () => {
    const element = await render([kitchen, sunday]);

    const rows = [...element.querySelectorAll('.hero-table tr')].map(tr =>
      [...tr.querySelectorAll('td')].map(td => td.textContent?.trim()),
    );
    expect(rows).toEqual([
      ['Forplejning', 'Onsdag 7-12'],
      ['Rengøring', 'Søndag 17-22'],
    ]);
    expect(element.querySelector('.category strong')?.textContent).toBe('Forplejning');
    expect(element.querySelector('.not-ported')).toBeNull();
  });

  it('shows no rows for a participant without signups, like Infosys', async () => {
    const element = await render([]);

    expect(element.querySelectorAll('.hero-table tr').length).toBe(0);
    expect(element.querySelector('.not-ported')).toBeNull();
  });
});
