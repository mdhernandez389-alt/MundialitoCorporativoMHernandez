import Link from "next/link";

import type { PagedResult } from "@/lib/api/types";
import { hrefWith, type SearchParams } from "@/lib/search-params";

/** Paginación de cualquier listado: conserva filtros y orden, solo cambia pageNumber. */
export function Pagination<T>({ path, params, page }: { path: string; params: SearchParams; page: PagedResult<T> }) {
  const hasPrevious = page.pageNumber > 1;
  const hasNext = page.pageNumber < page.totalPages;

  return (
    <nav className="mt-3 flex items-center gap-4 text-sm">
      {hasPrevious ? (
        <Link className="underline" href={hrefWith(path, params, { pageNumber: page.pageNumber - 1 })}>
          ← Anterior
        </Link>
      ) : (
        <span className="text-gray-400">← Anterior</span>
      )}
      <span>
        Página {page.pageNumber} de {Math.max(page.totalPages, 1)} · {page.totalRecords} registros
      </span>
      {hasNext ? (
        <Link className="underline" href={hrefWith(path, params, { pageNumber: page.pageNumber + 1 })}>
          Siguiente →
        </Link>
      ) : (
        <span className="text-gray-400">Siguiente →</span>
      )}
    </nav>
  );
}
