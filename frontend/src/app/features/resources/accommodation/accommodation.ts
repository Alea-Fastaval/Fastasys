// todo: we need to look into how we store overnight stays, and how to model C#-ScheduleSignup to something in TS.
// todo: Upgrade table to show how many are in each room on each night

import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { TranslatePipe } from '@shared/pipes';
import { Room } from '@shared/types';

@Component({
  selector: 'app-accommodation',
  imports: [CommonModule, TranslatePipe],
  templateUrl: './accommodation.html',
  styleUrl: './accommodation.scss',
})
export class AccommodationComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly rooms = signal<Room[]>([]);
  private readonly dates = signal<Date[]>([]);

  ngOnInit(): void {
    this.loadData();
  }

  private loadData(): void {
    this.http.get<Room[]>('/api/rooms').subscribe(data => this.rooms.set(data.filter(r => r.isSleepingRoom)));
    this.http.get<Date[]>('/api/ConventionOptions/allDates').subscribe(data => this.dates.set(data));
  }

  private occupants(room: Room, time: Date) {
    return 44;
  }
}
