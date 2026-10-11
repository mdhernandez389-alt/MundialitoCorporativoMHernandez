"use client";

import { useActionState } from "react";

import { ErrorAlert } from "@/components/ErrorAlert";
import type { Player } from "@/lib/api/types";

import { registerResultAction, type RegisterResultState } from "./actions";

type Squad = { teamName: string; players: Player[] };

/**
 * Un campo de goles por jugador de cada equipo. Si el partido ya se jugó, viene precargado con el
 * resultado actual: guardar sin cambios no altera nada (la API es idempotente) y cambiar algo lo corrige.
 */
export function RegisterResultForm({
  matchId,
  home,
  away,
  goalsByPlayer,
}: {
  matchId: string;
  home: Squad;
  away: Squad;
  goalsByPlayer: Record<string, number>;
}) {
  const [state, formAction, pending] = useActionState<RegisterResultState, FormData>(
    registerResultAction.bind(null, matchId),
    {},
  );

  return (
    <form action={formAction} className="space-y-3 text-sm">
      <div className="grid gap-6 sm:grid-cols-2">
        {[home, away].map((squad) => (
          <fieldset key={squad.teamName} className="space-y-1">
            <legend className="font-semibold">{squad.teamName}</legend>
            {squad.players.map((player) => (
              <label key={player.id} className="flex items-center justify-between gap-2">
                <span>
                  {player.shirtNumber} · {player.fullName}
                </span>
                <input
                  type="number"
                  name={`goals.${player.id}`}
                  min={0}
                  max={20}
                  defaultValue={goalsByPlayer[player.id] ?? 0}
                  className="w-16"
                />
              </label>
            ))}
          </fieldset>
        ))}
      </div>
      <button type="submit" disabled={pending}>
        {pending ? "Guardando…" : "Guardar resultado"}
      </button>
      {state.saved && !pending && <p className="text-green-700">Resultado guardado. La tabla ya está actualizada.</p>}
      {state.problem && <ErrorAlert problem={state.problem} />}
    </form>
  );
}
