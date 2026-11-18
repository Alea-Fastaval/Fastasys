import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { IdCardTemplateOption, ParticipantIdCard } from '@shared/types';
import { ParticipantIdCardComponent, idCardTemplateText } from './participant-id-card';

const defaultText = (name: string) => `Default (${name})`;
const noTemplate: ParticipantIdCard = {
  photoUrl: null,
  templateId: null,
  templateName: null,
  defaultTemplateName: null,
};
const templates: IdCardTemplateOption[] = [
  { id: 3, name: 'Arrangør27' },
  { id: 4, name: 'Infonaut27' },
];

describe('idCardTemplateText ("Skabelon:" in the Infosys IDkort box)', () => {
  it("shows the participant's own template", () => {
    const card = { ...noTemplate, templateId: 4, templateName: 'Infonaut27', defaultTemplateName: 'Arrangør27' };
    expect(idCardTemplateText(card, defaultText, 'ingen')).toBe('Infonaut27');
  });

  it("shows the category's template as the default", () => {
    expect(idCardTemplateText({ ...noTemplate, defaultTemplateName: 'Arrangør27' }, defaultText, 'ingen')).toBe(
      'Default (Arrangør27)',
    );
  });

  it('shows "Default (ingen)" when the category has no template', () => {
    expect(idCardTemplateText(noTemplate, defaultText, 'ingen')).toBe('Default (ingen)');
  });
});

describe('ParticipantIdCardComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    // jsdom has no blob URLs
    URL.createObjectURL = vi.fn(() => 'blob:photo');
    URL.revokeObjectURL = vi.fn();
  });

  afterEach(() => httpMock.verify());

  const render = async (idCard: ParticipantIdCard) => {
    await TestBed.configureTestingModule({
      imports: [ParticipantIdCardComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();
    httpMock = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(ParticipantIdCardComponent);
    fixture.componentRef.setInput('idCard', idCard);
    fixture.componentRef.setInput('participantId', 7);
    fixture.componentRef.setInput('templates', templates);
    fixture.detectChanges();
    httpMock
      .match('/assets/i18n/en.json')
      .forEach(req =>
        req.flush({ participants: { idCard: { defaultTemplate: 'Default ({{name}})', noTemplate: 'ingen' } } }),
      );
    fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, element: fixture.nativeElement as HTMLElement };
  };

  it('fetches the photo with the login and shows it', async () => {
    const { fixture, element } = await render({ ...noTemplate, photoUrl: '/api/participants/7/photo/cropped?v=1' });

    const request = httpMock.expectOne('/api/participants/7/photo/cropped?v=1');
    expect(request.request.responseType).toBe('blob');
    request.flush(new Blob(['png'], { type: 'image/png' }));
    await new Promise(resolve => setTimeout(resolve));
    fixture.detectChanges();

    expect(element.querySelector('img.photo')?.getAttribute('src')).toBe('blob:photo');
  });

  it('shows no photo when the participant has none', async () => {
    const { element } = await render(noTemplate);

    expect(element.querySelector('img')).toBeNull();
    expect(element.querySelector('.template-name')?.textContent?.trim()).toBe('Default (ingen)');
  });

  it('links to the photo upload page and opens the ID card in a new tab, like Infosys', async () => {
    const { element } = await render(noTemplate);

    expect(element.querySelector('a.photo-upload')?.getAttribute('href')).toBe('/participants/7/photo');
    const card = element.querySelector('a.show-id-card');
    expect(card?.getAttribute('href')).toBe('/id-cards?ids=7');
    expect(card?.getAttribute('target')).toBe('_blank');
  });

  it('changes the template from a click on it, with "Default (...)" first', async () => {
    const { fixture, component, element } = await render({ ...noTemplate, defaultTemplateName: 'Arrangør27' });
    const changed = vi.fn();
    component.changed.subscribe(changed);

    element.querySelector<HTMLElement>('.template-name')?.click();
    fixture.detectChanges();
    const options = [...element.querySelectorAll('.template-select option')].map(o => o.textContent?.trim());
    expect(options).toEqual(['Default (Arrangør27)', 'Arrangør27', 'Infonaut27']);

    component.chooseTemplate(4);
    const request = httpMock.expectOne('/api/participants/7/fields/idCardTemplateId');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ value: 4 });
    request.flush({});

    expect(changed).toHaveBeenCalled();
    expect(component.editing()).toBe(false);
  });

  it('says so when the template could not be saved', async () => {
    const { fixture, component, element } = await render(noTemplate);

    component.startEdit();
    component.chooseTemplate(null);
    httpMock
      .expectOne('/api/participants/7/fields/idCardTemplateId')
      .flush({}, { status: 400, statusText: 'Bad Request' });
    fixture.detectChanges();

    expect(element.querySelector('.error')?.textContent).toContain('participants.idCard.saveFailed');
  });
});
