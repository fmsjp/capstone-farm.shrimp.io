import { Location } from '@angular/common';
import { inject } from '@angular/core';
import { ActivatedRoute, ParamMap, Router } from '@angular/router';

export class WizardSteps {
    private router = inject(Router);
    private route = inject(ActivatedRoute);
    private location = inject(Location);
    current: number;

    constructor(
        private readonly first: number,
        private readonly allowed: () => number,
        private readonly show: (step: number) => void
    ) {
        this.current = first;
    }

    reset(): void {
        this.current = this.first;
    }

    sync(q: ParamMap = this.route.snapshot.queryParamMap): void {
        const raw = Number(q.get('step'));
        const wanted = Number.isInteger(raw) && raw >= this.first ? raw : this.first;
        const step = Math.min(wanted, Math.max(this.first, this.allowed()));
        if (step !== wanted) {
            this.write(step, true);
            return;
        }
        if (step !== this.current) {
            this.current = step;
            this.show(step);
        }
    }

    go(step: number, replace = false): void {
        const state = this.location.getState() as { fpmsStep?: number } | null;
        if (!replace && step === this.current - 1 && state?.fpmsStep === this.current) {
            this.location.back();
            return;
        }
        this.write(step, replace);
    }

    private write(step: number, replace: boolean): void {
        this.router.navigate([], {
            relativeTo: this.route,
            queryParams: { step: step === this.first ? null : step },
            queryParamsHandling: 'merge',
            replaceUrl: replace,
            state: { fpmsStep: step },
        });
    }
}
