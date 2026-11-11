import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { describe, afterEach, it, expect, vi } from 'vitest';
import { ParticipantQuickSearchComponent } from './participant-quick-search';

describe('ParticipantQuickSearchComponent (the Infosys page header search)', () => {
  let httpMock: HttpTestingController;

  afterEach(() => {
    httpMock.verify();
  });

  const setup = async () => {
    await TestBed.configureTestingModule({
      imports: [ParticipantQuickSearchComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();
    httpMock = TestBed.inject(HttpTestingController);
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(ParticipantQuickSearchComponent);
    fixture.detectChanges();
    httpMock.match('/assets/i18n/en.json').forEach(req => req.flush({}));
    return { component: fixture.componentInstance, navigate };
  };

  it('does nothing for an empty field or "0", like PHP empty()', async () => {
    const { component, navigate } = await setup();

    component.term.set('');
    component.submit();
    component.term.set('0');
    component.submit();

    httpMock.expectNone('/api/participants/search');
    expect(navigate).not.toHaveBeenCalled();
  });

  it('opens the participant the search goes straight to', async () => {
    const { component, navigate } = await setup();
    component.term.set('27000425');

    component.submit();
    const req = httpMock.expectOne('/api/participants/search');
    expect(req.request.body).toEqual({
      steps: [{ quick: '27000425', criteria: null, logic: 'And', combination: 'New' }],
      columns: [],
    });
    req.flush({ directId: 42, rows: [] });

    expect(navigate).toHaveBeenCalledWith(['/participants', 42]);
    expect(component.term()).toBe('');
  });

  it('lists the participants found otherwise', async () => {
    const { component, navigate } = await setup();
    component.term.set('anna');

    component.submit();
    httpMock.expectOne('/api/participants/search').flush({ directId: null, rows: [] });

    expect(navigate).toHaveBeenCalledWith(['/participants'], { queryParams: { search: '[{"quick":"anna"}]' } });
  });
});
