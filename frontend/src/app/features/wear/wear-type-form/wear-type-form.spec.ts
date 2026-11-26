import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ParticipantCategory, WearAttribute, WearImage, WearTypeDetail } from '@shared/types';
import { WearFlashService } from '../wear-flash.service';
import {
  WearTypeForm,
  WearTypeFormComponent,
  canSubmitWearType,
  freeCategories,
  linkedImageIds,
  newWearTypeForm,
  toDto,
  toForm,
} from './wear-type-form';

const herre: WearAttribute = { id: 101, kind: 'Model', nameDanish: 'Herre', nameEnglish: 'Men', position: 1 };
const sort: WearAttribute = { id: 111, kind: 'Color', nameDanish: 'Sort', nameEnglish: 'Black', position: 1 };
const s: WearAttribute = { id: 12, kind: 'Size', nameDanish: 'S', nameEnglish: 'S', position: 12 };
const m: WearAttribute = { id: 13, kind: 'Size', nameDanish: 'M', nameEnglish: 'M', position: 13 };
const attributes = [herre, sort, s, m];

const categories: ParticipantCategory[] = [
  { id: 1, name: 'Deltager' },
  { id: 2, name: 'Arrangør' },
  { id: 3, name: 'Junior' },
];
const images: WearImage[] = [
  { id: 7, fileName: 'shirt.png' },
  { id: 8, fileName: 'cap.png' },
];

const existing: WearTypeDetail = {
  id: 1,
  nameDanish: 'T-shirt',
  nameEnglish: 'T-shirt',
  descriptionDanish: '',
  descriptionEnglish: '',
  maxOrder: 0,
  isRequired: false,
  variants: [[herre, sort, s, m]],
  prices: [
    { id: 4, participantCategoryId: 1, categoryName: 'Deltager', price: 150, orderCount: 2 },
    { id: 5, participantCategoryId: 2, categoryName: 'Arrangør', price: 100, orderCount: 0 },
  ],
  imageLinks: [
    { wearImageId: 7, attributeId: null },
    { wearImageId: 7, attributeId: 111 },
  ],
  orderCount: 2,
};

describe('wear type form rules', () => {
  const complete: WearTypeForm = { ...newWearTypeForm(), nameDanish: 'Hoodie' };

  it('starts like Infosys: one empty variant and Maks bestilling 0', () => {
    expect(newWearTypeForm()).toMatchObject({ variants: [[]], maxOrder: 0, prices: [] });
  });

  it('needs a navn, names of at most 64 and whole numbers of 0 or more', () => {
    expect(canSubmitWearType(newWearTypeForm())).toBe(false);
    expect(canSubmitWearType(complete)).toBe(true);
    expect(canSubmitWearType({ ...complete, nameDanish: '0' })).toBe(false);
    expect(canSubmitWearType({ ...complete, nameDanish: 'x'.repeat(65) })).toBe(false);
    expect(canSubmitWearType({ ...complete, nameEnglish: 'x'.repeat(65) })).toBe(false);
    // An empty Maks bestilling is saved as 0, like Infosys
    expect(canSubmitWearType({ ...complete, maxOrder: null })).toBe(true);
    expect(canSubmitWearType({ ...complete, maxOrder: -1 })).toBe(false);
    expect(canSubmitWearType({ ...complete, maxOrder: 1.5 })).toBe(false);
    const price = { participantCategoryId: 1, categoryName: 'Deltager', price: 0, orderCount: 0 };
    expect(canSubmitWearType({ ...complete, prices: [price] })).toBe(true);
    expect(canSubmitWearType({ ...complete, prices: [{ ...price, price: null }] })).toBe(false);
    expect(canSubmitWearType({ ...complete, prices: [{ ...price, price: -5 }] })).toBe(false);
  });

  it('turns the type into the form and back', () => {
    expect(toDto(toForm(existing))).toEqual({
      nameDanish: 'T-shirt',
      nameEnglish: 'T-shirt',
      descriptionDanish: '',
      descriptionEnglish: '',
      maxOrder: 0,
      isRequired: false,
      variants: [[101, 111, 12, 13]],
      prices: [
        { participantCategoryId: 1, price: 150 },
        { participantCategoryId: 2, price: 100 },
      ],
      imageLinks: existing.imageLinks,
    });
    expect(linkedImageIds(existing.imageLinks)).toEqual([7]);
  });

  it('offers the categories without a price, by name (wear.js sortCategories)', () => {
    expect(freeCategories(categories, toForm(existing).prices)).toEqual([{ id: 3, name: 'Junior' }]);
    expect(freeCategories(categories, []).map(c => c.name)).toEqual(['Arrangør', 'Deltager', 'Junior']);
  });
});

describe('WearTypeFormComponent', () => {
  let http: HttpTestingController;

  afterEach(() => http.verify());

  const render = async (id: string | null) => {
    // jsdom has no object URLs
    URL.createObjectURL = vi.fn(() => 'blob:image');
    URL.revokeObjectURL = vi.fn();
    await TestBed.configureTestingModule({
      imports: [WearTypeFormComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap(id ? { id } : {}) } } },
      ],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(WearTypeFormComponent);
    fixture.detectChanges();
    http.match('/assets/i18n/en.json').forEach(request => request.flush({}));
    http.expectOne('/api/wear-attributes').flush(attributes);
    http.expectOne('/api/wear-types/categories').flush(categories);
    http.expectOne('/api/wear-images').flush(images);
    if (id) http.expectOne(`/api/wear-types/${id}`).flush(existing);
    fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, navigate, flash: TestBed.inject(WearFlashService) };
  };

  const form = (component: WearTypeFormComponent): WearTypeForm => component.form() ?? newWearTypeForm();

  it('ticks attributes in variants, adds a variant and a price, and creates the type', async () => {
    const { fixture, component, navigate, flash } = await render(null);
    const element = fixture.nativeElement as HTMLElement;

    // Every attribute by kind in each variant
    expect(element.querySelectorAll('.variant').length).toBe(1);
    expect([...element.querySelectorAll('.variant h4')].map(h => h.textContent)).toEqual([
      'wear.kinds.Model',
      'wear.kinds.Color',
      'wear.kinds.Size',
    ]);
    component.toggleAttribute(form(component), 0, s, true);
    component.toggleAttribute(form(component), 0, m, true);
    component.toggleAttribute(form(component), 0, s, false);
    component.addVariant(form(component));
    component.newCategoryId = 2;
    component.newPrice = 90;
    component.addPrice(form(component));
    component.form.set({ ...form(component), nameDanish: 'Hoodie' });

    component.submit();
    const request = http.expectOne('/api/wear-types');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toMatchObject({
      nameDanish: 'Hoodie',
      variants: [[13], []],
      prices: [{ participantCategoryId: 2, price: 90 }],
    });
    request.flush({ ...existing, id: 9 });

    expect(flash.take()).toEqual({ type: 'success', key: 'created', params: undefined });
    expect(navigate).toHaveBeenCalledWith(['/wear/types', 9]);
  });

  it("won't remove an ordered price, and shows why an update was refused", async () => {
    const { component } = await render('1');

    const [ordered, organizer] = form(component).prices;
    component.removePrice(form(component), ordered);
    component.removePrice(form(component), organizer);
    expect(form(component).prices.map(p => p.categoryName)).toEqual(['Deltager']);

    component.submit();
    http
      .expectOne('/api/wear-types/1')
      .flush({ reason: 'priceOrdered', categoryName: 'Arrangør' }, { status: 409, statusText: 'Conflict' });

    expect(component.errorKey()).toBe('priceOrdered');
    expect(component.errorParams()).toEqual({ category: 'Arrangør' });
    expect(component.busy()).toBe(false);
  });

  it('links images to attributes or "Alle" (wear.js)', async () => {
    const { component } = await render('1');

    expect(component.addedImageIds()).toEqual([7]);
    expect(component.availableImages()).toEqual([{ id: 8, fileName: 'cap.png' }]);
    component.openImageDialog();
    component.previewInDialog(8);
    http.expectOne('/api/wear-images/8/file').flush(new Blob(['x']));
    expect(component.previewUrls()[8]).toBe('blob:image');
    component.closeImageDialog(true);
    expect(component.addedImageIds()).toEqual([7, 8]);

    // An image can be shown for the attributes ticked in a variant
    expect(component.imageAttributeGroups(form(component), 8).map(g => g.kind)).toEqual(['Model', 'Color', 'Size']);
    component.toggleLink(form(component), 8, 12, true);
    component.toggleLink(form(component), 7, null, false);
    expect(form(component).imageLinks).toEqual([
      { wearImageId: 7, attributeId: 111 },
      { wearImageId: 8, attributeId: 12 },
    ]);
  });

  it('"Fortryd" reports that nothing was saved, like Infosys', async () => {
    const { component, navigate, flash } = await render('1');

    component.cancel();

    expect(flash.take()).toEqual({ type: 'error', key: 'notUpdated', params: undefined });
    expect(navigate).toHaveBeenCalledWith(['/wear/types', 1]);
  });

  it('"Fortryd" on the create form reports "Wear typen blev ikke oprettet."', async () => {
    const { component, navigate, flash } = await render(null);

    component.cancel();

    expect(flash.take()).toEqual({ type: 'error', key: 'notCreated', params: undefined });
    expect(navigate).toHaveBeenCalledWith(['/wear']);
  });

  it('deletes after the confirmation, and shows the number of orders when refused', async () => {
    const { component, navigate, flash } = await render('1');

    component.askDelete();
    component.confirmDelete();
    http
      .expectOne('/api/wear-types/1')
      .flush({ reason: 'inUse', orderCount: 2 }, { status: 409, statusText: 'Conflict' });

    expect(flash.take()).toEqual({ type: 'error', key: 'inUse', params: { count: '2' } });
    expect(navigate).toHaveBeenCalledWith(['/wear/types', 1]);

    component.confirmDelete();
    http.expectOne('/api/wear-types/1').flush({});
    expect(flash.take()).toEqual({ type: 'success', key: 'deleted', params: undefined });
    expect(navigate).toHaveBeenCalledWith(['/wear']);
  });
});
