export class MenuItem {
    id: number;
    parentId: number;
    label: string;
    route: string;
    icon: string;
    permissionName: string;
    isActive?: boolean;
    isCollapsed?: boolean;
    section?: string;
    heading?: string;
    matchPrefixes?: string[];
    anyPermissions?: string[];
    children: MenuItem[];

    constructor(
        label: string,
        route: string,
        icon: string,
        permissionName: string = null,
        children: MenuItem[] = null
    ) {
        this.label = label;
        this.route = route;
        this.icon = icon;
        this.permissionName = permissionName;
        this.children = children;
    }
}
