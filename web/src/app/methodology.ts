import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FlowDiagram } from './flow-diagram';
import { RevealDirective } from './motion';
import { SurfaceView } from './surface';

@Component({
  selector: 'app-methodology',
  imports: [RouterLink, RevealDirective, FlowDiagram, SurfaceView],
  templateUrl: './methodology.html',
})
export class MethodologyPage {}
