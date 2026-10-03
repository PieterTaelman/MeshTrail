import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  afterNextRender,
  effect,
  inject,
  input,
  output,
  viewChild,
} from '@angular/core';
import type { FeatureCollection, Point } from 'geojson';
import {
  type GeoJSONSource,
  Map as MapLibreMap,
  type MapLayerMouseEvent,
  NavigationControl,
  ScaleControl,
  setWorkerUrl,
} from 'maplibre-gl';
import { MAP_STYLE } from './map-style';

/**
 * Features the map can draw. Each feature needs a string `id` and may set these properties:
 * `color` (CSS colour), `radius` (px) and `label` (tooltip text).
 */
export type MapFeatures = FeatureCollection<Point>;

const SOURCE = 'features';
const POINTS = 'features-points';
const SELECTED = 'features-selected';
const EMPTY: MapFeatures = { type: 'FeatureCollection', features: [] };

/** Belgium, until we have data to zoom to. */
const START_CENTER: [number, number] = [4.47, 50.5];

// The MapLibre worker is copied to /maplibre by angular.json (bundlers cannot find it on their own).
let workerConfigured = false;
function configureWorker(): void {
  if (!workerConfigured) {
    setWorkerUrl(new URL('maplibre/maplibre-gl-worker.mjs', document.baseURI).href);
    workerConfigured = true;
  }
}

/**
 * The ONLY component that talks to MapLibre, so the map library can be swapped in one place.
 * Draws point features coloured by their `color` property and reports clicks.
 */
@Component({
  selector: 'app-map-view',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div #container class="size-full" role="region" aria-label="Map"></div>`,
  host: { class: 'block size-full' },
})
export class MapView {
  readonly features = input<MapFeatures>(EMPTY);
  /** Id of the feature to highlight, or null. */
  readonly selectedId = input<string | null>(null);
  /** Emits the id of the clicked feature. */
  readonly featureClick = output<string>();

  private readonly container = viewChild.required<ElementRef<HTMLDivElement>>('container');
  private readonly style = inject(MAP_STYLE);
  private map?: MapLibreMap;
  private loaded = false;
  private fittedOnce = false;

  constructor() {
    afterNextRender(() => this.createMap());
    inject(DestroyRef).onDestroy(() => this.map?.remove());

    effect(() => {
      const features = this.features();
      if (this.loaded) {
        this.showFeatures(features);
      }
    });

    effect(() => {
      const selected = this.selectedId();
      if (this.loaded) {
        this.map?.setFilter(SELECTED, ['==', ['id'], selected ?? '']);
      }
    });
  }

  /** Moves the camera to a point (e.g. the selected node or a new SOS). */
  flyTo(longitude: number, latitude: number, zoom = 13): void {
    this.map?.flyTo({ center: [longitude, latitude], zoom: Math.max(zoom, this.map.getZoom()) });
  }

  private createMap(): void {
    configureWorker();
    const map = new MapLibreMap({
      container: this.container().nativeElement,
      style: this.style,
      center: START_CENTER,
      zoom: 7,
      attributionControl: { compact: true },
    });
    map.addControl(new NavigationControl({ visualizePitch: false }), 'top-right');
    map.addControl(new ScaleControl({ unit: 'metric' }), 'bottom-left');

    map.on('load', () => {
      map.addSource(SOURCE, { type: 'geojson', data: EMPTY, promoteId: 'featureId' });
      map.addLayer({
        id: SELECTED,
        type: 'circle',
        source: SOURCE,
        filter: ['==', ['id'], ''],
        paint: {
          'circle-radius': ['+', ['coalesce', ['get', 'radius'], 7], 6],
          'circle-color': 'rgba(255,255,255,0.25)',
          'circle-stroke-color': '#ffffff',
          'circle-stroke-width': 2,
        },
      });
      map.addLayer({
        id: POINTS,
        type: 'circle',
        source: SOURCE,
        paint: {
          'circle-radius': ['coalesce', ['get', 'radius'], 7],
          'circle-color': ['coalesce', ['get', 'color'], '#64748b'],
          'circle-stroke-color': '#0f172a',
          'circle-stroke-width': 1.5,
        },
      });

      map.on('click', POINTS, (event: MapLayerMouseEvent) => {
        const id = event.features?.[0]?.properties?.['featureId'];
        if (typeof id === 'string') {
          this.featureClick.emit(id);
        }
      });
      map.on('mouseenter', POINTS, () => (map.getCanvas().style.cursor = 'pointer'));
      map.on('mouseleave', POINTS, () => (map.getCanvas().style.cursor = ''));

      this.loaded = true;
      this.showFeatures(this.features());
      map.setFilter(SELECTED, ['==', ['id'], this.selectedId() ?? '']);
    });

    this.map = map;
  }

  private showFeatures(features: MapFeatures): void {
    const map = this.map;
    if (!map) {
      return;
    }

    // MapLibre needs the id inside the properties to report it on click (promoteId).
    const data: MapFeatures = {
      type: 'FeatureCollection',
      features: features.features.map((feature) => ({
        ...feature,
        properties: { ...feature.properties, featureId: String(feature.id) },
      })),
    };
    void (map.getSource(SOURCE) as GeoJSONSource | undefined)?.setData(data);

    // Zoom to the data the first time there is any; after that the user controls the camera.
    if (!this.fittedOnce && data.features.length > 0) {
      this.fittedOnce = true;
      const [first, ...rest] = data.features.map((feature) => feature.geometry.coordinates);
      const bounds = rest.reduce(
        (box, [lng, lat]) => [
          Math.min(box[0], lng),
          Math.min(box[1], lat),
          Math.max(box[2], lng),
          Math.max(box[3], lat),
        ],
        [first[0], first[1], first[0], first[1]],
      );
      map.fitBounds(
        [
          [bounds[0], bounds[1]],
          [bounds[2], bounds[3]],
        ],
        { padding: 60, maxZoom: 13, duration: 0 },
      );
    }
  }
}
