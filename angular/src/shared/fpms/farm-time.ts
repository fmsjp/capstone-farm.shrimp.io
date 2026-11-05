import moment from 'moment-timezone';

export class FarmTime {
    static zone = 'Asia/Manila';

    static dayFirst = false;
    static use24Hour = false;

    static setZone(zone?: string): void {
        if (zone && moment.tz.zone(zone)) {
            FarmTime.zone = zone;
        }
    }

    static setDisplay(dateOrder?: number, use24Hour?: boolean): void {
        FarmTime.dayFirst = dateOrder === 2;
        FarmTime.use24Hour = !!use24Hour;
    }

    static get dateFormat(): string {
        return FarmTime.dayFirst ? 'D MMM YYYY' : 'MMM D, YYYY';
    }

    static get timeFormat(): string {
        return FarmTime.use24Hour ? 'HH:mm' : 'h:mm A';
    }

    static dateTime(value?: string | Date): string {
        if (!value) {
            return '';
        }
        return moment.utc(value).tz(FarmTime.zone).format(`${FarmTime.dateFormat} ${FarmTime.timeFormat}`);
    }

    static date(value?: string): string {
        if (!value) {
            return '';
        }
        const day = moment(String(value).substring(0, 10), 'YYYY-MM-DD', true);
        return day.isValid() ? day.format(FarmTime.dateFormat) : String(value);
    }

    static time(value?: string): string {
        if (!value) {
            return '';
        }
        return moment.utc(value).tz(FarmTime.zone).format(FarmTime.timeFormat);
    }

    static isoDateOf(value?: string): string {
        if (!value) {
            return '';
        }
        return moment.utc(value).tz(FarmTime.zone).format('YYYY-MM-DD');
    }

    static localDateOf(value?: string): string {
        if (!value) {
            return '';
        }
        return moment.utc(value).tz(FarmTime.zone).format(FarmTime.dateFormat);
    }

    static longDateNow(): string {
        return moment().tz(FarmTime.zone).format(`dddd, ${FarmTime.dateFormat}`);
    }

    static timeNow(): string {
        return moment().tz(FarmTime.zone).format(FarmTime.timeFormat);
    }

    static today(): string {
        return moment().tz(FarmTime.zone).format('YYYY-MM-DD');
    }

    static toLocalInput(value?: string | Date): string {
        return moment.utc(value ?? new Date()).tz(FarmTime.zone).format('YYYY-MM-DDTHH:mm');
    }

    static fromLocalInput(value: string): string {
        return moment.tz(value, 'YYYY-MM-DDTHH:mm', FarmTime.zone).toISOString();
    }

    static nowInput(): string {
        return moment().tz(FarmTime.zone).format('YYYY-MM-DDTHH:mm');
    }

    static addMonths(date: string, months: number): string {
        return moment(date, 'YYYY-MM-DD').add(months, 'months').format('YYYY-MM-DD');
    }

    static addDays(date: string, days: number): string {
        return moment(date, 'YYYY-MM-DD').add(days, 'days').format('YYYY-MM-DD');
    }
}

export function newGuid(): string {
    const c = (globalThis as any).crypto;
    if (c?.randomUUID) {
        return c.randomUUID();
    }
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (ch) => {
        const r = (Math.random() * 16) | 0;
        return (ch === 'x' ? r : (r & 0x3) | 0x8).toString(16);
    });
}

export const EMPTY_GUID = '00000000-0000-0000-0000-000000000000';
