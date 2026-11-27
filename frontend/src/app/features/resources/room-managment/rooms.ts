import { Component, signal, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { CreateRoomDto, Room, UpdateRoomDto } from '@shared/types';
import { Card } from '@shared/components';
import { TranslatePipe } from '@shared/pipes';
import { MatButtonModule } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialog,
  MatDialogActions,
  MatDialogContent,
  MatDialogRef,
  MatDialogTitle,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';

@Component({
  selector: 'app-rooms',
  imports: [CommonModule, Card, TranslatePipe, MatButtonModule],
  templateUrl: './rooms.html',
  styleUrl: './rooms.scss',
})
export class RoomsComponent implements OnInit {
  private readonly http = inject(HttpClient);

  public rooms = signal<Room[]>([]);
  public readonly dialog = inject(MatDialog);

  ngOnInit(): void {
    this.loadData();
  }

  private loadData(): void {
    this.http.get<Room[]>('/api/rooms').subscribe(data => this.rooms.set(data.sort((a, b) => a.id - b.id)));
  }

  private getRoomDetails(id: number): Observable<Room> {
    return this.http.get<Room>(`/api/rooms/details/${id}`);
  }

  private openPopup(roomId: number): void {
    this.getRoomDetails(roomId).subscribe(room =>
      this.dialog
        .open(RoomOverviewUpdateDialog, {
          data: room,
        })
        .afterClosed()
        .subscribe(() => this.loadData()),
    );
  }

  private openPopupNewRoom() {
    const dialogRef = this.dialog.open(RoomCreateDialog, {});
    dialogRef.afterClosed().subscribe(() => this.loadData());
  }
}

@Component({
  selector: 'room-overview-update-dialog',
  templateUrl: 'room-overview-update-dialog.html',
  imports: [
    MatFormFieldModule,
    MatInputModule,
    FormsModule,
    MatButtonModule,
    MatDialogTitle,
    MatDialogContent,
    MatDialogActions,
    TranslatePipe,
  ],
})
export class RoomOverviewUpdateDialog {
  readonly dialogRef = inject(MatDialogRef<RoomOverviewUpdateDialog>);
  readonly room = inject<Room>(MAT_DIALOG_DATA);
  private readonly http = inject(HttpClient);

  private readonly name = this.room.name;
  private readonly location = this.room.location;
  private readonly capacity = this.room.capacity;
  private readonly isSleepingRoom = this.room.isSleepingRoom;
  private readonly canBook = this.room.canBook;
  private readonly description = this.room.description;

  private close(): void {
    this.dialogRef.close();
  }

  private gatherAndUpdateRoom() {
    const body: UpdateRoomDto = {
      name: this.name,
      location: this.location,
      capacity: this.capacity,
      isSleepingRoom: this.isSleepingRoom,
      canBook: this.canBook,
      description: this.description,
    };
    this.updateRoom(body, this.room.id);
  }

  private updateRoom(body: UpdateRoomDto, id: number): void {
    this.http.patch<Room>(`/api/rooms/update/${id}`, body).subscribe();
    this.close();
  }

  private deleteRoom(id: number): void {
    this.http.delete<any>(`/api/rooms/delete/${id}`).subscribe();
    this.close();
  }
}

@Component({
  selector: 'room-create-dialog',
  templateUrl: 'room-create-dialog.html',
  imports: [
    MatFormFieldModule,
    MatInputModule,
    FormsModule,
    MatButtonModule,
    MatDialogTitle,
    MatDialogContent,
    MatDialogActions,
    TranslatePipe,
  ],
})
export class RoomCreateDialog {
  readonly dialogRef = inject(MatDialogRef<RoomCreateDialog>);
  private readonly http = inject(HttpClient);

  private readonly name = '';
  private readonly location = '';
  private readonly capacity = 0;
  private readonly isSleepingRoom = false;
  private readonly canBook = false;
  private readonly description = '';

  private close(): void {
    this.dialogRef.close();
  }

  private gatherAndCreateRoom() {
    const body: CreateRoomDto = {
      name: this.name,
      location: this.location,
      capacity: this.capacity,
      isSleepingRoom: this.isSleepingRoom,
      canBook: this.canBook,
      description: this.description,
    };
    this.createRoom(body);
    this.close();
  }

  private createRoom(body: CreateRoomDto): void {
    this.http.post<Room>('/api/rooms', body).subscribe();
  }
}
