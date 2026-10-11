import Link from "next/link";

export default function NotFound() {
  return (
    <div className="space-y-3">
      <h1 className="text-lg font-semibold">No encontrado</h1>
      <p className="text-sm">El recurso que buscas no existe.</p>
      <Link className="text-sm underline" href="/">
        Volver a la tabla de posiciones
      </Link>
    </div>
  );
}
