// What a ticket type (Infosys indgang) is. Infosys worked it out from words in the name; here it is a field
// (agreed 2026-11-24). More kinds come with the features that need them.
export const TICKET_KINDS = ['Entrance', 'Sleeping', 'AleaMembership', 'Other'] as const;
export type TicketKind = (typeof TICKET_KINDS)[number];

// A ticket type with the number of participants who have it (Infosys "Antal solgt")
export interface TicketType {
  id: number;
  name: string;
  // Whole DKK
  price: number;
  // "yyyy-mm-dd": one of the convention's days (Infosys start; only the day is kept). A partout type gets the first
  // day; the pages show "Partout" for it.
  day: string;
  kind: TicketKind;
  // "Partout": for the whole convention, not only for day
  isAllConvention: boolean;
  soldCount: number;
}

// The fields of the create and edit form
export interface TicketTypeFieldsDto {
  name: string;
  price: number | null;
  // null for a partout type: the API gives it the convention's first day
  day: string | null;
  kind: TicketKind | null;
  isAllConvention: boolean;
}

export type CreateTicketTypeDto = TicketTypeFieldsDto;
export type UpdateTicketTypeDto = TicketTypeFieldsDto;

// One heading of the statistics page: the ticket types with the same name, by day
export interface TicketStatsGroup {
  name: string;
  kind: TicketKind;
  isAllConvention: boolean;
  days: TicketStatsDay[];
}

export interface TicketStatsDay {
  day: string;
  count: number;
  // Shown as "Partout" instead of the day
  isAllConvention: boolean;
}

// A ticket type in a select or a list: the participant's ticket editor and the search box's Indgang section
export interface TicketTypeOption {
  id: number;
  name: string;
  day: string;
  isAllConvention: boolean;
}

// The Indgang part of Infosys's "Rediger indgang, mad og wear": the participant's tickets and every ticket type
export interface ParticipantTickets {
  participantId: number;
  tickets: TicketTypeOption[];
  ticketTypes: TicketTypeOption[];
}

export interface UpdateParticipantTicketsDto {
  ticketTypeIds: number[];
}

// One row of the search box's Indgang section: "ja" (has) or "nej" (hasn't)
export interface TicketChoice {
  ticketTypeId: number;
  has: boolean;
}
