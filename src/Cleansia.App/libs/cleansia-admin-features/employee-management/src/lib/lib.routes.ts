import { Route } from '@angular/router';
import { EmployeeManagementComponent } from './employee-management/employee-management.component';
import { EmployeeDetailComponent } from './employee-detail/employee-detail.component';

export const employeeManagementRoutes: Route[] = [
  { path: '', component: EmployeeManagementComponent },
  { path: ':employeeId', component: EmployeeDetailComponent },
];
