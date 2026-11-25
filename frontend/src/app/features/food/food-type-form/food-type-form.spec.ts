import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { FoodTypeDetail } from '@shared/types';
import { FoodFlashService } from '../food-flash.service';
import { newMealTimeRow } from '../food-texts';
import { FoodTypeFormComponent, FoodTypeForm, canSubmitFoodType, newFoodTypeForm, toDto } from './food-type-form';

const mealTime = { ...newMealTimeRow(), day: '2027-03-24', clock: '17:00' };
const complete: FoodTypeForm = { ...newFoodTypeForm(), nameDanish: 'Aftensmad', price: 68, mealTimes: [mealTime] };

const existing: FoodTypeDetail = {
  id: 3,
  nameDanish: 'Aftensmad',
  nameEnglish: 'Dinner',
  price: 68,
  isHidden: false,
  mealTimes: [
    { id: 10, time: '2027-03-24T16:00:00Z', descriptionDanish: '', descriptionEnglish: '', orderCount: 2 },
    { id: 11, time: '2027-02-03T11:00:00Z', descriptionDanish: '', descriptionEnglish: '', orderCount: 0 },
  ],
};

describe('canSubmitFoodType (the rules the API checks)', () => {
  it('needs a kategori, a whole price of 0 or more, and a day and time for every meal time', () => {
    expect(canSubmitFoodType(newFoodTypeForm())).toBe(false);
    expect(canSubmitFoodType(complete)).toBe(true);
    // A food type without meal times may be saved, as in Infosys
    expect(canSubmitFoodType({ ...complete, mealTimes: [] })).toBe(true);
    expect(canSubmitFoodType({ ...complete, nameDanish: '0' })).toBe(false);
    expect(canSubmitFoodType({ ...complete, nameDanish: 'x'.repeat(101) })).toBe(false);
    expect(canSubmitFoodType({ ...complete, nameEnglish: 'x'.repeat(101) })).toBe(false);
    expect(canSubmitFoodType({ ...complete, price: 0 })).toBe(true);
    expect(canSubmitFoodType({ ...complete, price: -1 })).toBe(false);
    expect(canSubmitFoodType({ ...complete, price: 7.5 })).toBe(false);
    expect(canSubmitFoodType({ ...complete, mealTimes: [{ ...mealTime, clock: '' }] })).toBe(false);
    expect(canSubmitFoodType({ ...complete, mealTimes: [{ ...mealTime, day: '' }] })).toBe(false);
    // Infosys madtider: unique (mad_id, dato)
    expect(canSubmitFoodType({ ...complete, mealTimes: [mealTime, { ...mealTime }] })).toBe(false);
  });

  it('sends the meal times as instants (17:00 in Copenhagen is 16:00 UTC that week)', () => {
    expect(toDto(complete)).toEqual({
      nameDanish: 'Aftensmad',
      nameEnglish: '',
      price: 68,
      isHidden: false,
      mealTimes: [{ id: null, time: '2027-03-24T16:00:00.000Z', descriptionDanish: '', descriptionEnglish: '' }],
    });
  });
});

// The form's meal time rows (the form is loaded in these tests)
const mealTimes = (component: FoodTypeFormComponent) => component.form()?.mealTimes ?? [];

describe('FoodTypeFormComponent', () => {
  let http: HttpTestingController;

  afterEach(() => http.verify());

  const render = async (id: string | null) => {
    await TestBed.configureTestingModule({
      imports: [FoodTypeFormComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap(id ? { id } : {}) } } },
      ],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(FoodTypeFormComponent);
    fixture.detectChanges();
    http.match('/assets/i18n/en.json').forEach(request => request.flush({}));
    http.expectOne('/api/signup/status').flush({ conventionStart: '2027-03-24', conventionEnd: '2027-03-29' });
    if (id) http.expectOne(`/api/food-types/${id}`).flush(existing);
    fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, navigate, flash: TestBed.inject(FoodFlashService) };
  };

  it("adds meal times with the convention's days, and creates the type", async () => {
    const { fixture, component, navigate, flash } = await render(null);

    component.addMealTime();
    fixture.detectChanges();
    const row = mealTimes(component)[0];
    expect(component.days(row)).toEqual([
      '2027-03-24',
      '2027-03-25',
      '2027-03-26',
      '2027-03-27',
      '2027-03-28',
      '2027-03-29',
    ]);
    expect(component.dayText('2027-03-24')).toBe('common.weekdays.Wednesday 24/03');
    expect((fixture.nativeElement as HTMLElement).querySelectorAll('.meal-time mat-select').length).toBe(1);

    component.form.set(complete);
    component.submit();
    const request = http.expectOne('/api/food-types');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(toDto(complete));
    request.flush({ ...existing, id: 9 });

    expect(flash.take()).toEqual({ type: 'success', key: 'created' });
    expect(navigate).toHaveBeenCalledWith(['/resources/food', 9]);
  });

  it("keeps a meal time's own day outside the convention choosable, and won't remove an ordered one", async () => {
    const { component } = await render('3');
    const [ordered, february] = mealTimes(component);

    expect(ordered).toMatchObject({ id: 10, day: '2027-03-24', clock: '17:00', orderCount: 2 });
    expect(component.days(february)[0]).toBe('2027-02-03');
    component.removeMealTime(ordered);
    component.removeMealTime(february);
    expect(mealTimes(component).map(r => r.id)).toEqual([10]);
  });

  it('shows why an update failed when an ordered meal time was removed', async () => {
    const { component } = await render('3');

    component.submit();
    http.expectOne('/api/food-types/3').flush({ reason: 'mealTimeOrdered' }, { status: 409, statusText: 'Conflict' });

    expect(component.errorKey()).toBe('mealTimeOrdered');
    expect(component.busy()).toBe(false);
  });

  it('"Fortryd" goes back to the type with "Mad-typen blev ikke opdateret.", like Infosys', async () => {
    const { component, navigate, flash } = await render('3');

    component.cancel();

    expect(flash.take()).toEqual({ type: 'error', key: 'notUpdated' });
    expect(navigate).toHaveBeenCalledWith(['/resources/food', 3]);
  });

  it('asks before deleting, and goes back to the type with "Kunne ikke slette mad-typen" when it fails', async () => {
    const { fixture, component, navigate, flash } = await render('3');
    component.askDelete();
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('food.deleteConfirmTitle');

    component.confirmDelete();
    http.expectOne('/api/food-types/3').flush({ reason: 'inUse' }, { status: 409, statusText: 'Conflict' });

    expect(flash.take()).toEqual({ type: 'error', key: 'deleteFailed' });
    expect(navigate).toHaveBeenCalledWith(['/resources/food', 3]);
  });
});
