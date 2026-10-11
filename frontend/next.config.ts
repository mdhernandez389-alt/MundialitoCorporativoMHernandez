import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // Servidor mínimo (server.js) con solo los archivos necesarios: la imagen de Docker queda chica.
  output: "standalone",
  cacheComponents: true,
  partialPrefetching: true,
  turbopack: {
    rules: {
      "*.css": {
        loaders: ["@tailwindcss/turbopack"],
        as: "*.css",
      },
    },
  },
};

export default nextConfig;
