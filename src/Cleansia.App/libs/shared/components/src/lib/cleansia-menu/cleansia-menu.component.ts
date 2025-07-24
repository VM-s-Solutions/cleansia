import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { MenubarModule } from 'primeng/menubar';
import { MenuItem } from 'primeng/api';
import { CleansiaButtonComponent } from "../cleansia-button";

@Component({
  selector: 'cleansia-cleansia-menu',
  imports: [CommonModule, MenubarModule, CleansiaButtonComponent],
  templateUrl: './cleansia-menu.component.html',
  styleUrl: './cleansia-menu.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaMenuComponent implements OnInit {
  items!: MenuItem[];

  ngOnInit(): void {
    this.items = [
      {
        label: "Úklid",
        icon: 'pi-heart',
        items:[ {
          label: 'Tvoje mama'
        }]
      }
    ];
  }
}
