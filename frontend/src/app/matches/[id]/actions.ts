"use server";

import { refresh } from "next/cache";

import { registerMatchResult } from "@/lib/api/endpoints";
import type { Problem } from "@/lib/api/types";

export type RegisterResultState = { problem?: Problem; saved?: boolean };

const GOALS_PREFIX = "goals.";
const MAX_GOALS_PER_PLAYER = 20;

/**
 * Registra o corrige el resultado (PUT, idempotente). El formulario trae un campo "goals.{playerId}" con los
 * goles de cada jugador; la API recibe un Id por gol (un doblete = dos veces el mismo Id).
 */
export async function registerResultAction(
  matchId: string,
  _previous: RegisterResultState,
  formData: FormData,
): Promise<RegisterResultState> {
  const scorerPlayerIds: string[] = [];

  for (const [field, value] of formData.entries()) {
    if (!field.startsWith(GOALS_PREFIX)) {
      continue;
    }

    const goals = Number(value);
    // Validación de forma en el borde; las reglas del partido (goleador de otro equipo, etc.) las aplica la API.
    if (!Number.isInteger(goals) || goals < 0 || goals > MAX_GOALS_PER_PLAYER) {
      return {
        problem: {
          status: 400,
          code: "Form.InvalidGoals",
          detail: `Los goles de cada jugador deben ser un número entero entre 0 y ${MAX_GOALS_PER_PLAYER}.`,
        },
      };
    }

    const playerId = field.slice(GOALS_PREFIX.length);
    scorerPlayerIds.push(...Array<string>(goals).fill(playerId));
  }

  const result = await registerMatchResult(matchId, scorerPlayerIds);
  if (!result.ok) {
    return { problem: result.problem };
  }

  // Vuelve a renderizar la página con el resultado nuevo (marcador, goles) traído de la API.
  refresh();
  return { saved: true };
}
