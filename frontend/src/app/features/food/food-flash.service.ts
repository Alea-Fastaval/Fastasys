import { Injectable, signal } from '@angular/core';

export interface FoodFlash {
  type: 'success' | 'error';
  // Translation key under food.messages
  key: string;
}

// Infosys shows a success/error message on the page it redirects to after a create, update or delete of a food type.
// The message is kept here until the next food page takes it.
@Injectable({
  providedIn: 'root',
})
export class FoodFlashService {
  private readonly pending = signal<FoodFlash | null>(null);

  public set(type: FoodFlash['type'], key: string): void {
    this.pending.set({ type, key });
  }

  public take(): FoodFlash | null {
    const flash = this.pending();
    this.pending.set(null);
    return flash;
  }
}
