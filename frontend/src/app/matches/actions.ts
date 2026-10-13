"use server";

import { redirect } from "next/navigation";

import { scheduleMatch } from "@/lib/api/endpoints";
import type { Problem } from "@/lib/api/types";
import { fromLocalDateTime } from "@/lib/format";

export type ScheduleMatchState = { problem?: Problem };

/**
 * POST idempotente: la key viene oculta en el formulario. Tras el éxito se va al detalle del partido nuevo,
 * que es donde se registra su resultado (y el formulario de esta página vuelve con otra key).
 */
export async function scheduleMatchAction(
  _previous: ScheduleMatchState,
  formData: FormData,
): Promise<ScheduleMatchState> {
  const result = await scheduleMatch(
    {
      homeTeamId: String(formData.get("homeTeamId") ?? ""),
      awayTeamId: String(formData.get("awayTeamId") ?? ""),
      scheduledAt: fromLocalDateTime(String(formData.get("scheduledAt") ?? "")),
    },
    String(formData.get("idempotencyKey") ?? ""),
  );

  if (!result.ok) {
    // 400 (mismo equipo, datos faltantes), 404 (equipo inexistente), 422 (key reusada).
    return { problem: result.problem };
  }

  redirect(`/matches/${result.data.id}`);
}
