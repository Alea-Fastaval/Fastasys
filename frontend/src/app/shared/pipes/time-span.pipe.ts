import { Pipe, PipeTransform, inject } from '@angular/core';
import { TranslationService } from '@shared/services';
import { ConventionTime, formatClock, toConventionTime } from '@shared/utils';

// Shows a period in Copenhagen time with translated day names, like Infosys's replaceDayNames(date(...)).
// By default "Onsdag 10:00-14:00" (Infosys hold pages: 'D H:i'-'H:i'); with endWithDay
// "Onsdag 10:00-Onsdag 14:00" (Infosys activity page: 'D H:i'-'D H:i').
@Pipe({
  name: 'timeSpan',
  standalone: true,
  pure: false,
})
export class TimeSpanPipe implements PipeTransform {
  private readonly translationService = inject(TranslationService);

  public transform(start: string, end: string, endWithDay = false): string {
    // Registers the signal dependency so day names follow the language, like the translate pipe
    this.translationService.translations();

    const from = toConventionTime(start);
    const to = toConventionTime(end);
    const endText = endWithDay ? this.withDay(to) : formatClock(to);

    return `${this.withDay(from)}-${endText}`;
  }

  private withDay(time: ConventionTime): string {
    return `${this.translationService.translate(`common.weekdays.${time.weekday}`)} ${formatClock(time)}`;
  }
}
