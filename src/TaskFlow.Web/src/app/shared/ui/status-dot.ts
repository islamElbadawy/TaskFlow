import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { TaskStatus } from '../../core/api/models';
import { TranslationService } from '../../core/i18n/translation.service';

const DOT_CLASS: Record<TaskStatus, string> = {
  Todo: 'bg-todo',
  InProgress: 'bg-flow',
  Done: 'bg-ink',
};

@Component({
  selector: 'app-status-dot',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span class="inline-flex items-center gap-2 whitespace-nowrap">
      <span class="size-2 rounded-full shrink-0" [class]="dotClass()" aria-hidden="true"></span>
      <span>{{ label() }}</span>
    </span>
  `,
})
export class AppStatusDot {
  private readonly i18n = inject(TranslationService);

  readonly status = input.required<TaskStatus>();

  protected readonly dotClass = computed(() => DOT_CLASS[this.status()]);
  protected readonly label = computed(() => {
    const d = this.i18n.dict();
    switch (this.status()) {
      case 'Todo':
        return d.statusTodo;
      case 'InProgress':
        return d.statusInProgress;
      case 'Done':
        return d.statusDone;
    }
  });
}
