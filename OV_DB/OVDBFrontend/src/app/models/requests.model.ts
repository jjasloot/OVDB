export interface RequestForUser {
    id: number;
    message: string;
    created: Date | string;
    response: string | null;
    responded: Date | string | null;
}

export interface RequestForAdmin extends RequestForUser {
    userId: number;
    userEmail: string;
}

export interface CreateRequest {
    message: string;
}
