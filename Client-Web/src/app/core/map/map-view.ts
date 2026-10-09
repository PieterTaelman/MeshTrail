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
  type MapGeoJSONFeature,
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

/** The visible area: [west, south, east, north] in degrees. */
export type MapBounds = [number, number, number, number];

const POINTS_SOURCE = 'features';
const MARKERS_SOURCE = 'markers';
const CLUSTERS = 'features-clusters';
const CLUSTER_COUNT = 'features-cluster-count';
const POINTS = 'features-points';
const MARKERS = 'markers-rings';
const SELECTED = 'features-selected';
const EMPTY: MapFeatures = { type: 'FeatureCollection', features: [] };

/** Belgium, where the first gateways are. */
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
 * Draws two kinds of points: `features` (many, grouped into numbered clusters when zoomed out) and `markers`
 * (few, e.g. gateways: a coloured ring, never clustered). Reports clicks and the visible area.
 */
@Component({
  selector: 'app-map-view',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div #container class="size-full" role="region" aria-label="Map"></div>`,
  host: { class: 'block size-full' },
})
export class MapView {
  readonly features = input<MapFeatures>(EMPTY);
  readonly markers = input<MapFeatures>(EMPTY);
  /** Id of the feature to highlight, or null. */
  readonly selectedId = input<string | null>(null);
  /** Emits the id of the clicked feature or marker. */
  readonly featureClick = output<string>();
  /** Emits the visible area after every move (and once when the map is ready), to load data per view. */
  readonly boundsChange = output<MapBounds>();

  private readonly container = viewChild.required<ElementRef<HTMLDivElement>>('container');
  private readonly style = inject(MAP_STYLE);
  private map?: MapLibreMap;
  private loaded = false;

  constructor() {
    afterNextRender(() => this.createMap());
    inject(DestroyRef).onDestroy(() => this.map?.remove());

    effect(() => {
      const features = this.features();
      if (this.loaded) {
        this.setData(POINTS_SOURCE, features);
      }
    });

    effect(() => {
      const markers = this.markers();
      if (this.loaded) {
        this.setData(MARKERS_SOURCE, markers);
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
      this.addLayers(map);
      this.loaded = true;
      this.setData(POINTS_SOURCE, this.features());
      this.setData(MARKERS_SOURCE, this.markers());
      map.setFilter(SELECTED, ['==', ['id'], this.selectedId() ?? '']);
      this.emitBounds(map);
    });
    map.on('moveend', () => this.emitBounds(map));

    this.map = map;
  }

  private addLayers(map: MapLibreMap): void {
    // Zoomed out, nearby nodes merge into one numbered circle; zooming in splits them again.
    map.addSource(POINTS_SOURCE, {
      type: 'geojson',
      data: EMPTY,
      promoteId: 'featureId',
      cluster: true,
      clusterRadius: 40,
      clusterMaxZoom: 12,
    });
    map.addSource(MARKERS_SOURCE, { type: 'geojson', data: EMPTY, promoteId: 'featureId' });

    map.addLayer({
      id: CLUSTERS,
      type: 'circle',
      source: POINTS_SOURCE,
      filter: ['has', 'point_count'],
      paint: {
        'circle-color': 'rgba(34,197,94,0.75)',
        'circle-radius': ['step', ['get', 'point_count'], 14, 10, 18, 100, 24, 1000, 30],
        'circle-stroke-color': '#0f172a',
        'circle-stroke-width': 1.5,
      },
    });
    // Numbers need a font; styles without one (glyphs) simply show the circles.
    if (map.getStyle().glyphs) {
      map.addLayer({
        id: CLUSTER_COUNT,
        type: 'symbol',
        source: POINTS_SOURCE,
        filter: ['has', 'point_count'],
        layout: {
          'text-field': ['get', 'point_count_abbreviated'],
          'text-font': ['Open Sans Semibold'],
          'text-size': 12,
        },
        paint: { 'text-color': '#0f172a' },
      });
    }
    map.addLayer({
      id: SELECTED,
      type: 'circle',
      source: POINTS_SOURCE,
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
      source: POINTS_SOURCE,
      filter: ['!', ['has', 'point_count']],
      paint: {
        'circle-radius': ['coalesce', ['get', 'radius'], 7],
        'circle-color': ['coalesce', ['get', 'color'], '#64748b'],
        'circle-stroke-color': '#0f172a',
        'circle-stroke-width': 1.5,
      },
    });
    map.addLayer({
      id: MARKERS,
      type: 'circle',
      source: MARKERS_SOURCE,
      paint: {
        'circle-radius': ['coalesce', ['get', 'radius'], 11],
        'circle-color': 'rgba(15,23,42,0.35)',
        'circle-stroke-color': ['coalesce', ['get', 'color'], '#64748b'],
        'circle-stroke-width': 3,
      },
    });

    for (const layer of [POINTS, MARKERS]) {
      map.on('click', layer, (event: MapLayerMouseEvent) => {
        const id = event.features?.[0]?.properties?.['featureId'];
        if (typeof id === 'string') {
          this.featureClick.emit(id);
        }
      });
    }
    map.on(
      'click',
      CLUSTERS,
      (event: MapLayerMouseEvent) => void this.zoomIntoCluster(map, event.features?.[0]),
    );
    for (const layer of [POINTS, MARKERS, CLUSTERS]) {
      map.on('mouseenter', layer, () => (map.getCanvas().style.cursor = 'pointer'));
      map.on('mouseleave', layer, () => (map.getCanvas().style.cursor = ''));
    }
  }

  private async zoomIntoCluster(
    map: MapLibreMap,
    cluster: MapGeoJSONFeature | undefined,
  ): Promise<void> {
    const clusterId = cluster?.properties?.['cluster_id'];
    if (typeof clusterId !== 'number' || cluster?.geometry.type !== 'Point') {
      return;
    }
    const source = map.getSource(POINTS_SOURCE) as GeoJSONSource | undefined;
    const zoom = await source?.getClusterExpansionZoom(clusterId);
    if (zoom !== undefined) {
      map.easeTo({ center: cluster.geometry.coordinates as [number, number], zoom });
    }
  }

  private setData(sourceId: string, features: MapFeatures): void {
    // MapLibre needs the id inside the properties to report it on click (promoteId).
    const data: MapFeatures = {
      type: 'FeatureCollection',
      features: features.features.map((feature) => ({
        ...feature,
        properties: { ...feature.properties, featureId: String(feature.id) },
      })),
    };
    void (this.map?.getSource(sourceId) as GeoJSONSource | undefined)?.setData(data);
  }

  private emitBounds(map: MapLibreMap): void {
    const bounds = map.getBounds();

    // Panning far east or west gives longitudes beyond ±180; wrap them back.
    const west =
      bounds.getWest() < -180 || bounds.getEast() - bounds.getWest() >= 360
        ? -180
        : bounds.getWest();
    const east =
      bounds.getEast() > 180 || bounds.getEast() - bounds.getWest() >= 360 ? 180 : bounds.getEast();
    this.boundsChange.emit([
      west,
      Math.max(bounds.getSouth(), -90),
      east,
      Math.min(bounds.getNorth(), 90),
    ]);
  }
}
