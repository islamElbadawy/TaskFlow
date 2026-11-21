import { Pipe, PipeTransform, inject } from '@angular/core';
import { parseUtcDate } from '../domain/due-date';
import { TranslationService } from './translation.service';

@Pipe({ name: 'dateFmt', pure: false })
export class DateFmtPipe implements PipeTransform {
  private readonly i18n = inject(TranslationService);

  transform(
    value: string | Date | null | undefined,
    options: Intl.DateTimeFormatOptions = { day: 'numeric', month: 'short', year: 'numeric' },
  ): string {
    if (!value) return '';
    const date = typeof value === 'string' ? parseUtcDate(value) : value;
    if (Number.isNaN(date.getTime())) return '';
    return this.i18n.formatDate(date, options);
  }
}
