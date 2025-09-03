export interface AdminUser{
    id: number;
    email: string;
    lastLogin: Date;
    routeCount: number;
    isAdmin: boolean;
    
    // Route instance statistics
    routeInstancesCount: number;
    routeInstancesWithTimeCount: number;
    routeInstancesWithTrawellingIdCount: number;
    lastRouteInstanceDate?: Date;
}
