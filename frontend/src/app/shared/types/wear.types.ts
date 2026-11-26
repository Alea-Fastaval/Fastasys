// The kinds of wear attributes, in the order Infosys shows them (Wear::getAttributeOrder)
export type WearAttributeKind = 'Model' | 'Design' | 'Color' | 'Size';

export const WEAR_ATTRIBUTE_KINDS: readonly WearAttributeKind[] = ['Model', 'Design', 'Color', 'Size'];

// A value a wear type comes in, e.g. the size "M" (Infosys wear_attributes)
export interface WearAttribute {
  id: number;
  kind: WearAttributeKind;
  nameDanish: string;
  nameEnglish: string;
  // The place within the kind
  position: number;
}

// A row of "Wear-typer"
export interface WearTypeSummary {
  id: number;
  nameDanish: string;
  descriptionDanish: string;
  position: number;
}

// A price of a wear type for a participant category
export interface WearPrice {
  id: number;
  participantCategoryId: number;
  categoryName: string;
  // Whole DKK
  price: number;
  // Orders made at the price (annulled participants' too); an ordered price can't be removed
  orderCount: number;
}

// An image shown for the wear type, for one of its attributes or for all of them ("Alle", attributeId null)
export interface WearImageLink {
  wearImageId: number;
  attributeId: number | null;
}

// A wear type with its variants, prices and image links (Infosys wear/viswear/:id: and the edit form)
export interface WearTypeDetail {
  id: number;
  nameDanish: string;
  nameEnglish: string;
  descriptionDanish: string;
  descriptionEnglish: string;
  // "Maks bestilling"; 0 means the signup's default of 10
  maxOrder: number;
  // "Påkrævet"
  isRequired: boolean;
  // Each variant's attributes in the order model, design, color, size
  variants: WearAttribute[][];
  prices: WearPrice[];
  imageLinks: WearImageLink[];
  // Orders of the type (annulled participants' too); a type with orders can't be deleted
  orderCount: number;
}

export interface WearPriceFieldsDto {
  participantCategoryId: number;
  price: number | null;
}

// The fields of the create and edit form; each variant is a list of attribute ids
export interface WearTypeFieldsDto {
  nameDanish: string;
  nameEnglish: string;
  descriptionDanish: string;
  descriptionEnglish: string;
  maxOrder: number | null;
  isRequired: boolean;
  variants: number[][];
  prices: WearPriceFieldsDto[];
  imageLinks: WearImageLink[];
}

export type CreateWearTypeDto = WearTypeFieldsDto;
export type UpdateWearTypeDto = WearTypeFieldsDto;

export interface MoveWearTypeDto {
  direction: 'Up' | 'Down';
}

// "Rediger wear egenskaber": Dansk and Engelsk
export interface WearAttributeFieldsDto {
  nameDanish: string;
  nameEnglish: string;
}

export interface CreateWearAttributeDto extends WearAttributeFieldsDto {
  kind: WearAttributeKind;
}

export type UpdateWearAttributeDto = WearAttributeFieldsDto;

// An uploaded image
export interface WearImage {
  id: number;
  fileName: string;
}

export interface CreateWearImageDto {
  fileName: string;
  // A data URL
  data: string;
}

// "Bestillings-oversigt"
export interface WearBreakdown {
  // The size columns, by position
  sizes: WearAttribute[];
  rows: WearBreakdownRow[];
}

export interface WearBreakdownRow {
  wearTypeId: number;
  wearTypeName: string;
  // The model, design and color of the row
  attributes: WearAttribute[];
  // The first row of a variant
  startsVariant: boolean;
  total: number;
  noSize: number;
  sizeCounts: WearSizeCount[];
}

export interface WearSizeCount {
  sizeId: number;
  count: number;
}

// An order on the order lists
export interface WearOrderLine {
  id: number;
  // The wear's name with its model/design/color, e.g. "T-shirt-Sort"
  name: string;
  // The category of the price it was ordered at
  priceCategoryName: string;
  amount: number;
  size: string | null;
  isReceived: boolean;
}

// "Alle bestillinger" / "Alle ikke-udleverede": a participant with their orders
export interface WearOrderListParticipant {
  participantId: number;
  // The chosen name
  name: string;
  categoryName: string;
  orders: WearOrderLine[];
}

// The list behind a number of "Bestillings-oversigt"
export interface WearMiniList {
  wearTypeId: number;
  wearTypeName: string;
  attributes: WearAttribute[];
  orders: WearMiniListOrder[];
}

export interface WearMiniListOrder {
  participantId: number;
  participantName: string;
  order: WearOrderLine;
}

export interface UpdateWearOrderReceivedDto {
  isReceived: boolean;
}

// "Wear-udlevering"
export interface WearHandoutInputDto {
  input: string;
}

export interface WearHandoutItem {
  name: string;
  amount: number;
  size: string | null;
}

export interface WearHandoutResult {
  participantId: number;
  name: string;
  // The participant's category
  categoryName: string;
  items: WearHandoutItem[];
}

// The Wear part of "Rediger indgang, mad og wear": an order of the participant
export interface ParticipantWearOrderRow {
  id: number;
  wearTypeId: number;
  wearPriceId: number;
  attributeIds: number[];
  name: string;
  size: string | null;
  categoryName: string;
  unitPrice: number;
  amount: number;
  isReceived: boolean;
}

// A wear type in the selects, with its prices and variants
export interface WearTypeOption {
  id: number;
  nameDanish: string;
  prices: WearPriceOption[];
  variants: WearAttribute[][];
}

export interface WearPriceOption {
  id: number;
  categoryName: string;
  price: number;
}

export interface ParticipantWear {
  participantId: number;
  orders: ParticipantWearOrderRow[];
  wearTypes: WearTypeOption[];
}

// An order the participant should have: with an id one of their orders (kept with its "Udleveret"), without one new
export interface ParticipantWearOrderFieldsDto {
  id: number | null;
  wearPriceId: number | null;
  attributeIds: number[] | null;
  amount: number;
}

export interface UpdateParticipantWearDto {
  orders: ParticipantWearOrderFieldsDto[];
}
