import { Component } from '@angular/core';
import { RevealDirective } from './motion';

@Component({
  selector: 'app-flow',
  imports: [RevealDirective],
  templateUrl: './flow-diagram.html',
})
export class FlowDiagram {
  curve(): string {
    let d = '';
    for (let i = 0; i <= 42; i++) {
      const t = i / 42;
      const x = 56 + t * 368;
      const p = 1 / (1 + Math.exp(-(t - 0.5) * 7));
      const y = 338 - p * 78;
      d += `${i ? 'L' : 'M'}${x.toFixed(1)} ${y.toFixed(1)}`;
    }
    return d;
  }
}
