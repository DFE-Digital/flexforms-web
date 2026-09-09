import './support/load-env';
import { defineConfig, devices } from '@playwright/test';
import { getServiceConfigFromEnv } from './support/test-config';

const serviceConfig = getServiceConfigFromEnv();
const zapProxy = process.env.ZAP_PROXY;

export default defineConfig({
  testDir: './tests',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: process.env.CI
    ? [['list'], ['html', { open: 'never' }], ['json', { outputFile: 'reports/report.json' }]]
    : [['html', { open: 'never' }], ['list']],
  use: {
    trace: 'on-first-retry',
    ignoreHTTPSErrors: true,
    ...(zapProxy
      ? {
          proxy: { server: zapProxy },
        }
      : {}),
  },
  projects: [
    {
      name: serviceConfig.name,
      testMatch: new RegExp(`${serviceConfig.name}/.*\\.spec\\.ts`),
      use: {
        ...devices['Desktop Chrome'],
        baseURL: serviceConfig.url,
      },
    },
  ],
});
