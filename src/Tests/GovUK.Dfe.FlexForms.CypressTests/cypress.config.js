const { defineConfig } = require("cypress");

module.exports = defineConfig({
  e2e: {
    allowCypressEnv: false,
    experimentalRunAllSpecs: true,
    reporter: 'cypress-multi-reporters',
    reporterOptions: {
      reporterEnabled: 'mochawesome',
      mochawesomeReporterOptions: {
        reportDir: 'cypress/reports/mocha',
        quite: true,
        overwrite: false,
        html: false,
        json: true,
      }
    },

    setupNodeEvents(on, config) {
      // Accept self-signed certificates
      on('before:browser:launch', (browser, launchOptions) => {
        if (browser.name === 'chrome') {
          launchOptions.args.push('--ignore-certificate-errors')
          launchOptions.args.push('--allow-insecure-localhost')
        }
        return launchOptions
      })
      config.baseUrl = config.env.url;
      config.expose = {
        ...(config.expose || {}),
        url: config.env.url,
        username: config.env.username,
      }

      return config
    }
  },
});
