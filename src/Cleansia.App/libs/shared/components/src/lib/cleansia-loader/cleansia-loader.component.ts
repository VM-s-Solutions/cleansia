import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'cleansia-loader',
  templateUrl: './cleansia-loader.component.html',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaLoaderComponent {}
