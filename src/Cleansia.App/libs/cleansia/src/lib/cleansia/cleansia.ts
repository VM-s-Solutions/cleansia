import { Component, OnInit } from '@angular/core';
import { MenuItem, MessageService } from 'primeng/api';

@Component({
  selector: 'cleansia-home',
  templateUrl: './cleansia.component.html',
  styleUrls: ['./cleansia.component.scss'],
  providers: [MessageService]
})
export class CleansiaComponent
}