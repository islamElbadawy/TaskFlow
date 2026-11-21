import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { TranslationService } from '../../core/i18n/translation.service';
import { AppButton } from './button';

@Component({
  selector: 'app-paginator',
  imports: [AppButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (totalCount() > 0) {
      <div class="flex flex-wrap items-center justify-between gap-3 mt-4 text-[14px]">
        <span class="text-slate">{{ rangeText() }}</span>
        <div class="flex items-center gap-2">
          <app-button
            variant="quiet"
            size="sm"
            [disabled]="!hasPreviousPage()"
            (click)="pageChange.emit(pageNumber() - 1)"
          >
            {{ i18n.t('previous') }}
          </app-button>
          <app-button
            variant="quiet"
            size="sm"
            [disabled]="!hasNextPage()"
            (click)="pageChange.emit(pageNumber() + 1)"
          >
            {{ i18n.t('next') }}
          </app-button>

          @if (showPageSize()) {
            <label class="ms-3 text-slate inline-flex items-center gap-2">
              <span>{{ i18n.t('rows') }}</span>
              <select
                class="tf-transition h-9 px-2.5 rounded-[var(--radius-control)] bg-panel border border-line text-ink text-[14px] num hover:border-slate/50"
                [value]="pageSize()"
                (change)="onSizeChange($event)"
              >
                @for (size of pageSizeOptions(); track size) {
                  <option [value]="size" [selected]="size === pageSize()">
                    {{ i18n.formatNumber(size) }}
                  </option>
                }
              </select>
            </label>
          }
        </div>
      </div>
    }
  `,
})
export class AppPaginator {
  protected readonly i18n = inject(TranslationService);

  readonly pageNumber = input.required<number>();
  readonly pageSize = input.required<number>();
  readonly totalCount = input.required<number>();
  readonly hasPreviousPage = input(false);
  readonly hasNextPage = input(false);
  readonly showPageSize = input(true);
  readonly pageSizeOptions = input<readonly number[]>([20, 50, 100]);

  readonly pageChange = output<number>();
  readonly pageSizeChange = output<number>();

  protected readonly rangeText = computed(() => {
    const total = this.totalCount();
    if (total === 0) return '';
    const from = (this.pageNumber() - 1) * this.pageSize() + 1;
    const to = Math.min(this.pageNumber() * this.pageSize(), total);
    return this.i18n.dict().showingRange(from, to, total);
  });

  protected onSizeChange(event: Event): void {
    const value = Number((event.target as HTMLSelectElement).value);
    if (!Number.isNaN(value) && value > 0) {
      this.pageSizeChange.emit(value);
    }
  }
}
