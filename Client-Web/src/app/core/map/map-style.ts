import { InjectionToken } from '@angular/core';
import type { StyleSpecification } from 'maplibre-gl';
import { environment } from '../../../environments/environment';

/**
 * Default topo style: OpenTopoMap raster tiles (contours, hill shading, trails). Needs internet;
 * offline vector tiles (PMTiles) replace this later by setting environment.mapStyleUrl.
 */
export const OPEN_TOPO_MAP_STYLE: StyleSpecification = {
  version: 8,
  sources: {
    topo: {
      type: 'raster',
      tiles: [
        'https://a.tile.opentopomap.org/{z}/{x}/{y}.png',
        'https://b.tile.opentopomap.org/{z}/{x}/{y}.png',
        'https://c.tile.opentopomap.org/{z}/{x}/{y}.png',
      ],
      tileSize: 256,
      maxzoom: 17,
      attribution:
        'Map data © <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors, SRTM | ' +
        'Style © <a href="https://opentopomap.org">OpenTopoMap</a> (CC-BY-SA)',
    },
  },
  layers: [{ id: 'topo', type: 'raster', source: 'topo' }],
};

/** The style the map uses: a style URL from configuration, or the built-in topo style. A token so tests can swap it. */
export const MAP_STYLE = new InjectionToken<string | StyleSpecification>('MAP_STYLE', {
  providedIn: 'root',
  factory: () => environment.mapStyleUrl || OPEN_TOPO_MAP_STYLE,
});
