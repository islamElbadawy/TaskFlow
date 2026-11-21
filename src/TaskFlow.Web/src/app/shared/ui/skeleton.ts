import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { TranslationService } from '../../core/i18n/translation.service';

export type SkeletonKind = 'dashboard' | 'list' | 'board' | 'task';

@Component({
  selector: 'app-skeleton',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div aria-busy="true">
      <span class="sr-only">{{ i18n.t('loading') }}</span>

      @switch (kind()) {
        @case ('dashboard') {
          <div class="tf-skel h-24 rounded-2xl mt-8"></div>
          <div class="grid lg:grid-cols-[1fr_300px] gap-6 mt-8">
            <div class="tf-skel h-64 rounded-2xl"></div>
            <div class="tf-skel h-64 rounded-2xl"></div>
          </div>
        }
        @case ('board') {
          <div class="flex gap-5 overflow-x-auto pb-4">
            @for (col of [0, 1, 2]; track col) {
              <div class="w-[86vw] sm:w-[310px] shrink-0 space-y-2.5">
                <div class="tf-skel h-1.5 rounded-full"></div>
                <div class="tf-skel h-5 w-24 mb-3"></div>
                @for (card of [0, 1, 2]; track card) {
                  <div class="tf-skel h-[88px] rounded-xl"></div>
                }
              </div>
            }
          </div>
        }
        @case ('task') {
          <div class="grid lg:grid-cols-[1fr_280px] gap-10 mt-4">
            <div class="space-y-3">
              <div class="tf-skel h-9 w-2/3"></div>
              <div class="tf-skel h-4 w-full mt-6"></div>
              <div class="tf-skel h-4 w-5/6"></div>
              <div class="tf-skel h-4 w-3/5"></div>
            </div>
            <div class="space-y-4">
              @for (item of [0, 1, 2, 3]; track item) {
                <div class="tf-skel h-10"></div>
              }
            </div>
          </div>
        }
        @default {
          <div class="bg-panel border border-line rounded-2xl divide-y divide-line">
            @for (row of [0, 1, 2, 3, 4]; track row) {
              <div class="flex items-center gap-4 px-5 h-14">
                <div class="tf-skel h-4 flex-1 max-w-72"></div>
                <div class="tf-skel h-4 w-24 hidden sm:block"></div>
                <div class="tf-skel size-6 rounded-full ms-auto"></div>
              </div>
            }
          </div>
        }
      }
    </div>
  `,
})
export class AppSkeleton {
  protected readonly i18n = inject(TranslationService);
  readonly kind = input<SkeletonKind>('list');
}
