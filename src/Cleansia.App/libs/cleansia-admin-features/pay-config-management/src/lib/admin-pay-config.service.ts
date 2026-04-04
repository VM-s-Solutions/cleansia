import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { ADMINAPIBASEURL } from '@cleansia/admin-services';
import { Observable } from 'rxjs';
import { PayConfigListItem } from './pay-config-management/pay-config-management.models';

export interface PagedPayConfigResponse {
  data: PayConfigListItem[];
  total: number;
}

export interface CreatePayConfigCommand {
  serviceId?: string;
  packageId?: string;
  basePay: number;
  extraPerRoom: number;
  extraPerBathroom: number;
  distanceRatePerKm: number;
  minimumPay: number;
  maximumPay: number;
  currencyId: string;
  description?: string;
}

export interface UpdatePayConfigCommand {
  payConfigId: string;
  basePay: number;
  extraPerRoom: number;
  extraPerBathroom: number;
  distanceRatePerKm: number;
  minimumPay: number;
  maximumPay: number;
  description?: string;
}

export interface PayConfigResponse {
  payConfigId: string;
}

@Injectable()
export class AdminPayConfigService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl =
    inject(ADMINAPIBASEURL, { optional: true }) ?? 'http://localhost:5001';

  getPaged(
    offset: number,
    limit: number,
    serviceId?: string,
    packageId?: string
  ): Observable<PagedPayConfigResponse> {
    let params = new HttpParams()
      .set('Offset', offset.toString())
      .set('Limit', limit.toString());

    if (serviceId) {
      params = params.set('Filter.ServiceId', serviceId);
    }
    if (packageId) {
      params = params.set('Filter.PackageId', packageId);
    }

    return this.http.get<PagedPayConfigResponse>(
      `${this.baseUrl}/api/AdminPayConfig/get-paged`,
      { params }
    );
  }

  getById(payConfigId: string): Observable<PayConfigListItem> {
    return this.http.get<PayConfigListItem>(
      `${this.baseUrl}/api/AdminPayConfig/details/${payConfigId}`
    );
  }

  create(command: CreatePayConfigCommand): Observable<PayConfigResponse> {
    return this.http.post<PayConfigResponse>(
      `${this.baseUrl}/api/AdminPayConfig/create`,
      command
    );
  }

  update(
    payConfigId: string,
    command: UpdatePayConfigCommand
  ): Observable<PayConfigResponse> {
    return this.http.put<PayConfigResponse>(
      `${this.baseUrl}/api/AdminPayConfig/update/${payConfigId}`,
      command
    );
  }

  delete(payConfigId: string): Observable<PayConfigResponse> {
    return this.http.delete<PayConfigResponse>(
      `${this.baseUrl}/api/AdminPayConfig/delete/${payConfigId}`
    );
  }
}
