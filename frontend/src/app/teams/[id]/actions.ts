"use server";

import { refresh } from "next/cache";
import { redirect } from "next/navigation";

import { deleteTeam, registerPlayer, updateTeam } from "@/lib/api/endpoints";
import type { Problem } from "@/lib/api/types";

export type FormState = { problem?: Problem; saved?: boolean };

const text = (formData: FormData, field: string) => String(formData.get(field) ?? "");

/** PUT: reemplaza nombre y departamento. Repetirlo con los mismos datos no cambia nada. */
export async function updateTeamAction(teamId: string, _previous: FormState, formData: FormData): Promise<FormState> {
  const result = await updateTeam(teamId, { name: text(formData, "name"), department: text(formData, "department") });
  if (!result.ok) {
    // 400 (validación), 404 (ya no existe), 409 (nombre de otro equipo).
    return { problem: result.problem };
  }

  refresh();
  return { saved: true };
}

/** DELETE: un equipo con partidos no se puede borrar (409, la tabla quedaría inconsistente). */
export async function deleteTeamAction(teamId: string): Promise<FormState> {
  const result = await deleteTeam(teamId);
  if (!result.ok) {
    return { problem: result.problem };
  }

  redirect("/teams");
}

/**
 * POST idempotente: la key viene oculta en el formulario. Tras el éxito, refresh() vuelve a renderizar la página:
 * el jugador aparece en la lista y el formulario recibe una key nueva para el siguiente.
 */
export async function registerPlayerAction(
  teamId: string,
  _previous: FormState,
  formData: FormData,
): Promise<FormState> {
  const result = await registerPlayer(
    teamId,
    // Un dorsal vacío o no numérico se manda como 0: lo rechaza el dominio con su código (Player.InvalidShirtNumber).
    { fullName: text(formData, "fullName"), shirtNumber: Number.parseInt(text(formData, "shirtNumber"), 10) || 0 },
    text(formData, "idempotencyKey"),
  );
  if (!result.ok) {
    // 400 (validación), 409 (dorsal ocupado), 422 (key reusada con otros datos).
    return { problem: result.problem };
  }

  refresh();
  return { saved: true };
}
