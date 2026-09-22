import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { CleansiaButtonComponent } from '@cleansia/components';
import { CleansiaCustomerRoute } from '@cleansia/services';
import { FoamEdgeComponent } from '@cleansia-customer/home';
import { TranslatePipe } from '@ngx-translate/core';
import { Skeleton } from 'primeng/skeleton';
import { WorkContractPageFacade } from './work-contract-page.facade';

@Component({
  selector: 'cleansia-customer-work-contract-page',
  standalone: true,
  imports: [DatePipe, RouterLink, TranslatePipe, FoamEdgeComponent, CleansiaButtonComponent, Skeleton],
  templateUrl: './work-contract-page.component.html',
  providers: [WorkContractPageFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WorkContractPageComponent implements OnInit {
  protected readonly facade = inject(WorkContractPageFacade);
  private readonly route = inject(ActivatedRoute);

  protected readonly orderLink = [
    '/',
    CleansiaCustomerRoute.ORDERS,
    this.route.snapshot.paramMap.get('orderId') ?? '',
  ];

  ngOnInit(): void {
    this.facade.load(this.route.snapshot.paramMap.get('acceptanceId') ?? '');
  }
}
