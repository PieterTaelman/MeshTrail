// @ts-check
const eslint = require('@eslint/js');
const { defineConfig } = require('eslint/config');
const tseslint = require('typescript-eslint');
const angular = require('angular-eslint');

module.exports = defineConfig([
  {
    ignores: ['dist/**', '.angular/**', 'coverage/**', 'playwright-report/**', 'test-results/**'],
  },
  {
    files: ['**/*.ts'],
    extends: [
      eslint.configs.recommended,
      tseslint.configs.recommended,
      tseslint.configs.stylistic,
      angular.configs.tsRecommended,
    ],
    processor: angular.processInlineTemplates,
    rules: {
      '@angular-eslint/directive-selector': [
        'error',
        { type: 'attribute', prefix: 'app', style: 'camelCase' },
      ],
      '@angular-eslint/component-selector': [
        'error',
        { type: 'element', prefix: 'app', style: 'kebab-case' },
      ],
      // Team standards (see AGENTS.md): OnPush, standalone, inject(), no @HostBinding/@HostListener, signal inputs/outputs.
      '@angular-eslint/prefer-on-push-component-change-detection': 'error',
      '@angular-eslint/prefer-standalone': 'error',
      '@angular-eslint/prefer-inject': 'error',
      '@angular-eslint/no-host-metadata-property': 'off',
      '@angular-eslint/prefer-signals': 'error',
      '@angular-eslint/prefer-output-emitter-ref': 'error',
      'no-restricted-syntax': [
        'error',
        {
          selector: 'Decorator[expression.callee.name="HostBinding"]',
          message: 'Use the host property of @Component instead of @HostBinding.',
        },
        {
          selector: 'Decorator[expression.callee.name="HostListener"]',
          message: 'Use the host property of @Component instead of @HostListener.',
        },
        {
          selector: 'Decorator[expression.callee.name="NgModule"]',
          message: 'NgModules are not used: write standalone components.',
        },
        {
          selector: 'CallExpression[callee.property.name="mutate"]',
          message: 'signal.mutate does not exist anymore: use set() or update().',
        },
      ],
      'no-restricted-imports': [
        'error',
        {
          paths: [
            {
              name: '@angular/common',
              importNames: ['NgClass', 'NgStyle', 'NgIf', 'NgFor', 'NgSwitch'],
              message: 'Use [class]/[style] bindings and @if/@for/@switch.',
            },
          ],
          patterns: [
            {
              group: ['primeng', 'primeng/*', '@primeuix/*'],
              message: 'Use @openng/optimus-ui (the only UI library).',
            },
            { group: ['@ambe-web-framework/*'], message: 'AWF is not used in Meshtrail.' },
          ],
        },
      ],
    },
  },
  {
    files: ['**/*.html'],
    extends: [angular.configs.templateRecommended, angular.configs.templateAccessibility],
    rules: {
      '@angular-eslint/template/prefer-control-flow': 'error',
      '@angular-eslint/template/prefer-self-closing-tags': 'error',
    },
  },
]);
