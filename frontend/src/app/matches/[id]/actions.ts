"use server";

import { refresh } from "next/cache";

import { registerMatchResult } from "@/lib/api/endpoints";
import type { Problem } from "@/lib/api/types";

export type RegisterResultState = { problem?: Problem; saved?: boolean };

// Prefijo del campo → lista del request a la que van esos goles.
const GOAL_FIELDS = { "goals.": "scorerPlayerIds", "ownGoals.": "ownGoalPlayerIds" } as const;
const MAX_GOALS_PER_PLAYER = 20;

/**
 * Registra o corrige el resultado (PUT, idempotente). El formulario trae por jugador un campo "goals.{playerId}"
 * y otro "ownGoals.{playerId}"; la API recibe un Id por gol (un doblete = dos veces el mismo Id).
 */
export async function registerResultAction(
  matchId: string,
  _previous: RegisterResultState,
  formData: FormData,
): Promise<RegisterResultState> {
  const ids = { scorerPlayerIds: [] as string[], ownGoalPlayerIds: [] as string[] };

  for (const [field, value] of formData.entries()) {
    const prefix = Object.keys(GOAL_FIELDS).find((p) => field.startsWith(p)) as keyof typeof GOAL_FIELDS | undefined;
    if (!prefix) {
      continue;
    }

    const goals = Number(value);
    // Validación de forma en el borde; las reglas del partido (goleador de otro equipo, etc.) las aplica la API.
    if (!Number.isInteger(goals) || goals < 0 || goals > MAX_GOALS_PER_PLAYER) {
      return {
        problem: {
          status: 400,
          code: "Form.InvalidGoals",
          detail: `Los goles y autogoles de cada jugador deben ser un número entero entre 0 y ${MAX_GOALS_PER_PLAYER}.`,
        },
      };
    }

    const playerId = field.slice(prefix.length);
    ids[GOAL_FIELDS[prefix]].push(...Array<string>(goals).fill(playerId));
  }

  const result = await registerMatchResult(matchId, ids.scorerPlayerIds, ids.ownGoalPlayerIds);
  if (!result.ok) {
    return { problem: result.problem };
  }

  // Vuelve a renderizar la página con el resultado nuevo (marcador, goles) traído de la API.
  refresh();
  return { saved: true };
}
