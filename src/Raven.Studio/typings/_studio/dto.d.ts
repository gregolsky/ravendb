
interface queryResultDto {
    Results: any[];
    Includes: any[];
}

interface canActivateResultDto {
    redirect?: string;
    can?: boolean;   
}