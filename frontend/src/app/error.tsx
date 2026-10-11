"use client";

// Red de seguridad para errores INESPERADOS al renderizar (un bug). Los errores esperados de la API
// (400/404/409, API caída) no llegan aquí: cada página los muestra con <ErrorAlert>.
export default function Error({ retry }: { error: Error & { digest?: string }; retry: () => void }) {
  return (
    <div role="alert" className="space-y-3">
      <h1 className="text-lg font-semibold">Algo salió mal</h1>
      <p className="text-sm">Ocurrió un error inesperado al mostrar esta página.</p>
      {/* retry() vuelve a pedir los datos y a renderizar (en Next 16 reemplaza a reset() para este uso). */}
      <button onClick={() => retry()}>Reintentar</button>
    </div>
  );
}
