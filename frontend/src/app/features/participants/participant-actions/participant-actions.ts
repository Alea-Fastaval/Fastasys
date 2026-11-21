import { Component, Input } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { Participant } from '@shared/types';
import { TranslatePipe } from '@shared/pipes';

// One of the buttons at the top of the Infosys participant page
export type ParticipantAction =
  'printCharacterSheet' | 'checkIn' | 'checkInAndPrint' | 'sendSignupEmail' | 'registerBankTransfer' | 'paymentMode';

// The buttons Infosys shows above the participant page (visdeltager.phtml, .prev-next-buttons), in its order. "Vis
// forrige" and "Vis næste" are in the page header. Who sees what:
// - Print spillerseddel, Send tilmeldings email and Betalings mode: everyone who can open the page in Infosys; Send
//   tilmeldings email is shown to participant editors only here, since Infosys's Read-only role can't use it.
// - Check in and Check in & print: Infosys's Infonaut and Admin roles, when the participant isn't checked in and isn't
//   annulled. Here participant editors until the check-in privilege is added (agreed 2026-11-11).
// - Registrer bankoverførsel: in Betalings mode only. Shown here so it isn't forgotten; it moves into Betalings mode
//   when that is ported.
export function participantActions(p: Participant): ParticipantAction[] {
  const actions: ParticipantAction[] = ['printCharacterSheet'];
  if (p.details && p.details.checkedInAt === null && !p.details.isAnnulled) {
    actions.push('checkIn', 'checkInAndPrint');
  }
  if (p.canEdit) {
    actions.push('sendSignupEmail', 'registerBankTransfer');
  }
  actions.push('paymentMode');
  return actions;
}

// The buttons at the top of the Infosys participant page. None of them is ported yet, so they are shown disabled with
// "Ikke flyttet til Fastasys endnu." How to port each one:
//
// Print spillerseddel (also the "Spillerseddel" link in the Personlig info box): opens Infosys deltager/spillersedler/
// {id} (ParticipantController::spillerSedler, displayCharacterSheet.phtml with printsheet.phtml) in a new tab: a
// printable sheet with the participant's schedule and an EAN-8 barcode. It takes a range of ids ("12-14") too.
//
// Check in (ParticipantController::checkinAjax "mark-checkedin", ParticipantModel::markCheckedin, participant.js
// checkIn):
// - The page takes the amount owed from the Pris breakdown ("Difference ved check-ind": CheckInTotal - AmountPaid).
//   If it isn't 0 it asks first: "Deltageren har et udestående med Fastaval." with "Deltageren skal betale {x} kr.
//   ekstra" or "Deltageren skal have {x} kr. tilbage", and "Marker betalt og fortsæt" / "Fortryd". Continuing
//   appends "Deltageren har betalt {x} ved checkin {date}" to the Økonomi-note; Fortryd doesn't check in.
// - The server refuses a participant who is already checked in ("Fejl: deltageren (ID: x) er allerede tjekket ind."),
//   sets the check-in time to now and Udeblevet to nej, and when AmountPaid differs from CheckInTotal sets "Afregnet
//   ved check-in" to the difference and AmountPaid to CheckInTotal (otherwise "Afregnet" is 0). It logs
//   "Deltager #{id} blev checked ind af {user}".
// - The page then shows "Checkin gennemført" (or "Kunne ikke tjekke deltageren ind." with the error), fills Checked in
//   and Afregnet ved check-in, and hides both check-in buttons.
// - In Fastasys, work the amount out on the server, not from the page, and write the note on the server too.
//   Infosys's client-side date has a bug ("-0" before every month, so October is "-010").
// - Infosys also asks participant/has-vouchers first (game master seats on activities that aren't board games, plus
//   Extra vouchers) but ignores the answer, and markCheckedin's voucher message uses an undefined $vouchers, so it
//   never shows. Ask the user before showing it.
//
// Check in & print: Check in, then opens the Spillerseddel in a new tab.
//
// Send tilmeldings email (ParticipantController::sendSignupEmail): sends the participant the signup confirmation
// again (participant/sendsignupemail.phtml, or sendsignupemailen.phtml for participants who don't speak Danish), with
// their wear, activity wishes and hero force signups and the pay-by date. Infosys does it on a GET opened in a new tab
// that answers "Email sent" whether or not the mail went out; make it a POST and report failures.
//
// Registrer bankoverførsel (ParticipantController::registerBankTransfer, ParticipantModel::registerBankTransfer): a
// dialog with Dato (today), ID (the transfer's id), Beløb (the signup's original price), Gebyr (the bank transfer fee
// ticket, ticked) and Total, and "Opdatér". It adds the fee ticket to the participant, adds Total to AmountPaid and
// appends "Bank transfer of {total}DKK transferred on {date} with ID: {id}" to the Økonomi-note. Infosys takes the
// total from the page; work it out on the server.
//
// Betalings mode / Normal mode (?payment_edit=true): the same page for registering payments. It hides the lower part
// of Personlig info, the boxes next to it except Pris breakdown, and the boxes below the notes; it keeps the notes and
// shows Registrer bankoverførsel. The quick search keeps the mode.
@Component({
  selector: 'app-participant-actions',
  imports: [MatButtonModule, TranslatePipe],
  templateUrl: './participant-actions.html',
  styleUrl: './participant-actions.scss',
})
export class ParticipantActionsComponent {
  // A decorator input, like Card's: the unit tests run Angular in JIT mode, which doesn't know signal inputs
  @Input({ required: true }) public participant!: Participant;

  public actions(): ParticipantAction[] {
    return participantActions(this.participant);
  }
}
