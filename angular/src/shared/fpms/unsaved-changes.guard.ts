import { CanDeactivateFn } from '@angular/router';

export interface LeavePrompt {
    hasUnsavedChanges(): boolean;
}

export const unsavedChangesGuard: CanDeactivateFn<unknown> = (component) => {
    const page = component as Partial<LeavePrompt> | null;
    if (!page?.hasUnsavedChanges?.()) {
        return true;
    }
    return new Promise<boolean>((resolve) =>
        abp.message.confirm('Your changes on this page are not saved yet. Leave without saving?', 'Leave this page?', (ok: boolean) => resolve(!!ok))
    );
};
