"use client";

import { useActionState } from "react";

import { ErrorAlert } from "@/components/ErrorAlert";

import { createTeamAction, type CreateTeamState } from "./actions";

/**
 * Formulario de alta. Es Client Component solo para mostrar el estado (enviando, error); el envío
 * lo procesa la Server Action en el servidor.
 */
export function CreateTeamForm({ idempotencyKey }: { idempotencyKey: string }) {
  const [state, formAction, pending] = useActionState<CreateTeamState, FormData>(createTeamAction, {});

  return (
    <form action={formAction} className="space-y-2 text-sm">
      <div className="flex flex-wrap items-end gap-2">
        <label className="flex flex-col gap-1">
          <span className="text-xs text-gray-600">Nombre</span>
          <input name="name" required maxLength={100} />
        </label>
        <label className="flex flex-col gap-1">
          <span className="text-xs text-gray-600">Departamento</span>
          <input name="department" required maxLength={100} />
        </label>
        {/* La misma key en cada reenvío de este formulario: la API no crea el equipo dos veces. */}
        <input type="hidden" name="idempotencyKey" value={idempotencyKey} />
        {/* Deshabilitado mientras se envía: evita el doble clic (la key cubre además los reintentos de red). */}
        <button type="submit" disabled={pending}>
          {pending ? "Creando…" : "Crear equipo"}
        </button>
      </div>
      {state.problem && <ErrorAlert problem={state.problem} />}
    </form>
  );
}
