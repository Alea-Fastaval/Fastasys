import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { describe, it, expect } from 'vitest';
import { Participant, ParticipantDetails } from '@shared/types';
import { ParticipantActionsComponent, participantActions } from './participant-actions';

// Only the fields the buttons look at matter here
const details = { checkedInAt: null, isAnnulled: false } as ParticipantDetails;
const editorView = { id: 7, canEdit: true, details } as Participant;
const viewerView = { id: 7, canEdit: false, details: null } as Participant;

describe('participantActions (the buttons at the top of the Infosys participant page)', () => {
  it('shows editors every button in the Infosys order', () => {
    expect(participantActions(editorView)).toEqual([
      'printCharacterSheet',
      'checkIn',
      'checkInAndPrint',
      'sendSignupEmail',
      'registerBankTransfer',
      'paymentMode',
    ]);
  });

  it('hides the check-in buttons once the participant is checked in, and for an annulled participant', () => {
    const checkedIn = { ...editorView, details: { ...details, checkedInAt: '2027-03-24T09:00:00Z' } };
    const annulled = { ...editorView, details: { ...details, isAnnulled: true } };

    expect(participantActions(checkedIn)).not.toContain('checkIn');
    expect(participantActions(checkedIn)).not.toContain('checkInAndPrint');
    expect(participantActions(annulled)).not.toContain('checkIn');
    expect(participantActions(annulled)).not.toContain('checkInAndPrint');
  });

  it('shows users who may only view participants the character sheet and the payment mode', () => {
    expect(participantActions(viewerView)).toEqual(['printCharacterSheet', 'paymentMode']);
  });
});

describe('ParticipantActionsComponent', () => {
  const render = async (participant: Participant) => {
    await TestBed.configureTestingModule({
      imports: [ParticipantActionsComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    const fixture = TestBed.createComponent(ParticipantActionsComponent);
    fixture.componentRef.setInput('participant', participant);
    fixture.detectChanges();
    TestBed.inject(HttpTestingController)
      .match('/assets/i18n/en.json')
      .forEach(req => req.flush({}));
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  };

  it('shows the buttons disabled, with a tooltip saying they are not ported yet', async () => {
    const element = await render(editorView);

    const actions = [...element.querySelectorAll<HTMLElement>('.action')];
    expect(actions.map(a => a.querySelector('button')?.textContent?.trim())).toEqual([
      'participants.actions.printCharacterSheet',
      'participants.actions.checkIn',
      'participants.actions.checkInAndPrint',
      'participants.actions.sendSignupEmail',
      'participants.actions.registerBankTransfer',
      'participants.actions.paymentMode',
    ]);
    expect(actions.every(a => a.querySelector('button')?.disabled)).toBe(true);
    expect(actions.every(a => a.title === 'participants.notPortedTitle')).toBe(true);
  });
});
