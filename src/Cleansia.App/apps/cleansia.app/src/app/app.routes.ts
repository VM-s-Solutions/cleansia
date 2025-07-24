import { Route } from '@angular/router';

export const appRoutes: Route[] = [
    {
        path: '',
        loadComponent: () => import('@cleansia/cleansia')
            .then(m => m.CleansiaComponent)
    },
];
