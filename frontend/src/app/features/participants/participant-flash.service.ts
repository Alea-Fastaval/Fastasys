import { Injectable, signal } from '@angular/core';

export interface ParticipantFlash {
  type: 'success' | 'error';
  // Translation key under participants.messages
  key: string;
}

// Infosys shows a success/error message on the page it redirects to after a create, update, annul or delete.
// The message is kept here until the next participants page takes it.
@Injectable({
  providedIn: 'root',
})
export class ParticipantFlashService {
  private readonly pending = signal<ParticipantFlash | null>(null);

  public set(type: ParticipantFlash['type'], key: string): void {
    this.pending.set({ type, key });
  }

  public take(): ParticipantFlash | null {
    const flash = this.pending();
    this.pending.set(null);
    return flash;
  }
}
