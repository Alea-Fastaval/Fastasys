import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { afterEach, describe, expect, it } from 'vitest';
import { WearAttribute } from '@shared/types';
import { WearAttributesComponent, isChanged, toRow } from './wear-attributes';

const attributes: WearAttribute[] = [
  { id: 13, kind: 'Size', nameDanish: 'M', nameEnglish: 'M', position: 13 },
  { id: 111, kind: 'Color', nameDanish: 'Sort', nameEnglish: 'Black', position: 1 },
  { id: 12, kind: 'Size', nameDanish: 'S', nameEnglish: 'S', position: 12 },
];

describe('WearAttributesComponent (Infosys wear/attributes)', () => {
  let http: HttpTestingController;

  afterEach(() => http.verify());

  const render = async () => {
    await TestBed.configureTestingModule({
      imports: [WearAttributesComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(WearAttributesComponent);
    fixture.detectChanges();
    http.match('/assets/i18n/en.json').forEach(request => request.flush({}));
    http.expectOne('/api/wear-attributes').flush(attributes);
    fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, element: fixture.nativeElement as HTMLElement };
  };

  it('shows a table per kind, the attributes by position, and "Gem" only on a changed row', async () => {
    const { fixture, component, element } = await render();

    const kinds = [...element.querySelectorAll('.kind')].map(kind => [
      kind.getAttribute('data-kind'),
      [...kind.querySelectorAll<HTMLInputElement>('.name-danish')].map(input => input.value),
    ]);
    // ngModel writes the inputs after a tick; the rows are what the inputs show
    expect(kinds.map(k => k[0])).toEqual(['Model', 'Design', 'Color', 'Size']);
    expect(component.rowsOf('Size').map(r => r.nameDanish)).toEqual(['S', 'M']);
    expect(element.querySelectorAll('.save').length).toBe(0);

    component.rowsOf('Color')[0].nameEnglish = 'Black!';
    fixture.detectChanges();
    expect(element.querySelectorAll('.save').length).toBe(1);
  });

  it('saves a new row and keeps its id, so saving it again changes it (Infosys added a second one)', async () => {
    const { component } = await render();

    component.addRow('Model');
    const row = component.rowsOf('Model')[0];
    expect(row).toMatchObject({ id: null, nameDanish: 'wear.newAttributeDanish' });
    row.nameDanish = 'Herre';
    row.nameEnglish = 'Men';
    component.save(row);
    const create = http.expectOne('/api/wear-attributes');
    expect(create.request.method).toBe('POST');
    expect(create.request.body).toEqual({ kind: 'Model', nameDanish: 'Herre', nameEnglish: 'Men' });
    create.flush({ id: 120, kind: 'Model', nameDanish: 'Herre', nameEnglish: 'Men', position: 1 });

    const saved = component.rowsOf('Model')[0];
    expect(saved.id).toBe(120);
    expect(isChanged(saved)).toBe(false);
    saved.nameDanish = 'Herrer';
    component.save(saved);
    const update = http.expectOne('/api/wear-attributes/120');
    expect(update.request.method).toBe('PUT');
    expect(update.request.body).toEqual({ nameDanish: 'Herrer', nameEnglish: 'Men' });
    update.flush({ id: 120, kind: 'Model', nameDanish: 'Herrer', nameEnglish: 'Men', position: 1 });
  });

  it('says when saving fails', async () => {
    const { fixture, component, element } = await render();

    const row = component.rowsOf('Size')[0];
    row.nameDanish = 'Small';
    component.save(row);
    http.expectOne('/api/wear-attributes/12').flush({}, { status: 500, statusText: 'Server error' });
    fixture.detectChanges();

    expect(element.querySelector('.save-failed')).not.toBeNull();
    expect(isChanged(component.rowsOf('Size')[0])).toBe(true);
  });

  it('marks a row changed until it is saved', () => {
    const row = toRow(attributes[0]);
    expect(isChanged(row)).toBe(false);
    expect(isChanged({ ...row, nameDanish: 'Medium' })).toBe(true);
    expect(isChanged({ ...row, id: null })).toBe(true);
  });
});
