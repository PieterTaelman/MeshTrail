// Production: the app is served from the same host as the API, so relative URLs are enough.
export const environment = {
  production: true,
  apiBaseUrl: '',
  // Map style URL (MapLibre style JSON). Empty = built-in OpenTopoMap raster style (online; offline tiles come later).
  mapStyleUrl: '',
};
