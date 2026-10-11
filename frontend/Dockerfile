# Frontend Next.js (§12). Multi-stage con salida "standalone": la imagen final lleva solo el servidor
# mínimo (server.js) y los archivos que usa, sin el node_modules completo ni el código fuente.

# ---- Dependencias ----
FROM node:24-alpine AS deps
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci

# ---- Build ----
FROM node:24-alpine AS build
WORKDIR /app
ENV NEXT_TELEMETRY_DISABLED=1
COPY --from=deps /app/node_modules ./node_modules
COPY . .
RUN npm run build

# ---- Runtime ----
FROM node:24-alpine AS runner
WORKDIR /app
ENV NODE_ENV=production \
    NEXT_TELEMETRY_DISABLED=1 \
    PORT=3000 \
    HOSTNAME=0.0.0.0

# server.js no sirve .next/static por su cuenta: se copia junto a él.
COPY --from=build --chown=node:node /app/.next/standalone ./
COPY --from=build --chown=node:node /app/.next/static ./.next/static

# Usuario sin privilegios que ya trae la imagen de Node.
USER node
EXPOSE 3000

# API_URL se lee en el servidor al atender cada request: se define en el docker-compose (http://api:8080).
CMD ["node", "server.js"]
