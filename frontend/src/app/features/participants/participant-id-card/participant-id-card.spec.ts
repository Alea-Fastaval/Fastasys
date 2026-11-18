import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { describe, it, expect } from 'vitest';
import { ParticipantIdCard } from '@shared/types';
import { ParticipantIdCardComponent, idCardTemplateText } from './participant-id-card';

const defaultText = (name: string) => `Default (${name})`;

describe('idCardTemplateText ("Skabelon:" in the Infosys IDkort box)', () => {
  it("shows the participant's own template", () => {
    const card: ParticipantIdCard = { photoUrl: null, templateName: 'Infonaut18', defaultTemplateName: 'Arrangør18' };
    expect(idCardTemplateText(card, defaultText, 'ingen')).toBe('Infonaut18');
  });

  it("shows the category's template as the default", () => {
    const card: ParticipantIdCard = { photoUrl: null, templateName: null, defaultTemplateName: 'Arrangør18' };
    expect(idCardTemplateText(card, defaultText, 'ingen')).toBe('Default (Arrangør18)');
  });

  it('shows "Default (ingen)" when the category has no template', () => {
    const card: ParticipantIdCard = { photoUrl: null, templateName: null, defaultTemplateName: null };
    expect(idCardTemplateText(card, defaultText, 'ingen')).toBe('Default (ingen)');
  });
});

describe('ParticipantIdCardComponent', () => {
  const render = async (idCard: ParticipantIdCard | null) => {
    await TestBed.configureTestingModule({
      imports: [ParticipantIdCardComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    const fixture = TestBed.createComponent(ParticipantIdCardComponent);
    fixture.componentRef.setInput('idCard', idCard);
    fixture.detectChanges();
    TestBed.inject(HttpTestingController)
      .match('/assets/i18n/en.json')
      .forEach(req =>
        req.flush({ participants: { idCard: { defaultTemplate: 'Default ({{name}})', noTemplate: 'ingen' } } }),
      );
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  };

  it('says that ID cards are not ported yet while there is no data', async () => {
    const element = await render(null);

    expect(element.querySelector('.not-ported')?.textContent).toContain('participants.idCard.notPorted');
    expect(element.querySelector('.template')).toBeNull();
  });

  it('shows the photo and the template', async () => {
    const element = await render({ photoUrl: '/photos/7.jpg', templateName: null, defaultTemplateName: 'Arrangør18' });

    expect(element.querySelector('img.photo')?.getAttribute('src')).toBe('/photos/7.jpg');
    expect(element.querySelector('.template-label')?.textContent).toBe('participants.idCard.template');
    expect(element.querySelector('.template-name')?.textContent).toBe('Default (Arrangør18)');
    expect(element.querySelector('.not-ported')).toBeNull();
  });

  it('shows no photo when the participant has none', async () => {
    const element = await render({ photoUrl: null, templateName: null, defaultTemplateName: null });

    expect(element.querySelector('img')).toBeNull();
    expect(element.querySelector('.template-name')?.textContent).toBe('Default (ingen)');
  });
});
