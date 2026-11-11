import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { describe, it, expect } from 'vitest';
import { CriteriaSearchStep } from '@shared/types';
import { ParticipantSearchFormComponent, newSearchForm, toSearchStep } from './participant-search-form';

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
  });
});
