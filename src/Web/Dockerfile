FROM node:24-alpine AS build
RUN corepack enable && corepack prepare pnpm@9 --activate
WORKDIR /app

# Install dependencies (layer cached when lockfile unchanged)
COPY pnpm-workspace.yaml package.json pnpm-lock.yaml ./
COPY packages/app/package.json packages/app/
COPY packages/bridge/package.json packages/bridge/
COPY packages/bot/package.json packages/bot/
COPY packages/portal/package.json packages/portal/
RUN pnpm install --frozen-lockfile

# Copy source — then reinstall to fix any symlinks overwritten by COPY
COPY . .
RUN pnpm install --frozen-lockfile

RUN pnpm --filter @nocturne/bridge build 2>/dev/null || true
RUN pnpm --filter @nocturne/app run build:sveltekit

# -- Production --
FROM node:24-alpine AS runtime
RUN corepack enable && corepack prepare pnpm@9 --activate
WORKDIR /app
ENV NODE_ENV=production

# Copy workspace structure — server.js must stay in packages/app for pnpm resolution
COPY --from=build /app/pnpm-workspace.yaml ./
COPY --from=build /app/package.json ./
COPY --from=build /app/pnpm-lock.yaml ./
COPY --from=build /app/packages/app/build packages/app/build
COPY --from=build /app/packages/app/server.js packages/app/
COPY --from=build /app/packages/app/package.json packages/app/
COPY --from=build /app/packages/bridge packages/bridge
COPY --from=build /app/packages/bot/package.json packages/bot/
COPY --from=build /app/packages/portal/package.json packages/portal/
RUN pnpm install --frozen-lockfile --prod

EXPOSE 3000
ENV PORT=3000
CMD ["node", "packages/app/server.js"]
