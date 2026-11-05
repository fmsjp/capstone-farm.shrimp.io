import tsPlugin from '@typescript-eslint/eslint-plugin';
import tsParser from '@typescript-eslint/parser';
import angularPlugin from '@angular-eslint/eslint-plugin';
import importPlugin from 'eslint-plugin-import';
import prettierPlugin from 'eslint-plugin-prettier';
import globals from 'globals';
import js from '@eslint/js';

import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { includeIgnoreFile } from '@eslint/compat';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const gitignorePath = path.resolve(__dirname, '.gitignore');

export default [
    js.configs.recommended,
    {
        files: ['src/**/*.ts'],

        rules: {
            'no-multi-assign': 'off',
            'no-undef': 'off',
            'prettier/prettier': 'error',
            'no-unused-vars': 'off',

            '@angular-eslint/component-class-suffix': 'off',
            '@angular-eslint/component-selector': [
                'off',
                {
                    type: 'element',
                    prefix: 'app',
                    style: 'kebab-case',
                },
            ],
            '@angular-eslint/directive-class-suffix': 'error',
            '@angular-eslint/directive-selector': [
                'off',
                {
                    type: 'attribute',
                    prefix: 'app',
                    style: 'camelCase',
                },
            ],
            '@angular-eslint/no-input-rename': 'off',
            '@angular-eslint/no-inputs-metadata-property': 'error',
            '@angular-eslint/no-output-rename': 'error',
            '@angular-eslint/no-outputs-metadata-property': 'error',
            '@angular-eslint/use-lifecycle-interface': 'error',
            '@angular-eslint/use-pipe-transform-interface': 'error',

            '@typescript-eslint/consistent-type-definitions': 'error',
            '@typescript-eslint/dot-notation': 'off',
            '@typescript-eslint/explicit-member-accessibility': [
                'off',
                {
                    accessibility: 'explicit',
                },
            ],
            '@typescript-eslint/indent': 'off',
            '@typescript-eslint/member-ordering': 'error',
            '@typescript-eslint/naming-convention': [
                'error',
                {
                    selector: ['variable'],
                    modifiers: ['static', 'readonly'],
                    format: ['UPPER_CASE'],
                },
            ],
            '@typescript-eslint/no-empty-function': 'off',
            '@typescript-eslint/no-empty-interface': 'error',
            '@typescript-eslint/no-inferrable-types': [
                'error',
                {
                    ignoreParameters: true,
                },
            ],
            '@typescript-eslint/no-misused-new': 'error',
            '@typescript-eslint/no-non-null-assertion': 'error',
            '@typescript-eslint/no-shadow': 'off',
            '@typescript-eslint/no-unused-expressions': 'error',
            '@typescript-eslint/prefer-function-type': 'error',
            '@typescript-eslint/semi': 'off',
            '@typescript-eslint/unified-signatures': 'error',

            'arrow-body-style': 'off',
            'brace-style': ['error', '1tbs'],
            'constructor-super': 'error',
            curly: 'error',
            'dot-notation': 'off',
            'eol-last': 'error',
            eqeqeq: ['error', 'smart'],
            'guard-for-in': 'error',
            'id-denylist': 'off',
            'id-match': 'off',
            'import/no-deprecated': 'warn',
            'max-len': 'off',
            'no-bitwise': 'error',
            'no-caller': 'error',
            'no-console': [
                'error',
                {
                    allow: [
                        'log',
                        'warn',
                        'dir',
                        'timeLog',
                        'assert',
                        'clear',
                        'count',
                        'countReset',
                        'group',
                        'groupEnd',
                        'table',
                        'dirxml',
                        'error',
                        'groupCollapsed',
                        'Console',
                        'profile',
                        'profileEnd',
                        'timeStamp',
                        'context',
                    ],
                },
            ],
            'no-debugger': 'error',
            'no-empty': 'off',
            'no-empty-function': 'off',
            'no-eval': 'off',
            'no-fallthrough': 'error',
            'no-new-wrappers': 'error',
            'no-restricted-imports': 'error',
            'no-shadow': 'off',
            'no-throw-literal': 'off',
            'no-trailing-spaces': 'error',
            'no-undef-init': 'error',
            'no-underscore-dangle': 'off',
            'no-unused-expressions': 'error',
            'no-unused-labels': 'error',
            'no-var': 'error',
            'prefer-const': 'off',
            radix: 'off',
            'valid-typeof': 'error',
        },

        plugins: {
            '@typescript-eslint': tsPlugin,
            '@angular-eslint': angularPlugin,
            import: importPlugin,
            prettier: prettierPlugin,
        },
    },
    {
        languageOptions: {
            ecmaVersion: 'latest',
            sourceType: 'module',
            parser: tsParser,
            globals: {
                console: 'readonly',
                ...globals.browser,
                ...globals.node,
                AudioWorkletGlobalScope: true,
            },
        },
    },
    includeIgnoreFile(gitignorePath),
    {
        ignores: ['src/**/*.d.ts'],
    },
];
