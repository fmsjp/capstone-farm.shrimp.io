import { ChangeDetectionStrategy, Component, DestroyRef, effect, inject, signal } from '@angular/core';
import { FpmsApiService } from '@shared/fpms/api.service';

@Component({
    selector: 'fpms-progress',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    template: `
        <div class="fpms-progress no-print" [class.on]="visible()" aria-hidden="true"><span></span></div>
        <span class="sr-only" role="status" aria-live="polite">{{ visible() ? 'Working…' : '' }}</span>
    `,
})
export class ProgressBarComponent {
    private api = inject(FpmsApiService);
    readonly visible = signal(false);
    private timer: ReturnType<typeof setTimeout> | undefined;

    constructor() {
        effect(() => {
            const busy = this.api.pending() > 0;
            clearTimeout(this.timer);
            if (busy) {
                this.timer = setTimeout(() => this.visible.set(true), 250);
            } else {
                this.visible.set(false);
            }
        });
        inject(DestroyRef).onDestroy(() => clearTimeout(this.timer));
    }
}
