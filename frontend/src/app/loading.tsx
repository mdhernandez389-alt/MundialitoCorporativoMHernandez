// Límite de Suspense de todas las páginas: con Cache Components, las lecturas a la API (dinámicas, por
// request) deben estar dentro de uno. Mientras la API responde se muestra esto.
export default function Loading() {
  return <p className="text-sm text-gray-500">Cargando…</p>;
}
