# Frontend — Mundialito Corporativo

Next.js 16 (App Router, Cache Components). Decisiones de diseño en
[docs/DECISIONES.md](../docs/DECISIONES.md#18-frontend-nextjs-11).

```bash
npm install
npm run dev     # http://localhost:3000 — usa API_URL (por defecto http://localhost:5092)
```

- `src/lib/api/`: única capa que habla con la API (solo en el servidor). Devuelve `ApiResult<T>`.
- `src/app/`: páginas (Server Components) y formularios (Server Actions).
- `src/components/`: tabla, paginación, filtros y errores reutilizables.

En Docker corre con `output: "standalone"` y `API_URL=http://api:8080` (ver [Dockerfile](Dockerfile)).
