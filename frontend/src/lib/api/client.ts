import "server-only";

import type { ApiResult, Problem } from "./types";

// Solo existe en el servidor de Next (sin NEXT_PUBLIC_): el navegador nunca conoce la URL de la API.
// En Docker apunta a la red interna (http://api:8080).
const API_URL = process.env.API_URL ?? "http://localhost:5092";

export type Query = Record<string, string | number | undefined>;

type Method = "GET" | "POST" | "PUT" | "PATCH" | "DELETE";

export function apiGet<T>(path: string, query?: Query): Promise<ApiResult<T>> {
  return send<T>("GET", path + toQueryString(query));
}

export function apiSend<T>(
  method: Exclude<Method, "GET">,
  path: string,
  body?: unknown,
  headers?: Record<string, string>,
): Promise<ApiResult<T>> {
  return send<T>(method, path, body, headers);
}

/**
 * Única puerta hacia la API: arma el request y convierte TODA respuesta en un ApiResult.
 * Las páginas nunca ven un Response ni un try/catch: reciben datos o un Problem.
 */
async function send<T>(
  method: Method,
  path: string,
  body?: unknown,
  headers?: Record<string, string>,
): Promise<ApiResult<T>> {
  let response: Response;
  try {
    response = await fetch(`${API_URL}${path}`, {
      method,
      headers: { Accept: "application/json", "Content-Type": "application/json", ...headers },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
  } catch {
    // La API no responde (caída, red, reiniciando): es esperable, se muestra como error y no rompe la página.
    return {
      ok: false,
      problem: {
        status: 503,
        code: "Api.Unavailable",
        detail: "No se pudo conectar con la API. Intenta de nuevo en unos segundos.",
      },
    };
  }

  if (!response.ok) {
    return { ok: false, problem: await toProblem(response) };
  }

  // 204 No Content (PUT, PATCH, DELETE): éxito sin cuerpo.
  const data = response.status === 204 ? undefined : await response.json();
  return { ok: true, data: data as T };
}

/**
 * Traduce una respuesta de error de la API a un Problem.
 * La API responde ProblemDetails: { status, detail, code, traceId, ... }.
 */
async function toProblem(response: Response): Promise<Problem> {
  let body: Partial<Problem> = {};
  try {
    body = (await response.json()) as Partial<Problem>;
  } catch {
    // Cuerpo vacío o no JSON (p. ej. un proxy que responde HTML): se arma el Problem solo con el status.
  }

  return {
    // El status HTTP real manda: el del cuerpo podría faltar o no coincidir.
    status: response.status,
    // Cada campo con respaldo: un error de la API nunca rompe la página, siempre se puede mostrar.
    code: body.code ?? `Http.${response.status}`,
    detail: body.detail ?? "La API respondió con un error inesperado.",
    traceId: body.traceId,
  };
}

function toQueryString(query?: Query): string {
  if (!query) {
    return "";
  }

  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(query)) {
    if (value !== undefined && value !== "") {
      params.set(key, String(value));
    }
  }

  const text = params.toString();
  return text ? `?${text}` : "";
}
