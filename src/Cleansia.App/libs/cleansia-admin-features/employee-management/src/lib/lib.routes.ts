import { Route } from '@angular/router';
import { EmployeeManagementComponent } from './employee-management/employee-management.component';
import { EmployeeDetailComponent } from './employee-detail/employee-detail.component';

export const employeeManagementRoutes: Route[] = [
  {
    path: '',
    component: EmployeeManagementComponent,
    data: { title: 'page_titles.admin.employees' },
  },
  {
    path: ':employeeId',
    component: EmployeeDetailComponent,
    data: { title: 'page_titles.admin.employee_details' },
  },
];
