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
      const y = 336 - p * 68;
      d += `${i ? 'L' : 'M'}${x.toFixed(1)} ${y.toFixed(1)}`;
    }
    return d;
  }

  /** The dot sits on the curve at x 268. */
  dotY(): number {
    const t = (268 - 56) / 368;
    return 336 - (1 / (1 + Math.exp(-(t - 0.5) * 7))) * 68;
  }
}
