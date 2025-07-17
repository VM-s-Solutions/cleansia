import { Component } from '@angular/core';
import { RouterModule } from '@angular/router';
import { CleansiaButtonComponent } from '@cleansia/components';

@Component({
  imports: [RouterModule, CleansiaButtonComponent],
  selector: 'app-root',
  templateUrl: './app.html',
})
export class AppComponent {
  protected title = 'cleansia.app';
}
