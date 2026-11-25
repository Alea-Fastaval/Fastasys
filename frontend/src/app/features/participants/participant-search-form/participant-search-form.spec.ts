import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { describe, it, expect } from 'vitest';
import { CriteriaSearchStep } from '@shared/types';
import { ParticipantSearchFormComponent, mealSearchGrid, newSearchForm, toSearchStep } from './participant-search-form';

// Dinner on Wednesday and Thursday, breakfast on Thursday, and a food type without meal times. 23:30 UTC on Wednesday
// is 00:30 on Thursday in Copenhagen.
const foodTypes = [
  {
    id: 1,
    nameDanish: 'Aftensmad',
    mealTimes: [
      { id: 10, time: '2027-03-24T16:00:00Z' },
      { id: 11, time: '2027-03-25T16:00:00Z' },
    ],
  },
  { id: 2, nameDanish: 'Natmad', mealTimes: [{ id: 21, time: '2027-03-24T23:30:00Z' }] },
  { id: 3, nameDanish: 'Skjult', mealTimes: [] },
];

describe('mealSearchGrid (the Infosys search box Mad section)', () => {
  it('has a column per day with a meal time and a row per food type with meal times', () => {
    const grid = mealSearchGrid(foodTypes);

    expect(grid.days).toEqual(['2027-03-24', '2027-03-25']);
    expect(grid.rows.map(row => row.food.id)).toEqual([1, 2]);
    expect(grid.rows[0].cells.map(cell => cell.map(m => m.id))).toEqual([[10], [11]]);
    // The day is the Copenhagen day
    expect(grid.rows[1].cells.map(cell => cell.map(m => m.id))).toEqual([[], [21]]);
  });
});

describe('toSearchStep (the Infosys search box)', () => {
  it('sends nothing for an untouched box: a new "And" search for everyone', () => {
    expect(toSearchStep(newSearchForm())).toEqual({ criteria: {}, logic: 'And', combination: 'New' });
  });

  it('sends only the fields that have a value, with ja/nej as true/false', () => {
    const form = {
      ...newSearchForm(),
      firstName: 'Anna',
      isNoShow: 'nej' as const,
      superHero: 'ja' as const,
      categoryId: 2,
      desiredActivities: 0,
      activityLanguages: ['English' as const],
      logic: 'Or' as const,
      combination: 'Difference' as const,
    };

    expect(toSearchStep(form)).toEqual({
      criteria: {
        firstName: 'Anna',
        isNoShow: false,
        superHero: true,
        categoryId: 2,
        desiredActivities: 0,
        activityLanguages: ['English'],
      },
      logic: 'Or',
      combination: 'Difference',
    });
  });

  it('sends the Indgang rows set to ja or nej, in ticket type id order', () => {
    const form = { ...newSearchForm(), tickets: { 9: 'nej' as const, 2: 'ja' as const, 5: '' as const } };

    expect(toSearchStep(form).criteria).toEqual({
      tickets: [
        { ticketTypeId: 2, has: true },
        { ticketTypeId: 9, has: false },
      ],
    });
    expect(toSearchStep({ ...newSearchForm(), tickets: { 5: '' } }).criteria).toEqual({});
  });

  it('sends the Mad selects set to ja or nej, in meal time id order', () => {
    const form = { ...newSearchForm(), meals: { 21: 'ja' as const, 10: 'nej' as const, 11: '' as const } };

    expect(toSearchStep(form).criteria).toEqual({
      meals: [
        { mealTimeId: 10, has: false },
        { mealTimeId: 21, has: true },
      ],
    });
  });

  it('sends Før/Efter/Samme only with a birth date', () => {
    expect(toSearchStep({ ...newSearchForm(), birthDateRange: 'After' }).criteria).toEqual({});
    expect(toSearchStep({ ...newSearchForm(), birthDate: '2000-01-01', birthDateRange: 'After' }).criteria).toEqual({
      birthDate: '2000-01-01',
      birthDateRange: 'After',
    });
  });
});

describe('ParticipantSearchFormComponent', () => {
  it('sends the search when "Søg" is pressed', async () => {
    await TestBed.configureTestingModule({
      imports: [ParticipantSearchFormComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    const fixture = TestBed.createComponent(ParticipantSearchFormComponent);
    fixture.componentRef.setInput('categories', [{ id: 1, name: 'Deltager' }]);
    fixture.detectChanges();
    TestBed.inject(HttpTestingController)
      .match('/assets/i18n/en.json')
      .forEach(req => req.flush({}));
    expect(fixture.componentInstance.categories).toEqual([{ id: 1, name: 'Deltager' }]);
    const sent: CriteriaSearchStep[] = [];
    fixture.componentInstance.searched.subscribe(step => sent.push(step));

    fixture.componentInstance.form().city = 'Aarhus';
    fixture.componentInstance.toggleLanguage('Danish', true);
    (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('button[type=submit]')?.click();

    expect(sent).toEqual([
      { criteria: { city: 'Aarhus', activityLanguages: ['Danish'] }, logic: 'And', combination: 'New' },
    ]);
    // "Reset" searches instead of clearing in Infosys; it is left out until that is decided
    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('Reset');
    // No ticket types, no Indgang section; no meal times, no Mad section
    expect((fixture.nativeElement as HTMLElement).querySelector('fieldset.tickets')).toBeNull();
    expect((fixture.nativeElement as HTMLElement).querySelector('fieldset.meals')).toBeNull();
  });

  it('shows the Mad section with a select per meal time', async () => {
    await TestBed.configureTestingModule({
      imports: [ParticipantSearchFormComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    const fixture = TestBed.createComponent(ParticipantSearchFormComponent);
    fixture.componentRef.setInput('foodTypes', foodTypes);
    fixture.detectChanges();
    TestBed.inject(HttpTestingController)
      .match('/assets/i18n/en.json')
      .forEach(req => req.flush({}));
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;

    const headers = [...element.querySelectorAll('fieldset.meals th.meal-day')].map(th => th.textContent?.trim());
    expect(headers).toEqual(['common.weekdaysShort.Wednesday', 'common.weekdaysShort.Thursday']);
    const names = [...element.querySelectorAll('fieldset.meals .food-name')].map(td => td.textContent?.trim());
    expect(names).toEqual(['Aftensmad', 'Natmad']);
    expect(element.querySelectorAll('fieldset.meals .meal-choice').length).toBe(3);
  });

  it('lists the ticket types in the Indgang section, without a day for a ticket for the whole convention', async () => {
    await TestBed.configureTestingModule({
      imports: [ParticipantSearchFormComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    const fixture = TestBed.createComponent(ParticipantSearchFormComponent);
    fixture.componentRef.setInput('ticketTypes', [
      { id: 1, name: 'Indgang - Partout', day: '2027-03-24', kind: 'Entrance', isAllConvention: true },
      { id: 3, name: 'Indgang - Enkelt', day: '2027-03-25', kind: 'Entrance', isAllConvention: false },
      { id: 4, name: 'Alea medlemskab', day: '2027-03-24', kind: 'AleaMembership', isAllConvention: false },
    ]);
    fixture.detectChanges();
    TestBed.inject(HttpTestingController)
      .match('/assets/i18n/en.json')
      .forEach(req => req.flush({}));
    fixture.detectChanges();

    const rows = [...(fixture.nativeElement as HTMLElement).querySelectorAll('fieldset.tickets .ticket-name')].map(
      cell => cell.textContent?.trim(),
    );
    expect(rows).toEqual(['Indgang - Partout', 'Indgang - Enkelt common.weekdaysShort.Thursday', 'Alea medlemskab']);
  });
});
