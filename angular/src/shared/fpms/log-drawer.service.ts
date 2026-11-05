import { Injectable, InjectionToken, signal } from '@angular/core';

export const EMBEDDED_QUERY = new InjectionToken<Record<string, string | null>>('EMBEDDED_QUERY');

export interface LogDrawerRequest {
    kind?: 'feeding' | 'log';
    batchId?: string | null;
    date?: string | null;
    logType?: number | null;
}

@Injectable({ providedIn: 'root' })
export class LogDrawerService {
    readonly request = signal<LogDrawerRequest | null>(null);

    embeddedPage: { hasUnsavedInput(): boolean } | null = null;

    readonly saved = signal(0);

    open(request: LogDrawerRequest): void {
        this.request.set({ ...request });
    }

    close(): void {
        this.request.set(null);
    }

    markSaved(): void {
        this.saved.update((n) => n + 1);
    }
}
