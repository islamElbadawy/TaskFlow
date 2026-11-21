import { Pipe, PipeTransform, inject } from '@angular/core';
import { Dictionary, TranslationKey } from '../../../i18n/en';
import { TranslationService } from './translation.service';

type StringKey = {
  [K in TranslationKey]: Dictionary[K] extends string ? K : never;
}[TranslationKey];

@Pipe({ name: 't', pure: false })
export class TPipe implements PipeTransform {
  private readonly i18n = inject(TranslationService);

  transform(key: StringKey): string {
    return this.i18n.dict()[key];
  }
}
