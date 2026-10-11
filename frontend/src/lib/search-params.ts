// Filtros, orden y página viven en la URL (/matches?status=Played&pageNumber=2): se pueden compartir
// y el botón "atrás" funciona. Estas funciones los leen y los reescriben.

export type SearchParams = Record<string, string | string[] | undefined>;

/** Primer valor del parámetro, sin espacios; vacío = no enviado. */
export function readParam(params: SearchParams, key: string): string | undefined {
  const raw = params[key];
  const value = (Array.isArray(raw) ? raw[0] : raw)?.trim();
  return value ? value : undefined;
}

/** Parámetros comunes de todo listado (§10). Si son inválidos, la API responde 400 y se muestra el error. */
export function readPaging(params: SearchParams) {
  return {
    pageNumber: readParam(params, "pageNumber"),
    pageSize: readParam(params, "pageSize"),
    sortBy: readParam(params, "sortBy"),
    sortDirection: readParam(params, "sortDirection"),
  };
}

/** URL con los parámetros actuales más los cambios indicados (undefined = quitar el parámetro). */
export function hrefWith(path: string, params: SearchParams, changes: Record<string, string | number | undefined>): string {
  const next = new URLSearchParams();
  for (const key of Object.keys(params)) {
    const value = readParam(params, key);
    if (value !== undefined) {
      next.set(key, value);
    }
  }

  for (const [key, value] of Object.entries(changes)) {
    if (value === undefined || value === "") {
      next.delete(key);
    } else {
      next.set(key, String(value));
    }
  }

  const query = next.toString();
  return query ? `${path}?${query}` : path;
}
