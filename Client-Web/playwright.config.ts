import { defineConfig, devices } from '@playwright/test';

// End-to-end tests run against the full stack started by the Aspire AppHost (http://localhost:3000).
// Start it first: dotnet run --project ../Code/Server/Meshtrail.AppHost
export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: !!process.env['CI'],
  retries: process.env['CI'] ? 2 : 0,
  reporter: 'html',
  use: {
    baseURL: process.env['E2E_BASE_URL'] ?? 'http://localhost:3000',
    trace: 'on-first-retry',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
});
