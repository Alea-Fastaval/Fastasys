// A row of "Mad-typer" (Infosys mad)
export interface FoodTypeSummary {
  id: number;
  // Infosys kategori
  nameDanish: string;
  // Infosys title_en
  nameEnglish: string;
  // Whole DKK; 0 is a free meal
  price: number;
  // "Skjult": not offered in the signup
  isHidden: boolean;
}

// A food type with its meal times (Infosys mad/show/:id:)
export interface FoodTypeDetail extends FoodTypeSummary {
  mealTimes: MealTime[];
}

// A time the food type is served (Infosys madtider)
export interface MealTime {
  id: number;
  // UTC instant; the pages show it in Copenhagen time
  time: string;
  descriptionDanish: string;
  descriptionEnglish: string;
  // How many participants have ordered it; an ordered meal time can't be removed
  orderCount: number;
}

// The fields of the create and edit form; a meal time without an id is new
export interface FoodTypeFieldsDto {
  nameDanish: string;
  nameEnglish: string;
  price: number | null;
  isHidden: boolean;
  mealTimes: MealTimeFieldsDto[];
}

export interface MealTimeFieldsDto {
  id: number | null;
  // UTC instant (ISO)
  time: string | null;
  descriptionDanish: string;
  descriptionEnglish: string;
}

export type CreateFoodTypeDto = FoodTypeFieldsDto;
export type UpdateFoodTypeDto = FoodTypeFieldsDto;

// A food type on the statistics page: ordered and handed out per meal time, and the total
export interface FoodStats {
  id: number;
  nameDanish: string;
  total: number;
  mealTimes: FoodStatsMealTime[];
}

export interface FoodStatsMealTime {
  id: number;
  time: string;
  total: number;
  received: number;
}

// A food type with its meal times, for the participant's edit page and the search box's Mad section
export interface FoodTypeOption {
  id: number;
  nameDanish: string;
  mealTimes: MealTimeOption[];
}

export interface MealTimeOption {
  id: number;
  time: string;
}

// A meal of the participant on the edit page
export interface ParticipantMealRow {
  mealTimeId: number;
  foodName: string;
  time: string;
  isReceived: boolean;
}

// The Mad part of Infosys's "Rediger indgang, mad og wear"
export interface ParticipantMeals {
  participantId: number;
  meals: ParticipantMealRow[];
  foodTypes: FoodTypeOption[];
}

export interface UpdateParticipantMealsDto {
  mealTimeIds: number[];
}

// One select of the search box's Mad section: "ja" (has) or "nej" (hasn't)
export interface MealChoice {
  mealTimeId: number;
  has: boolean;
}
