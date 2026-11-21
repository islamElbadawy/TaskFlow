import { Pipe, PipeTransform, inject } from '@angular/core';
import { TranslationService } from './translation.service';

@Pipe({ name: 'num', pure: false })
export class NumPipe implements PipeTransform {
  private readonly i18n = inject(TranslationService);

  transform(value: number | null | undefined): string {
    if (value === null || value === undefined || Number.isNaN(value)) return '';
    return this.i18n.formatNumber(value);
  }
}
