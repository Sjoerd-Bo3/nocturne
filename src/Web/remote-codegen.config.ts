export default {
  openApiPath: './packages/app/src/lib/api/generated/openapi.json',
  outputDir: './packages/app/src/lib',
  remoteFunctionsOutput: 'api/generated',
  apiClientOutput: 'api/api-client.generated.ts',
  imports: {
    schemas: '$lib/api/generated/schemas',
    apiTypes: '$api',
  },
  nswagClientPath: './generated/nocturne-api-client',
};
