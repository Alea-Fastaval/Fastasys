import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController, TestRequest } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { describe, beforeEach, afterEach, it, expect, vi } from 'vitest';
import { AuthService } from '@shared/services';
import { ParticipantRow } from '@shared/types';
import { ParticipantFlashService } from './participant-flash.service';
import { ParticipantsComponent } from './participants';

const rows: ParticipantRow[] = Array.from({ length: 30 }, (_, i) => ({
  id: i + 1,
  name: i === 0 ? 'Anna Hansen' : `Deltager ${i + 1}`,
  values: { city: i % 2 === 0 ? 'Aarhus' : 'Odense', adminNote: '' },
}));

describe('ParticipantsComponent (Infosys deltager/visalle and search_result)', () => {
  let httpMock: HttpTestingController;
  let canEdit: boolean;

  beforeEach(async () => {
    canEdit = true;
    await TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'participants', component: ParticipantsComponent }]),
      ],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
    vi.spyOn(TestBed.inject(AuthService), 'hasPrivilege').mockImplementation(() => canEdit);
  });

  afterEach(() => {
    httpMock.verify();
  });

  // Lets the page fetch, remove unknown values from the URL and show the result
  const settle = async (harness: RouterTestingHarness) => {
    harness.detectChanges();
    await harness.fixture.whenStable();
    harness.detectChanges();
  };

  const search = () => httpMock.expectOne('/api/participants/search');

  const open = async (url: string) => {
    const harness = await RouterTestingHarness.create();
    const component = await harness.navigateByUrl(url, ParticipantsComponent);
    await settle(harness);
    httpMock.match('/assets/i18n/en.json').forEach(req => req.flush({}));
    return { harness, component, element: harness.routeNativeElement as HTMLElement };
  };

  const answer = async (harness: RouterTestingHarness, req: TestRequest, data = rows) => {
    req.flush({ directId: null, rows: data });
    await settle(harness);
  };

  it('lists everyone 25 at a time with links to the participant', async () => {
    const { harness, element, component } = await open('/participants');
    const req = search();
    expect(req.request.body).toEqual({ steps: [], columns: [] });
    await answer(harness, req);

    expect(element.querySelectorAll('tbody tr').length).toBe(25);
    expect(element.querySelector('tbody a')?.getAttribute('href')).toBe('/participants/1');
    expect(element.querySelector('h1')?.textContent).toContain('participants.allParticipants');

    component.goTo(1);
    await settle(harness);
    expect(element.querySelectorAll('tbody tr').length).toBe(5);
  });

  it('reads the searches and the columns from the URL', async () => {
    const steps = encodeURIComponent('[{"quick":"anna"}]');
    const { harness, element } = await open(`/participants?search=${steps}&columns=city`);
    const req = search();
    expect(req.request.body).toEqual({
      steps: [{ quick: 'anna', criteria: null, logic: 'And', combination: 'New' }],
      columns: ['city'],
    });
    await answer(harness, req, rows.slice(0, 1));

    expect(element.querySelector('h1')?.textContent).toContain('participants.list.searchTitle');
    expect(element.querySelector('.reset-search strong')?.textContent).toBe('anna');
    expect(element.querySelector('tbody tr')?.textContent).toContain('Aarhus');
  });

  it('filters the rows on the full name (fixed)', async () => {
    const { harness, element } = await open('/participants?filter=anna%20hansen');
    await answer(harness, search());

    expect(element.querySelectorAll('tbody tr').length).toBe(1);
  });

  it('adds and removes columns through the URL', async () => {
    const { harness, component } = await open('/participants');
    await answer(harness, search());

    component.toggleColumn('city');
    await settle(harness);
    expect(TestBed.inject(Router).url).toBe('/participants?columns=city');
    const withCity = search();
    expect(withCity.request.body.columns).toEqual(['city']);
    await answer(harness, withCity);

    component.resetColumns();
    await settle(harness);
    expect(TestBed.inject(Router).url).toBe('/participants');
    await answer(harness, search());
  });

  it('starts a new search, or combines it with the searches so far', async () => {
    const { harness, component } = await open('/participants');
    await answer(harness, search());

    component.search({ criteria: { city: 'Aarhus' }, logic: 'And', combination: 'New' });
    await settle(harness);
    const first = search();
    expect(first.request.body.steps).toEqual([
      { quick: null, criteria: { city: 'Aarhus' }, logic: 'And', combination: 'New' },
    ]);
    // The column of the field searched on is added
    expect(first.request.body.columns).toEqual(['city']);
    await answer(harness, first);

    component.search({ criteria: { firstName: 'Anna' }, logic: 'Or', combination: 'Intersection' });
    await settle(harness);
    const second = search();
    expect(second.request.body.steps.length).toBe(2);
    expect(second.request.body.columns).toEqual(['city', 'firstName']);
    await answer(harness, second);

    component.resetSearch();
    await settle(harness);
    expect(search().request.body.steps).toEqual([]);
  });

  it('does not offer the notes to users who may only view participants', async () => {
    canEdit = false;
    const { harness, component } = await open('/participants?columns=city,adminNote');
    const req = search();
    expect(req.request.body.columns).toEqual(['city']);
    await answer(harness, req);

    expect(component.availableColumns().some(c => c.column === 'adminNote')).toBe(false);
  });

  it('removes a search it does not understand from the URL and says so', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => undefined);
    const { harness, component } = await open('/participants?search=nonsense');
    // Before and after the unknown value is removed, the page asks for everyone
    httpMock.match('/api/participants/search').forEach(req => req.flush({ directId: null, rows }));
    await settle(harness);

    expect(TestBed.inject(Router).url).toBe('/participants');
    expect(component.unknownQueryValues()).toBe('search=nonsense');
  });

  it('shows the message from the page it came from', async () => {
    TestBed.inject(ParticipantFlashService).set('error', 'createFailed');
    const { harness, element } = await open('/participants');
    await answer(harness, search());

    expect(element.querySelector('.flash.error')).not.toBeNull();
  });
});
