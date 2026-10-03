// Local development: the API runs on the fixed port from its launchSettings.json (started by the Aspire AppHost).
export const environment = {
  production: false,
  apiBaseUrl: 'https://localhost:7301',
  // Map style URL (MapLibre style JSON). Empty = built-in OpenTopoMap raster style (online; offline tiles come later).
  mapStyleUrl: '',
};
