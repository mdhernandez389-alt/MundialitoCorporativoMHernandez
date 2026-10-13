"use client";

import { useActionState } from "react";

import { ErrorAlert } from "@/components/ErrorAlert";

import { deleteTeamAction, type FormState } from "./actions";

/**
 * Pide confirmación en el navegador antes de enviar. Si el equipo tiene partidos, la API responde 409
 * (Team.HasMatches) y se muestra aquí: el conflicto se ve sin salir de la página.
 */
export function DeleteTeamButton({ teamId, teamName }: { teamId: string; teamName: string }) {
  const [state, formAction, pending] = useActionState<FormState>(deleteTeamAction.bind(null, teamId), {});

  return (
    <form
      action={formAction}
      onSubmit={(event) => {
        if (!window.confirm(`¿Eliminar el equipo "${teamName}" y sus jugadores?`)) {
          event.preventDefault();
        }
      }}
      className="space-y-2 text-sm"
    >
      <button type="submit" disabled={pending} className="danger">
        {pending ? "Eliminando…" : "Eliminar equipo"}
      </button>
      {state.problem && <ErrorAlert problem={state.problem} />}
    </form>
  );
}
