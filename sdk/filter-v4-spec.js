#!/usr/bin/env node
/**
 * Filters the full Nocturne OpenAPI spec down to V4 endpoints only.
 * Usage: node filter-v4-spec.js <input-openapi.json> <output-openapi-v4.json>
 */
import { readFileSync, writeFileSync } from 'fs';

const [,, inputPath, outputPath] = process.argv;
if (!inputPath || !outputPath) {
  console.error('Usage: node filter-v4-spec.js <input> <output>');
  process.exit(1);
}

const spec = JSON.parse(readFileSync(inputPath, 'utf8'));

// Filter paths to only /api/v4/**
const filteredPaths = {};
for (const [path, operations] of Object.entries(spec.paths)) {
  if (path.startsWith('/api/v4/')) {
    filteredPaths[path] = operations;
  }
}

// Collect all $ref references used by V4 paths
function collectRefs(obj, refs = new Set()) {
  if (obj === null || typeof obj !== 'object') return refs;
  if (obj['$ref'] && typeof obj['$ref'] === 'string') {
    const schemaName = obj['$ref'].replace('#/components/schemas/', '');
    if (obj['$ref'].startsWith('#/components/schemas/')) {
      refs.add(schemaName);
    }
  }
  for (const value of Object.values(obj)) {
    collectRefs(value, refs);
  }
  return refs;
}

// Iteratively resolve transitive schema references
const allSchemas = spec.components?.schemas || {};
const usedSchemaNames = collectRefs(filteredPaths);
let prevSize = 0;
while (usedSchemaNames.size > prevSize) {
  prevSize = usedSchemaNames.size;
  for (const name of [...usedSchemaNames]) {
    if (allSchemas[name]) {
      collectRefs(allSchemas[name], usedSchemaNames);
    }
  }
}

// Build filtered schemas
const filteredSchemas = {};
for (const name of usedSchemaNames) {
  if (allSchemas[name]) {
    filteredSchemas[name] = allSchemas[name];
  }
}

const output = {
  openapi: spec.openapi,
  info: {
    ...spec.info,
    title: 'Nocturne SDK',
    description: 'Nocturne V4 API SDK',
  },
  paths: filteredPaths,
  components: {
    ...spec.components,
    schemas: filteredSchemas,
  },
};

// Remove security schemes not relevant to SDK consumers (keep Bearer)
if (output.components.securitySchemes) {
  const kept = {};
  for (const [name, scheme] of Object.entries(output.components.securitySchemes)) {
    if (scheme.type === 'http' && scheme.scheme === 'bearer') {
      kept[name] = scheme;
    }
  }
  output.components.securitySchemes = kept;
}

writeFileSync(outputPath, JSON.stringify(output, null, 2));
console.log(`Filtered spec: ${Object.keys(filteredPaths).length} paths, ${Object.keys(filteredSchemas).length} schemas`);
