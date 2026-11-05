export interface SectionTab {
    label: string;
    route: string;
    permission?: string;
}

export interface Section {
    key: string;
    label: string;
    tabs: SectionTab[];
    prefixes: string[];
    action?: SectionAction;
}

export interface SectionAction {
    label: string;
    route: string;
    icon: string;
    permission: string;
    onTabs: string[];
}

export const SECTIONS: Section[] = [
    {
        key: 'operations',
        label: 'Operations',
        prefixes: ['/app/logbook'],
        tabs: [
            { label: 'Feeding (AM/PM)', route: '/app/logbook/feeding', permission: 'Farm.Logs' },
            { label: 'Record operations', route: '/app/logbook/new', permission: 'Farm.Logs.Create' },
            { label: 'Daily log status', route: '/app/logbook/status', permission: 'Farm.Logs' },
            { label: 'Logbooks', route: '/app/logbook', permission: 'Farm.Logs' },
            { label: 'Printable log sheets', route: '/app/logbook/sheets', permission: 'Farm.Logs' },
        ],
    },
    {
        key: 'production',
        label: 'Production',
        prefixes: ['/app/production'],
        tabs: [
            { label: 'Cycles', route: '/app/production/cycles', permission: 'Farm.Production' },
            { label: 'Batches', route: '/app/production/batches', permission: 'Farm.Production' },
        ],
        action: {
            label: 'Stock a batch',
            route: '/app/production/create-batch',
            icon: 'fas fa-shrimp',
            permission: 'Farm.Production.Stock',
            onTabs: ['/app/production/cycles', '/app/production/batches'],
        },
    },
    {
        key: 'harvest',
        label: 'Harvest and sales',
        prefixes: ['/app/harvests', '/app/sales'],
        tabs: [
            { label: 'Harvests', route: '/app/harvests', permission: 'Farm.Harvests' },
            { label: 'Sales', route: '/app/sales', permission: 'Farm.Sales' },
        ],
    },
    {
        key: 'resources',
        label: 'Resources',
        prefixes: ['/app/resources', '/app/inventory'],
        tabs: [
            { label: 'Hatchery sources', route: '/app/resources/hatcheries', permission: 'Farm.Hatcheries' },
            { label: 'Fry deliveries', route: '/app/resources/fry-deliveries', permission: 'Farm.FryDeliveries' },
            { label: 'Buyers', route: '/app/resources/buyers', permission: 'Farm.Buyers' },
            { label: 'Inventory', route: '/app/inventory', permission: 'Farm.Inventory' },
        ],
    },
    {
        key: 'farm',
        label: 'Farm',
        prefixes: ['/app/farm'],
        tabs: [
            { label: 'Profile', route: '/app/farm/profile', permission: 'Farm.Profile' },
            { label: 'Farm standards', route: '/app/farm/standards', permission: 'Farm.Profile' },
            { label: 'Users', route: '/app/farm/users', permission: 'Farm.Users' },
            { label: 'Permits and licenses', route: '/app/farm/documents', permission: 'Farm.Compliance' },
            { label: 'Activity log', route: '/app/farm/activity', permission: 'Farm.ActivityLog' },
        ],
    },
];

function startsWithPath(url: string, prefix: string): boolean {
    return url === prefix || url.startsWith(prefix + '/');
}

export function effectiveUrl(url: string): string {
    const [path, query = ''] = (url || '').split('?');
    if (/^\/app\/reports\/trace(\/|$)/.test(path.split('#')[0])) {
        return /(^|&)from=sales(&|#|$)/.test(query) ? '/app/sales' : '/app/production/batches';
    }
    return url;
}

export function pathOf(url: string): string {
    return (url || '').split(/[?#]/)[0];
}

export function sectionOfUrl(url: string): Section | undefined {
    const path = pathOf(url);
    return SECTIONS.find((s) => s.prefixes.some((p) => startsWithPath(path, p)));
}

export function activeTabRoute(section: Section, url: string): string | undefined {
    const path = pathOf(url);
    let best: SectionTab | undefined;
    for (const tab of section.tabs) {
        if (startsWithPath(path, tab.route) && (!best || tab.route.length > best.route.length)) {
            best = tab;
        }
    }
    return best?.route;
}
