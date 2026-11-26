// The parts of a day a participant can say they can help the hero force, in this order
export type HeroPeriod = 'Morning' | 'Afternoon' | 'Evening';

export const HERO_PERIODS: readonly HeroPeriod[] = ['Morning', 'Afternoon', 'Evening'];

// The hours Infosys shows for each part of the day (Danish time)
export const HERO_PERIOD_HOURS: Record<HeroPeriod, string> = {
  Morning: '7-12',
  Afternoon: '12-17',
  Evening: '17-24',
};

// When the participant can help: a category (null is any category, "Alle"), a day and a part of it
export interface HeroAvailability {
  categoryId: number | null;
  categoryName: string | null;
  // yyyy-MM-dd, a day in Denmark
  date: string;
  period: HeroPeriod;
}

// A hero force task category (Infosys gdscategories)
export interface HeroTaskCategory {
  id: number;
  nameDanish: string;
}

// The participant's edit page ("Opdater Heltestyrke Tilmeldinger")
export interface ParticipantHeroAvailability {
  participantId: number;
  availability: HeroAvailability[];
  categories: HeroTaskCategory[];
}

export interface HeroAvailabilityFieldsDto {
  categoryId: number | null;
  date: string;
  period: HeroPeriod;
}

export interface UpdateParticipantHeroAvailabilityDto {
  availability: HeroAvailabilityFieldsDto[];
}
