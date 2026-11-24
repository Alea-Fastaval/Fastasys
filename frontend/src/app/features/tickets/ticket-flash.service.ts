import { Injectable, signal } from '@angular/core';

export interface TicketFlash {
  type: 'success' | 'error';
  // Translation key under tickets.messages
  key: string;
}

// Infosys shows a success/error message on the page it redirects to after a create, update or delete of a ticket
// type. The message is kept here until the next ticket type page takes it.
@Injectable({
  providedIn: 'root',
})
export class TicketFlashService {
  private readonly pending = signal<TicketFlash | null>(null);

  public set(type: TicketFlash['type'], key: string): void {
    this.pending.set({ type, key });
  }

  public take(): TicketFlash | null {
    const flash = this.pending();
    this.pending.set(null);
    return flash;
  }
}
