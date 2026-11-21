import { Pipe, PipeTransform, inject } from '@angular/core';
import { parseUtcDate } from '../domain/due-date';
import { TranslationService } from './translation.service';

@Pipe({ name: 'relTime', pure: false })
export class RelTimePipe implements PipeTransform {
  private readonly i18n = inject(TranslationService);

  transform(value: string | Date | null | undefined): string {
    if (!value) return '';
    const date = typeof value === 'string' ? parseUtcDate(value) : value;
    if (Number.isNaN(date.getTime())) return '';
    return this.i18n.formatRelativeTime(date);
  }
}
