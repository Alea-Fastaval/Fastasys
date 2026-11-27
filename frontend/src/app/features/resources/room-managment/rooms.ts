import { Component, signal, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { CreateRoomDto, Room, UpdateRoomDto } from '@shared/types';
import { Card } from '@shared/components';
import { TranslatePipe } from '@shared/pipes';

@Component({
  selector: 'app-rooms',
  imports: [CommonModule, Card, TranslatePipe],
  templateUrl: './rooms.html',
  styleUrl: './rooms.scss',
})
export class RoomsComponent implements OnInit {
  private readonly http = inject(HttpClient);

  public rooms = signal<Room[]>([]);

  ngOnInit(): void {
    this.loadData();
  }

  private loadData(): void {
    this.http.get<Room[]>('/api/rooms').subscribe(data => this.rooms.set(data));
  }

  private getRoomDetails(id: number) {
    this.http.get<Room>(`/api/rooms/details/${id}`).subscribe();
  }

  private createRoom(body: CreateRoomDto): void {
    this.http.post<Room>('/api/rooms', body).subscribe();
  }

  private deleteRoom(id: number): void {
    this.http.delete<any>(`/api/rooms/delete/${id}`).subscribe();
  }

  private updateRoom(body: UpdateRoomDto, id: number): void {
    this.http.patch<Room>(`/api/rooms/update/${id}`, body).subscribe();
  }

  //todo:
  // Display a list of all rooms, with their "static" information
  // Make a create room button/input box
  // Make a delete room button, with some sort of accident protection
  // Make an edit room button/box

  // Additional room‑specific actions can be added here
}
