import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit } from '@angular/core';
import { MenuItem } from 'primeng/api';
import { MenubarModule } from 'primeng/menubar';
import { CleansiaButtonComponent } from '../cleansia-button';

@Component({
  selector: 'cleansia-cleansia-menu',
  imports: [CommonModule, MenubarModule, CleansiaButtonComponent],
  templateUrl: './cleansia-menu.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaMenuComponent implements OnInit {
  items!: MenuItem[];

  ngOnInit(): void {
    this.items = [
      {
        label: 'Úklid',
        icon: 'pi-heart',
        items: [
          {
            label: 'Tvoje mama',
          },
        ],
      },
    ];
  }
}
