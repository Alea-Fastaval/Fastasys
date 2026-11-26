import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { HeroAvailability, ParticipantHeroAvailability } from '@shared/types';
import { ParticipantFlashService } from '../participant-flash.service';
import {
  ANY_CATEGORY,
  ParticipantHeroAvailabilityComponent,
  addHeroPeriod,
  heroDayOptions,
} from './participant-hero-availability';

const anyMorning: HeroAvailability = { categoryId: null, categoryName: null, date: '2027-03-24', period: 'Morning' };
const kitchenEvening: HeroAvailability = {
  categoryId: 1,
  categoryName: 'Forplejning',
  date: '2027-03-25',
  period: 'Evening',
};

describe('the hero availability rows', () => {
  it('offers the convention days and the days the participant has periods on', () => {
    expect(heroDayOptions('2027-03-24', '2027-03-26', [anyMorning])).toEqual([
      '2027-03-24',
      '2027-03-25',
      '2027-03-26',
    ]);
    expect(heroDayOptions('2027-03-24', '2027-03-25', [{ ...anyMorning, date: '2027-03-30' }])).toEqual([
      '2027-03-24',
      '2027-03-25',
      '2027-03-30',
    ]);
    expect(heroDayOptions(null, null, [])).toEqual([]);
  });

  it('adds a period unless it is listed already (Infosys checked the wrong field)', () => {
    expect(addHeroPeriod([anyMorning], kitchenEvening)).toEqual([anyMorning, kitchenEvening]);
    expect(addHeroPeriod([anyMorning], { ...anyMorning })).toEqual([anyMorning]);
    // The same time for another category is another period
    expect(addHeroPeriod([anyMorning], { ...anyMorning, categoryId: 1, categoryName: 'Forplejning' })).toHaveLength(2);
  });
});

describe('ParticipantHeroAvailabilityComponent (Infosys deltager/updategdstilmeldinger)', () => {
  let http: HttpTestingController;

  afterEach(() => http.verify());

  const page: ParticipantHeroAvailability = {
    participantId: 7,
    availability: [anyMorning, kitchenEvening],
    categories: [
      { id: 1, nameDanish: 'Forplejning' },
      { id: 5, nameDanish: 'Brandvagt' },
    ],
  };

  const render = async (found = true) => {
    await TestBed.configureTestingModule({
      imports: [ParticipantHeroAvailabilityComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: '7' }) } } },
      ],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(ParticipantHeroAvailabilityComponent);
    fixture.detectChanges();
    http.match('/assets/i18n/en.json').forEach(request => request.flush({}));
    http.expectOne('/api/signup/status').flush({ conventionStart: '2027-03-24', conventionEnd: '2027-03-29' });
    const request = http.expectOne('/api/participants/7/hero-availability');
    if (found) request.flush(page);
    else request.flush({}, { status: 404, statusText: 'Not found' });
    fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, navigate, flash: TestBed.inject(ParticipantFlashService) };
  };

  it('lists the periods with the category, or "Alle" for any category', async () => {
    const { fixture, component } = await render();
    const element = fixture.nativeElement as HTMLElement;

    expect([...element.querySelectorAll('.period-list .period')].map(td => td.textContent?.trim())).toEqual([
      'participants.heroSignups.anyCategory common.weekdays.Wednesday 7-12',
      'Forplejning common.weekdays.Thursday 17-24',
    ]);
    expect(component.days()).toHaveLength(6);
    expect(component.dayText('2027-03-24')).toBe('common.weekdays.Wednesday 24/03');
  });

  it('adds a period for a category or any category, and "Opdater" saves the list, any category included', async () => {
    const { component, navigate, flash } = await render();

    expect(component.canAdd()).toBe(false);
    component.chosenCategory = 5;
    component.chosenDay = '2027-03-26';
    component.chosenPeriod = 'Afternoon';
    component.add();
    component.chosenCategory = ANY_CATEGORY;
    component.add();
    // The same period again is kept once
    component.add();
    component.remove(component.rows()[1]);
    component.submit();

    const request = http.expectOne('/api/participants/7/hero-availability');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({
      availability: [
        { categoryId: null, date: '2027-03-24', period: 'Morning' },
        { categoryId: 5, date: '2027-03-26', period: 'Afternoon' },
        { categoryId: null, date: '2027-03-26', period: 'Afternoon' },
      ],
    });
    request.flush(null);

    expect(flash.take()).toEqual({ type: 'success', key: 'updated' });
    expect(navigate).toHaveBeenCalledWith(['/participants', 7]);
  });

  it('goes back with "Kunne ikke opdatere deltageren." when saving fails', async () => {
    const { component, navigate, flash } = await render();

    component.submit();
    http.expectOne('/api/participants/7/hero-availability').flush({}, { status: 500, statusText: 'Server error' });

    expect(flash.take()).toEqual({ type: 'error', key: 'heroAvailabilityUpdateFailed' });
    expect(navigate).toHaveBeenCalledWith(['/participants', 7]);
  });

  it('"Fortryd" goes back saying nothing was changed, like Infosys', async () => {
    const { component, flash } = await render();

    component.cancel();

    expect(flash.take()).toEqual({ type: 'error', key: 'heroAvailabilityNotUpdated' });
  });

  it('says so for an unknown participant', async () => {
    const { fixture } = await render(false);

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('participants.messages.notFound');
  });
});
