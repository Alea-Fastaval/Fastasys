import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { describe, expect, it } from 'vitest';
import { FoodTypeDetail } from '@shared/types';
import { AuthService } from '@shared/services';
import { FoodTypeDetailComponent } from './food-type-detail';

const food: FoodTypeDetail = {
  id: 3,
  nameDanish: 'Aftensmad - Pizza',
  nameEnglish: 'Dinner - Pizza',
  price: 68,
  isHidden: false,
  mealTimes: [
    {
      id: 10,
      time: '2027-03-24T16:00:00Z',
      descriptionDanish: 'Med salat',
      descriptionEnglish: 'With salad',
      orderCount: 0,
    },
  ],
};

describe('FoodTypeDetailComponent', () => {
  const render = async (privileges: string[], found = true) => {
    await TestBed.configureTestingModule({
      imports: [FoodTypeDetailComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: '3' }) } } },
      ],
    }).compileComponents();
    TestBed.inject(AuthService).currentUser.set({
      id: 1,
      username: 'info',
      email: '',
      firstName: '',
      lastName: '',
      roles: [],
      privileges,
    });
    const http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(FoodTypeDetailComponent);
    fixture.detectChanges();
    http.match('/assets/i18n/en.json').forEach(request => request.flush({}));
    const request = http.expectOne('/api/food-types/3');
    if (found) request.flush(food);
    else request.flush({}, { status: 404, statusText: 'Not found' });
    fixture.detectChanges();
    http.verify();
    return fixture.nativeElement as HTMLElement;
  };

  const text = (element: HTMLElement, selector: string) => element.querySelector(selector)?.textContent?.trim();

  it('shows the fields and the meal times (Infosys showfood.phtml)', async () => {
    const element = await render(['food_view']);

    expect(text(element, '.name-danish')).toBe('Aftensmad - Pizza');
    expect(text(element, '.name-english')).toBe('Dinner - Pizza');
    expect(text(element, '.price')).toBe('68 DKK');
    expect(text(element, '.hidden-type')).toBe('common.no');
    // 16:00 UTC is 17:00 in Copenhagen
    expect(text(element, '.meal-time .time')).toBe('common.weekdaysShort.Wednesday 17:00');
    expect(element.querySelector('.meal-time')?.textContent).toContain('Med salat');
    expect(element.querySelector('a[href="/resources/food/3/edit"]')).toBeNull();
  });

  it('links "ret/slet" for food_edit', async () => {
    const element = await render(['food_edit']);

    expect(element.querySelector('a[href="/resources/food/3/edit"]')).not.toBeNull();
  });

  it('says so for an unknown food type, like Infosys', async () => {
    const element = await render(['food_view'], false);

    expect(element.textContent).toContain('food.messages.notFound');
  });
});
