"use server";

import { redirect } from "next/navigation";

import { createTeam } from "@/lib/api/endpoints";
import type { Problem } from "@/lib/api/types";

export type CreateTeamState = { problem?: Problem };

/**
 * Server Action: corre en el servidor de Next, no en el navegador. La Idempotency-Key viene oculta en el
 * formulario: un doble clic o un reintento reusa la misma key y la API devuelve el mismo equipo (§6).
 */
export async function createTeamAction(_previous: CreateTeamState, formData: FormData): Promise<CreateTeamState> {
  const result = await createTeam(
    {
      name: String(formData.get("name") ?? ""),
      department: String(formData.get("department") ?? ""),
    },
    String(formData.get("idempotencyKey") ?? ""),
  );

  if (!result.ok) {
    // 400 (validación), 409 (nombre duplicado), 422 (key reusada)...: se muestran en el formulario.
    return { problem: result.problem };
  }

  // Éxito: a la página del equipo nuevo. Ahí se carga un formulario nuevo, con otra key.
  redirect(`/teams/${result.data.id}`);
}
