"use client";

import { useActionState } from "react";

import { ErrorAlert } from "@/components/ErrorAlert";

import { registerPlayerAction, type FormState } from "./actions";

/** Alta de jugador con Idempotency-Key (§6). Tras guardar, el formulario se vacía para cargar el siguiente. */
export function RegisterPlayerForm({ teamId, idempotencyKey }: { teamId: string; idempotencyKey: string }) {
  const [state, formAction, pending] = useActionState<FormState, FormData>(
    registerPlayerAction.bind(null, teamId),
    {},
  );

  return (
    <form action={formAction} className="space-y-2 text-sm">
      <div className="flex flex-wrap items-end gap-2">
        <label className="flex flex-col gap-1">
          <span className="text-xs text-gray-600">Nombre completo</span>
          <input name="fullName" required maxLength={100} />
        </label>
        <label className="flex flex-col gap-1">
          <span className="text-xs text-gray-600">Dorsal</span>
          <input type="number" name="shirtNumber" required min={1} max={99} className="w-20" />
        </label>
        <input type="hidden" name="idempotencyKey" value={idempotencyKey} />
        <button type="submit" disabled={pending}>
          {pending ? "Registrando…" : "Registrar jugador"}
        </button>
      </div>
      {state.saved && !pending && <p className="text-green-700">Jugador registrado.</p>}
      {state.problem && <ErrorAlert problem={state.problem} />}
    </form>
  );
}
