"use client";

import { useActionState } from "react";

import { ErrorAlert } from "@/components/ErrorAlert";
import type { Team } from "@/lib/api/types";

import { scheduleMatchAction, type ScheduleMatchState } from "./actions";

/**
 * Programar un partido. Los equipos llegan del servidor (la página los consulta); que local y visitante no sean
 * el mismo lo valida el dominio (Match.SameTeams) y se muestra aquí.
 */
export function ScheduleMatchForm({ teams, idempotencyKey }: { teams: Team[]; idempotencyKey: string }) {
  const [state, formAction, pending] = useActionState<ScheduleMatchState, FormData>(scheduleMatchAction, {});

  const teamSelect = (name: string) => (
    <select name={name} required defaultValue="">
      <option value="" disabled>
        Elegir…
      </option>
      {teams.map((team) => (
        <option key={team.id} value={team.id}>
          {team.name}
        </option>
      ))}
    </select>
  );

  return (
    <form action={formAction} className="space-y-2 text-sm">
      <div className="flex flex-wrap items-end gap-2">
        <label className="flex flex-col gap-1">
          <span className="text-xs text-gray-600">Local</span>
          {teamSelect("homeTeamId")}
        </label>
        <label className="flex flex-col gap-1">
          <span className="text-xs text-gray-600">Visitante</span>
          {teamSelect("awayTeamId")}
        </label>
        <label className="flex flex-col gap-1">
          <span className="text-xs text-gray-600">Fecha y hora (El Salvador)</span>
          <input type="datetime-local" name="scheduledAt" required />
        </label>
        <input type="hidden" name="idempotencyKey" value={idempotencyKey} />
        <button type="submit" disabled={pending}>
          {pending ? "Programando…" : "Programar partido"}
        </button>
      </div>
      {state.problem && <ErrorAlert problem={state.problem} />}
    </form>
  );
}
