// ID cards (Infosys idtemplates and the ID card rendering; see IdCardTemplatesController and IdCardsController in the
// API)

export type IdCardItemType = 'Photo' | 'Text' | 'Barcode';

// What a text item shows: ID, Navn, Dr. + Navn, Arbejdsområde, Scenarie or Ungdomsskole
export type IdCardDataSource = 'Id' | 'Name' | 'DoctorName' | 'OrganizerArea' | 'Game' | 'YouthClub';

export const ID_CARD_DATA_SOURCES: readonly IdCardDataSource[] = [
  'Id',
  'Name',
  'DoctorName',
  'OrganizerArea',
  'Game',
  'YouthClub',
];

// An item on a template; positions and sizes in pixels of the background, rotation in degrees clockwise
export interface IdCardTemplateItem {
  type: IdCardItemType;
  x: number;
  y: number;
  width: number;
  height: number;
  rotation: number;
  // Text items only
  dataSource: IdCardDataSource | null;
}

export interface IdCardTemplate {
  id: number;
  name: string;
  // The background's size in pixels; 0 without a background
  width: number;
  height: number;
  hasBackground: boolean;
  items: IdCardTemplateItem[];
}

// "Kategori-skabeloner": a participant category and its template
export interface CategoryIdCardTemplate {
  categoryId: number;
  name: string;
  templateId: number | null;
}

// One card to draw
export interface IdCard {
  participantId: number;
  templateId: number;
  hasPhoto: boolean;
  // The EAN-8 number, 8 digits
  barcode: string;
  texts: Record<IdCardDataSource, string>;
}

export interface IdCards {
  cards: IdCard[];
  templates: IdCardTemplate[];
}
