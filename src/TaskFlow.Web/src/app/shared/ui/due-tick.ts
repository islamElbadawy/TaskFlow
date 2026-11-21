import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { TaskStatus } from '../../core/api/models';
import { isOverdue, overdueDays, parseUtcDate } from '../../core/domain/due-date';
import { TranslationService } from '../../core/i18n/translation.service';

@Component({
  selector: 'app-due-tick',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (!dueDate()) {
      <span class="text-slate whitespace-nowrap">{{ i18n.t('noDueDate') }}</span>
    } @else {
      <span
        class="inline-flex items-center gap-2 whitespace-nowrap"
        [class.text-heat]="late()"
        [class.font-bold]="late()"
        [title]="fullDate()"
      >
        <b
          class="num text-[11px] leading-none w-6 h-6 rounded-md grid place-items-center border font-medium"
          [class.border-heat/30]="late()"
          [class.bg-heat-soft]="late()"
          [class.text-heat]="late()"
          [class.border-line]="!late()"
          [class.bg-panel]="!late()"
          [class.text-slate]="!late()"
        >
          {{ dayNumber() }}
        </b>
        <span>{{ text() }}</span>
      </span>
    }
  `,
})
export class AppDueTick {
  protected readonly i18n = inject(TranslationService);

  readonly dueDate = input<string | null>(null);
  readonly status = input<TaskStatus>('Todo');

  private readonly parsed = computed(() => {
    const raw = this.dueDate();
    return raw ? parseUtcDate(raw) : null;
  });

  protected readonly late = computed(() => isOverdue(this.dueDate(), this.status()));

  protected readonly dayNumber = computed(() => {
    const d = this.parsed();
    return d ? this.i18n.formatNumber(d.getDate()) : '';
  });

  protected readonly text = computed(() => {
    const d = this.parsed();
    if (!d) return '';
    if (this.late()) {
      const days = overdueDays(this.dueDate(), this.status());
      return days === 0 ? this.i18n.t('overdueToday') : this.i18n.dict().daysOverdue(days);
    }
    return this.i18n.formatDate(d, { weekday: 'short' });
  });

  protected readonly fullDate = computed(() => {
    const d = this.parsed();
    return d ? this.i18n.formatDate(d, { dateStyle: 'full' }) : '';
  });
}
