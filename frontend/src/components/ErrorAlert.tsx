import type { Problem } from "@/lib/api/types";

/**
 * Muestra un error de la API de forma uniforme: el mensaje para la persona, el código para quien depura
 * y el traceId para buscar el request en Seq y Jaeger (§7).
 */
export function ErrorAlert({ problem }: { problem: Problem }) {
  return (
    <div role="alert" className="rounded border border-red-300 bg-red-50 p-3 text-sm text-red-900">
      <p className="font-medium">{problem.detail}</p>
      <p className="mt-1 text-xs text-red-700">
        {problem.code} · HTTP {problem.status}
        {problem.traceId && <> · traceId {problem.traceId}</>}
      </p>
    </div>
  );
}
