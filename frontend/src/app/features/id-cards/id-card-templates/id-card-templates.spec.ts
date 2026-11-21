import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { CategoryIdCardTemplate, IdCardTemplate } from '@shared/types';
import { IdCardTemplatesComponent } from './id-card-templates';

const template = (overrides: Partial<IdCardTemplate> = {}): IdCardTemplate => ({
  id: 1,
  name: 'Arrangør27',
  width: 1011,
  height: 638,
  hasBackground: true,
  items: [{ type: 'Photo', x: 30, y: 40, width: 213, height: 295, rotation: 0, dataSource: null }],
  ...overrides,
});
const withoutBackground = template({ id: 2, name: 'Infonaut27', width: 0, height: 0, hasBackground: false, items: [] });
const categories: CategoryIdCardTemplate[] = [
  { categoryId: 5, name: 'Arrangør', templateId: 1 },
  { categoryId: 6, name: 'Deltager', templateId: null },
];

// Lets the awaited requests finish
const settle = () => new Promise(resolve => setTimeout(resolve));

describe('IdCardTemplatesComponent (Infosys id-templates)', () => {
  let httpMock: HttpTestingController;
  let fixture: ComponentFixture<IdCardTemplatesComponent>;
  let component: IdCardTemplatesComponent;
  let element: HTMLElement;

  beforeEach(async () => {
    // jsdom has no blob URLs
    URL.createObjectURL = vi.fn(() => 'blob:background');
    URL.revokeObjectURL = vi.fn();

    await TestBed.configureTestingModule({
      imports: [IdCardTemplatesComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();
    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(IdCardTemplatesComponent);
    component = fixture.componentInstance;
    element = fixture.nativeElement as HTMLElement;
    fixture.detectChanges();
    httpMock
      .match('/assets/i18n/en.json')
      .forEach(req => req.flush({ idCardTemplates: { renderSize: '{{widthMm}} x {{heightMm}} mm' } }));
    httpMock.expectOne('/api/id-card-templates').flush([template(), withoutBackground]);
    httpMock.expectOne('/api/id-card-templates/categories').flush(categories);
    fixture.detectChanges();
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
    httpMock.verify();
  });

  // Opens the template with the background
  const openFirst = async () => {
    component.activate(component.templates()[0]);
    httpMock.expectOne('/api/id-card-templates/1/background').flush(new Blob(['png'], { type: 'image/png' }));
    await settle();
    fixture.detectChanges();
  };

  it('lists the templates and the categories, and opens a template on its background', async () => {
    expect([...element.querySelectorAll('.template-name-button')].map(b => b.textContent?.trim())).toEqual([
      'Arrangør27',
      'Infonaut27',
    ]);
    expect([...element.querySelectorAll('.categories label')].map(l => l.firstChild?.textContent?.trim())).toEqual([
      'Arrangør',
      'Deltager',
    ]);

    await openFirst();

    expect(element.querySelector('img.background')?.getAttribute('src')).toBe('blob:background');
    expect(element.querySelectorAll('rect.item')).toHaveLength(1);
    // "Render size (at 300 DPI)"
    expect(element.querySelector('.render-size')?.textContent).toBe('86 x 54 mm');
  });

  it('cannot place items on a template without a background', () => {
    component.activate(withoutBackground);
    fixture.detectChanges();

    expect(element.querySelector('.no-background')).not.toBeNull();
    expect(element.querySelector<HTMLButtonElement>('.add-photo')?.disabled).toBe(true);
    component.addItem('Photo');
    expect(component.active()?.items).toEqual([]);
  });

  it('adds a template with the trimmed name and opens it', () => {
    component.newName = '   ';
    component.add();
    httpMock.expectNone('/api/id-card-templates');

    component.newName = '  Gæst27 ';
    component.add();
    const request = httpMock.expectOne('/api/id-card-templates');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ name: 'Gæst27' });
    request.flush({ ...withoutBackground, id: 3, name: 'Gæst27' });

    expect(component.activeId()).toBe(3);
    expect(component.newName).toBe('');
    expect(component.templates().map(t => t.name)).toEqual(['Arrangør27', 'Infonaut27', 'Gæst27']);
  });

  it('copies a template and opens the copy', async () => {
    component.copy(component.templates()[0]);
    const request = httpMock.expectOne('/api/id-card-templates/1/copy');
    expect(request.request.method).toBe('POST');
    request.flush(template({ id: 3, name: 'Copy of Arrangør27' }));
    httpMock.expectOne('/api/id-card-templates/3/background').flush(new Blob(['png'], { type: 'image/png' }));
    await settle();

    expect(component.activeId()).toBe(3);
    expect(component.active()?.items).toHaveLength(1);
  });

  it('asks before removing a template, and then reloads the categories', async () => {
    await openFirst();
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);
    component.remove(component.templates()[0]);
    httpMock.expectNone('/api/id-card-templates/1');
    expect(confirm).toHaveBeenCalledWith('idCardTemplates.confirmDelete');

    confirm.mockReturnValue(true);
    component.remove(component.templates()[0]);
    const request = httpMock.expectOne('/api/id-card-templates/1');
    expect(request.request.method).toBe('DELETE');
    request.flush(null);
    httpMock
      .expectOne('/api/id-card-templates/categories')
      .flush([{ ...categories[0], templateId: null }, categories[1]]);

    expect(component.activeId()).toBeNull();
    expect(component.templates().map(t => t.id)).toEqual([2]);
    expect(component.categories()[0].templateId).toBeNull();
  });

  it('saves the template 100 ms after the last change, as Infosys', async () => {
    await openFirst();
    vi.useFakeTimers();

    component.addItem('Text');
    vi.advanceTimersByTime(50);
    // The new text item is selected; typed past the card's edge, it stays on the card
    component.setPosition('x', 900);
    vi.advanceTimersByTime(99);
    httpMock.expectNone('/api/id-card-templates/1');
    vi.advanceTimersByTime(1);

    const request = httpMock.expectOne('/api/id-card-templates/1');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({
      name: 'Arrangør27',
      items: [
        { type: 'Photo', x: 30, y: 40, width: 213, height: 295, rotation: 0, dataSource: null },
        { type: 'Text', x: 811, y: 0, width: 200, height: 50, rotation: 0, dataSource: 'Id' },
      ],
    });
    request.flush(template());
  });

  it('saves a pending change at once when another template is opened', async () => {
    await openFirst();
    vi.useFakeTimers();

    component.rename('Arrangør 2027');
    component.activate(withoutBackground);

    const request = httpMock.expectOne('/api/id-card-templates/1');
    expect(request.request.body.name).toBe('Arrangør 2027');
    request.flush(template());
    expect(component.activeId()).toBe(2);
  });

  it('drags an item, keeping it on the card', async () => {
    await openFirst();
    vi.useFakeTimers();

    component.startDrag(0, new MouseEvent('mousedown', { screenX: 100, screenY: 100 }));
    document.dispatchEvent(new MouseEvent('mousemove', { screenX: 2100, screenY: 90 }));
    document.dispatchEvent(new MouseEvent('mouseup'));
    document.dispatchEvent(new MouseEvent('mousemove', { screenX: 0, screenY: 0 }));

    expect(component.active()?.items[0]).toMatchObject({ x: 1011 - 213, y: 30 });
    expect(component.selectedIndex()).toBe(0);
    vi.advanceTimersByTime(100);
    httpMock.expectOne('/api/id-card-templates/1').flush(template());
  });

  it('chooses what a text item shows, and removes the selected item', async () => {
    await openFirst();
    vi.useFakeTimers();

    component.addItem('Text');
    component.setDataSource('OrganizerArea');
    expect(component.selected()?.dataSource).toBe('OrganizerArea');
    component.removeSelected();
    expect(component.active()?.items).toHaveLength(1);
    expect(component.selectedIndex()).toBeNull();

    vi.advanceTimersByTime(100);
    expect(httpMock.expectOne('/api/id-card-templates/1').request.body.items).toHaveLength(1);
  });

  it('moves the items back onto a smaller background', async () => {
    await openFirst();
    const file = new File(['png'], 'baggrund.png', { type: 'image/png' });
    const done = component.setBackground({ target: { files: [file], value: 'baggrund.png' } } as unknown as Event);

    const upload = await vi.waitFor(() => httpMock.expectOne('/api/id-card-templates/1/background'));
    expect(upload.request.method).toBe('PUT');
    expect(upload.request.body).toEqual({ image: 'data:image/png;base64,cG5n' });
    upload.flush(template({ width: 500, height: 300 }));
    const background = await vi.waitFor(() => httpMock.expectOne('/api/id-card-templates/1/background'));
    expect(background.request.method).toBe('GET');
    background.flush(new Blob(['png'], { type: 'image/png' }));
    await done;

    const save = await vi.waitFor(() => httpMock.expectOne('/api/id-card-templates/1'));
    expect(save.request.body.items[0]).toMatchObject({ x: 30, y: 5, width: 213, height: 295 });
    save.flush(template());
  });

  it('gives a category its template, and reloads the categories when that fails', () => {
    component.setCategoryTemplate(categories[1], 1);
    const request = httpMock.expectOne('/api/id-card-templates/categories/6');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ templateId: 1 });
    request.flush(null);
    expect(component.categories()[1].templateId).toBe(1);

    component.setCategoryTemplate(categories[0], 2);
    httpMock
      .expectOne('/api/id-card-templates/categories/5')
      .flush({ message: 'Unknown template' }, { status: 400, statusText: 'Bad Request' });
    httpMock.expectOne('/api/id-card-templates/categories').flush(categories);
    expect(component.message()).toBe('categoryFailed');
  });

  it('says so when a change could not be saved', async () => {
    await openFirst();
    vi.useFakeTimers();

    component.rename('Arrangør 2027');
    vi.advanceTimersByTime(100);
    httpMock.expectOne('/api/id-card-templates/1').flush(null, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(element.querySelector('.flash.error')?.textContent).toContain('idCardTemplates.messages.saveFailed');
  });
});
