import { A11yModule } from '@angular/cdk/a11y';
import { DIALOG_DATA, Dialog, DialogModule, DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { TranslationService } from '../../core/i18n/translation.service';
import { AppButton } from './button';

export interface ConfirmDialogOptions {
  readonly title: string;
  readonly body?: string;
  readonly confirmLabel: string;
  readonly cancelLabel?: string;
  readonly danger?: boolean;
  /** When set, the user must type this exact string (case-insensitive) to enable confirm. */
  readonly typedConfirmation?: string;
  readonly typedPrompt?: string;
}

@Component({
  selector: 'app-confirm-dialog',
  imports: [A11yModule, AppButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      cdkTrapFocus
      [cdkTrapFocusAutoCapture]="true"
      class="tf-menu-in bg-panel text-ink border border-line rounded-2xl shadow-2xl p-6 w-[min(440px,calc(100vw-2rem))]"
      role="alertdialog"
      aria-modal="true"
      [attr.aria-labelledby]="'confirm-dialog-title'"
    >
      <h2 id="confirm-dialog-title" class="text-lg font-bold">{{ data.title }}</h2>
      @if (data.body) {
        <p class="text-slate mt-2 text-[14px]">{{ data.body }}</p>
      }

      @if (data.typedConfirmation) {
        <div class="mt-5">
          <label class="block text-[14px] mb-2" for="confirm-typed-input">
            {{ data.typedPrompt || i18n.dict().typeEmailConfirm(data.typedConfirmation) }}
          </label>
          <input
            id="confirm-typed-input"
            type="text"
            dir="ltr"
            autocomplete="off"
            class="tf-transition h-10 px-3 rounded-[var(--radius-control)] bg-panel border border-line text-[14px] w-full text-start hover:border-slate/50"
            [placeholder]="data.typedConfirmation"
            [value]="typed()"
            (input)="onTyped($event)"
          />
        </div>
      }

      <div class="flex justify-end gap-2 mt-7">
        <app-button variant="quiet" (click)="close(false)">
          {{ data.cancelLabel || i18n.t('cancel') }}
        </app-button>
        <app-button
          [variant]="data.danger ? 'danger-solid' : 'primary'"
          [disabled]="!canConfirm()"
          (click)="close(true)"
        >
          {{ data.confirmLabel }}
        </app-button>
      </div>
    </div>
  `,
})
export class ConfirmDialogComponent {
  protected readonly data = inject<ConfirmDialogOptions>(DIALOG_DATA);
  protected readonly i18n = inject(TranslationService);
  private readonly dialogRef = inject<DialogRef<boolean>>(DialogRef);

  protected readonly typed = signal('');
  protected readonly canConfirm = computed(() => {
    const required = this.data.typedConfirmation;
    if (!required) return true;
    return this.typed().trim().toLowerCase() === required.trim().toLowerCase();
  });

  protected onTyped(event: Event): void {
    this.typed.set((event.target as HTMLInputElement).value);
  }

  protected close(confirmed: boolean): void {
    this.dialogRef.close(confirmed);
  }
}

@Injectable({ providedIn: 'root' })
export class ConfirmDialogService {
  private readonly dialog = inject(Dialog);

  async confirm(options: ConfirmDialogOptions): Promise<boolean> {
    const ref = this.dialog.open<boolean, ConfirmDialogOptions>(ConfirmDialogComponent, {
      data: options,
      backdropClass: ['bg-black/45', 'backdrop-blur-[2px]'],
      panelClass: ['focus:outline-none'],
      autoFocus: 'first-tabbable',
      restoreFocus: true,
    });
    const result = await firstValueFrom(ref.closed);
    return result === true;
  }
}

export { DialogModule };
