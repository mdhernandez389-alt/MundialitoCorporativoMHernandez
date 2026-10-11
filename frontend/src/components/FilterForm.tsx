import Form from "next/form";
import Link from "next/link";
import type { ReactNode } from "react";

import { readParam, type SearchParams } from "@/lib/search-params";

/**
 * Formulario GET de filtros: al enviarlo, los campos quedan en la URL y el servidor renderiza la página
 * filtrada (funciona incluso sin JavaScript). Conserva el orden elegido; la página vuelve a 1.
 */
export function FilterForm({ path, params, children }: { path: string; params: SearchParams; children: ReactNode }) {
  const sortBy = readParam(params, "sortBy");
  const sortDirection = readParam(params, "sortDirection");

  return (
    <Form action={path} className="flex flex-wrap items-end gap-2 text-sm">
      {children}
      {sortBy && <input type="hidden" name="sortBy" value={sortBy} />}
      {sortDirection && <input type="hidden" name="sortDirection" value={sortDirection} />}
      <button type="submit">Filtrar</button>
      <Link className="underline" href={path}>
        Limpiar
      </Link>
    </Form>
  );
}

/** Campo con etiqueta para los filtros. */
export function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label className="flex flex-col gap-1">
      <span className="text-xs text-gray-600">{label}</span>
      {children}
    </label>
  );
}
