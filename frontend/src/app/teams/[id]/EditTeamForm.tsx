"use client";

import { useActionState } from "react";

import { ErrorAlert } from "@/components/ErrorAlert";
import type { Team } from "@/lib/api/types";

import { updateTeamAction, type FormState } from "./actions";

/** Edición completa (PUT): viene precargado con los datos actuales; guardar sin cambios no altera nada. */
export function EditTeamForm({ team }: { team: Team }) {
  const [state, formAction, pending] = useActionState<FormState, FormData>(updateTeamAction.bind(null, team.id), {});

  return (
    <form action={formAction} className="space-y-2 text-sm">
      <div className="flex flex-wrap items-end gap-2">
        <label className="flex flex-col gap-1">
          <span className="text-xs text-gray-600">Nombre</span>
          <input name="name" required maxLength={100} defaultValue={team.name} />
        </label>
        <label className="flex flex-col gap-1">
          <span className="text-xs text-gray-600">Departamento</span>
          <input name="department" required maxLength={100} defaultValue={team.department} />
        </label>
        <button type="submit" disabled={pending}>
          {pending ? "Guardando…" : "Guardar cambios"}
        </button>
      </div>
      {state.saved && !pending && <p className="text-green-700">Equipo actualizado.</p>}
      {state.problem && <ErrorAlert problem={state.problem} />}
    </form>
  );
}
