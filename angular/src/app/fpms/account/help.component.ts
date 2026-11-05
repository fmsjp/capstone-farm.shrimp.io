import { Component } from '@angular/core';
import { FpmsPage } from '@shared/fpms/fpms-page';
import { FarmTime } from '@shared/fpms/farm-time';
import { FPMS_IMPORTS } from '../shared-imports';

export const HELP_CONTACT = {
    builtBy: '[team name or member names]',
    whoAnswers: '[who answers]',
    phone: '[number]',
    phoneHours: '[hours]',
    email: '[address]',
    emailChecked: '[who checks it, how often]',
    messaging: '[app and name or number, if used]',
    inPerson: '[where and when, if the team visits the farm]',
    identityCheck: '[who checks your identity: a second Farm Owner or the agreed security question]',
    systemDown: '[what to do first, for example keep recording on paper sheets, then call]',
    replyWithin: '[time]',
    replyDays: '[days]',
    urgentWhen: '[when]',
};

@Component({
    standalone: true,
    imports: FPMS_IMPORTS,
    template: `
        <fpms-page-header [narrow]="true" title="Contact the developers" [subtitle]="intro"></fpms-page-header>
        <section class="content px-2 pb-3">
            <div class="container-fluid fpms-narrow">
                @if (hasBlanks) {
                    <div class="alert alert-light border small">Highlighted text is still to be filled in by the team.</div>
                }
                <div class="row">
                    <div class="col-lg-7">
                        <div class="card">
                            <div class="card-header"><h3 class="card-title"><i class="fas fa-phone" aria-hidden="true"></i>How to reach us</h3></div>
                            <div class="card-body">
                                <dl class="mb-0 help-dl">
                                    <dt>Phone or text</dt><dd><span [class.help-blank]="blank(c.phone)">{{ c.phone }}</span> · <span [class.help-blank]="blank(c.phoneHours)">{{ c.phoneHours }}</span></dd>
                                    <dt>Email</dt><dd><span [class.help-blank]="blank(c.email)">{{ c.email }}</span> · <span [class.help-blank]="blank(c.emailChecked)">{{ c.emailChecked }}</span></dd>
                                    <dt>Messaging app</dt><dd><span [class.help-blank]="blank(c.messaging)">{{ c.messaging }}</span></dd>
                                    <dt>In person</dt><dd><span [class.help-blank]="blank(c.inPerson)">{{ c.inPerson }}</span></dd>
                                </dl>
                            </div>
                        </div>
                        <div class="card">
                            <div class="card-header"><h3 class="card-title"><i class="fas fa-list" aria-hidden="true"></i>What to tell us</h3></div>
                            <div class="card-body">
                                <ul class="mb-0 pl-3">
                                    <li>Your name and the farm's name</li>
                                    <li>The page you were on, for example "Feeding" or "Logbooks"</li>
                                    <li>What you were doing and what you expected</li>
                                    <li>What happened, and any message shown on the screen</li>
                                    <li>The date and time it happened</li>
                                    <li>A photo of the screen, if you can take one</li>
                                </ul>
                            </div>
                        </div>
                        <div class="card">
                            <div class="card-header"><h3 class="card-title"><i class="fas fa-exclamation-circle" aria-hidden="true"></i>Locked out or urgent</h3></div>
                            <div class="card-body">
                                <p>If you cannot sign in, call <span [class.help-blank]="blank(c.phone)">{{ c.phone }}</span>. <span [class.help-blank]="blank(c.identityCheck)">{{ c.identityCheck }}</span> You will get a temporary password and set a new one when you sign in.</p>
                                <p>If the system is down, <span [class.help-blank]="blank(c.systemDown)">{{ c.systemDown }}</span>.</p>
                                <p class="mb-0 font-weight-bold">Never send your password to anyone, including us.</p>
                            </div>
                        </div>
                        <div class="card">
                            <div class="card-header"><h3 class="card-title"><i class="far fa-clock" aria-hidden="true"></i>What to expect</h3></div>
                            <div class="card-body">
                                We reply within <span [class.help-blank]="blank(c.replyWithin)">{{ c.replyWithin }}</span> on <span [class.help-blank]="blank(c.replyDays)">{{ c.replyDays }}</span>. Urgent calls are answered <span [class.help-blank]="blank(c.urgentWhen)">{{ c.urgentWhen }}</span>.
                            </div>
                        </div>
                    </div>
                    <div class="col-lg-5">
                        <div class="card">
                            <div class="card-header"><h3 class="card-title"><i class="fas fa-info-circle" aria-hidden="true"></i>Support details</h3></div>
                            <div class="card-body">
                                <dl class="help-dl">
                                    @for (d of details(); track d.label) {
                                        <dt>{{ d.label }}</dt><dd>{{ d.value }}</dd>
                                    }
                                </dl>
                                <button type="button" class="btn btn-default" (click)="copy()">Copy these details</button>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </section>
    `,
    styles: [
        `
            .help-dl dt {
                font-size: 13px;
                font-weight: 500;
                color: #596872;
            }
            .help-dl dd {
                margin-bottom: 0.75rem;
            }
            .help-blank {
                background: #fff3c4;
                border-radius: 4px;
                padding: 0 3px;
            }
        `,
    ],
})
export class HelpComponent extends FpmsPage {
    readonly c = HELP_CONTACT;

    get intro(): string {
        return `Farm.Shrimp.io was built by ${this.c.builtBy}. For help with the system, contact ${this.c.whoAnswers}.`;
    }

    get hasBlanks(): boolean {
        return Object.values(this.c).some((v) => this.blank(v));
    }

    blank(v: string): boolean {
        return /^\[.*\]$/.test(v.trim());
    }

    details(): { label: string; value: string }[] {
        const tenant = this.session.tenant;
        const user = this.session.user;
        const role = !tenant ? 'System Administrator' : this.isOwner ? 'Farm Owner' : 'Farm Technician';
        const rows = [];
        if (tenant) {
            rows.push({ label: 'Farm', value: `${tenant.name} (${tenant.tenancyName})` });
        }
        rows.push({ label: 'Signed in as', value: `${user ? [user.name, user.surname].filter((x) => !!x).join(' ') || user.userName : ''}, ${role}` });
        rows.push({ label: 'App version', value: this.session.application?.version ?? '' });
        rows.push({ label: 'Date and time', value: `${FarmTime.longDateNow()}, ${FarmTime.timeNow()}, farm time` });
        return rows;
    }

    async copy(): Promise<void> {
        const text = this.details()
            .map((d) => `${d.label}: ${d.value}`)
            .join('\n');
        try {
            await navigator.clipboard.writeText(text);
            this.notifySaved('Support details copied.');
        } catch {
            abp.message.info(text, 'Support details');
        }
    }
}
