import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import {
  CreateTicketTypeDto,
  SignupStatus,
  TICKET_KINDS,
  TicketKind,
  TicketType,
  TicketTypeFieldsDto,
  UpdateTicketTypeDto,
} from '@shared/types';
import { Card } from '@shared/components';
import { TranslatePipe } from '@shared/pipes';
import { TranslationService } from '@shared/services';
import { TicketFlashService } from '../ticket-flash.service';
import { dayWithDate, ticketDayOptions, translatedLongDay } from '../ticket-texts';

// indgang.type is varchar(64)
export const MAX_TICKET_NAME_LENGTH = 64;

export interface TicketTypeForm {
  name: string;
  price: number | null;
  // "yyyy-mm-dd", or '' while not chosen
  day: string;
  kind: TicketKind | null;
  isAllConvention: boolean;
}

// Infosys's create form starts empty
export function newTicketTypeForm(): TicketTypeForm {
  return { name: '', price: null, day: '', kind: null, isAllConvention: false };
}

export function toForm(type: TicketType): TicketTypeForm {
  return {
    name: type.name,
    price: type.price,
    day: type.day,
    kind: type.kind,
    isAllConvention: type.isAllConvention,
  };
}

export function toDto(form: TicketTypeForm): TicketTypeFieldsDto {
  return {
    name: form.name,
    price: form.price,
    day: form.day === '' ? null : form.day,
    kind: form.kind,
    isAllConvention: form.isAllConvention,
  };
}

// The rules the API checks (TicketTypesController.Validate): a type that isn't empty or "0" (PHP empty()), at most 64
// characters, a whole price of 0 or more, a day and a kind. Infosys's edit form didn't check the price, and an empty
// start became 1 January 1970 (both fixed 2026-11-24).
export function canSubmitTicketType(form: TicketTypeForm): boolean {
  return (
    form.name !== '' &&
    form.name !== '0' &&
    form.name.length <= MAX_TICKET_NAME_LENGTH &&
    form.price !== null &&
    Number.isInteger(form.price) &&
    form.price >= 0 &&
    form.day !== '' &&
    form.kind !== null
  );
}

// Infosys indgang/createentry and indgang/editentry/:id: ("Opret indgang" / "Opdater indgang"), with the delete
// confirmation (displaydelete.phtml, "Slet indgang?"). Infosys's "Start" was a date picker that suggested the
// convention's days; here "Dag" is a dropdown of the convention's days (Convention:Start to Convention:End, agreed
// 2026-11-25), read from the signup status.
@Component({
  selector: 'app-ticket-type-form',
  imports: [
    FormsModule,
    RouterLink,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    Card,
    TranslatePipe,
  ],
  templateUrl: './ticket-type-form.html',
  styleUrl: './ticket-type-form.scss',
})
export class TicketTypeFormComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly flash = inject(TicketFlashService);

  public readonly kinds = TICKET_KINDS;
  public readonly maxNameLength = MAX_TICKET_NAME_LENGTH;
  public readonly typeId = signal<number | null>(null);
  public readonly form = signal<TicketTypeForm | null>(null);
  public readonly notFound = signal(false);
  public readonly busy = signal(false);
  public readonly errorKey = signal<string | null>(null);
  public readonly confirmingDelete = signal(false);
  private readonly conventionStart = signal<string | null>(null);
  private readonly conventionEnd = signal<string | null>(null);
  // The day of the type being edited, which stays choosable when it is outside the convention
  private readonly originalDay = signal<string | null>(null);
  // Whether the convention's dates have been asked for (they may not be set, or the request may fail)
  public readonly datesLoaded = signal(false);
  public readonly days = computed(() =>
    ticketDayOptions(this.conventionStart(), this.conventionEnd(), this.originalDay()),
  );
  private readonly longDay = translatedLongDay(inject(TranslationService));

  public ngOnInit(): void {
    this.http.get<SignupStatus>('/api/signup/status').subscribe({
      next: status => {
        this.conventionStart.set(status.conventionStart);
        this.conventionEnd.set(status.conventionEnd);
        this.datesLoaded.set(true);
      },
      error: () => this.datesLoaded.set(true),
    });

    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.form.set(newTicketTypeForm());
      return;
    }

    this.typeId.set(Number(id));
    this.http.get<TicketType>(`/api/ticket-types/${id}`).subscribe({
      next: type => {
        this.originalDay.set(type.day);
        this.form.set(toForm(type));
      },
      error: () => this.notFound.set(true),
    });
  }

  // "Onsdag 24/03"
  public dayText(day: string): string {
    return dayWithDate(day, this.longDay);
  }

  public canSubmit(form: TicketTypeForm): boolean {
    return !this.busy() && canSubmitTicketType(form);
  }

  public submit(): void {
    const form = this.form();
    if (!form || !this.canSubmit(form)) return;

    const id = this.typeId();
    this.busy.set(true);
    this.errorKey.set(null);

    if (id === null) {
      this.create(toDto(form));
    } else {
      this.update(id, toDto(form));
    }
  }

  // Infosys "Fortryd" on the edit form reports "indgangs-typen blev ikke opdateret." on the type's page
  public cancel(): void {
    this.flash.set('error', 'notUpdated');
    void this.router.navigate(['/resources/tickets', this.typeId()]);
  }

  public askDelete(): void {
    this.confirmingDelete.set(true);
  }

  // Infosys goes to its Indgang page after a delete, and back to the type when it fails
  public confirmDelete(): void {
    const id = this.typeId();
    if (id === null) return;

    this.busy.set(true);
    this.http.delete(`/api/ticket-types/${id}`).subscribe({
      next: () => {
        this.flash.set('success', 'deleted');
        void this.router.navigate(['/resources/tickets']);
      },
      error: () => {
        this.flash.set('error', 'deleteFailed');
        void this.router.navigate(['/resources/tickets', id]);
      },
    });
  }

  // Infosys "Fortryd" on the delete confirmation goes back to the type, without a message
  public cancelDelete(): void {
    void this.router.navigate(['/resources/tickets', this.typeId()]);
  }

  private create(dto: CreateTicketTypeDto): void {
    this.http.post<TicketType>('/api/ticket-types', dto).subscribe({
      next: created => {
        this.flash.set('success', 'created');
        void this.router.navigate(['/resources/tickets', created.id]);
      },
      error: () => this.fail('createFailed'),
    });
  }

  private update(id: number, dto: UpdateTicketTypeDto): void {
    this.http.put<TicketType>(`/api/ticket-types/${id}`, dto).subscribe({
      next: () => {
        this.flash.set('success', 'updated');
        void this.router.navigate(['/resources/tickets', id]);
      },
      error: () => this.fail('updateFailed'),
    });
  }

  // The form keeps what was typed, like the activity form (Infosys went to another page)
  private fail(key: string): void {
    this.busy.set(false);
    this.errorKey.set(key);
  }
}
