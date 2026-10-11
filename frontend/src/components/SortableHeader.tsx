import Link from "next/link";

import { hrefWith, readParam, type SearchParams } from "@/lib/search-params";

/**
 * Encabezado de columna que ordena el listado (§10: sortBy + sortDirection en la URL).
 * Un clic ordena ascendente; otro clic sobre la misma columna invierte. Cambiar el orden vuelve a la página 1.
 */
export function SortableHeader({
  label,
  field,
  path,
  params,
  defaultField,
}: {
  label: string;
  field: string;
  path: string;
  params: SearchParams;
  defaultField: string;
}) {
  const currentField = readParam(params, "sortBy") ?? defaultField;
  const currentDirection = readParam(params, "sortDirection") ?? "asc";
  const isActive = currentField.toLowerCase() === field.toLowerCase();
  const nextDirection = isActive && currentDirection === "asc" ? "desc" : "asc";

  return (
    <th className="px-2 py-1 text-left">
      <Link
        className="underline-offset-2 hover:underline"
        href={hrefWith(path, params, { sortBy: field, sortDirection: nextDirection, pageNumber: undefined })}
      >
        {label}
        {isActive && (currentDirection === "asc" ? " ▲" : " ▼")}
      </Link>
    </th>
  );
}
