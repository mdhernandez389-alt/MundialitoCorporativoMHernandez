import Link from "next/link";

import { ErrorAlert } from "@/components/ErrorAlert";
import { Field, FilterForm } from "@/components/FilterForm";
import { Pagination } from "@/components/Pagination";
import { SortableHeader } from "@/components/SortableHeader";
import { TeamSelect } from "@/components/TeamSelect";
import { getMatches } from "@/lib/api/endpoints";
import { endOfDay, formatDateTime, startOfDay, statusLabel } from "@/lib/format";
import { readPaging, readParam, type SearchParams } from "@/lib/search-params";

const PATH = "/matches";

/** Partidos con los filtros del §8: fecha (desde/hasta), equipo y estado. */
export default async function MatchesPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const params = await searchParams;
  const from = readParam(params, "from");
  const to = readParam(params, "to");

  const result = await getMatches({
    ...readPaging(params),
    // La URL guarda solo la fecha (2026-10-10); a la API se le manda con hora y zona: el día completo.
    from: startOfDay(from),
    to: endOfDay(to),
    teamId: readParam(params, "teamId"),
    status: readParam(params, "status"),
  });

  const header = (label: string, field: string) => (
    <SortableHeader label={label} field={field} path={PATH} params={params} defaultField="scheduledAt" />
  );

  return (
    <section className="space-y-4">
      <h1 className="text-xl font-semibold">Partidos</h1>

      <FilterForm path={PATH} params={params}>
        <Field label="Desde">
          <input type="date" name="from" defaultValue={from} />
        </Field>
        <Field label="Hasta">
          <input type="date" name="to" defaultValue={to} />
        </Field>
        <Field label="Equipo">
          <TeamSelect name="teamId" selected={readParam(params, "teamId")} />
        </Field>
        <Field label="Estado">
          <select name="status" defaultValue={readParam(params, "status") ?? ""}>
            <option value="">Todos</option>
            <option value="Scheduled">Programado</option>
            <option value="Played">Jugado</option>
          </select>
        </Field>
      </FilterForm>

      {!result.ok ? (
        <ErrorAlert problem={result.problem} />
      ) : (
        <>
          <table className="w-full text-sm">
            <thead>
              <tr>
                {header("Fecha", "scheduledAt")}
                <th className="px-2 py-1 text-left">Partido</th>
                {header("Estado", "status")}
                <th className="px-2 py-1 text-left"></th>
              </tr>
            </thead>
            <tbody>
              {result.data.data.map((match) => (
                <tr key={match.id}>
                  <td className="px-2 py-1">{formatDateTime(match.scheduledAtUtc)}</td>
                  <td className="px-2 py-1">
                    {match.homeTeamName}{" "}
                    <strong>{match.status === "Played" ? `${match.homeGoals} - ${match.awayGoals}` : "vs"}</strong>{" "}
                    {match.awayTeamName}
                  </td>
                  <td className="px-2 py-1">{statusLabel(match.status)}</td>
                  <td className="px-2 py-1">
                    <Link className="underline" href={`/matches/${match.id}`}>
                      {match.status === "Played" ? "Ver / corregir" : "Registrar resultado"}
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          <Pagination path={PATH} params={params} page={result.data} />
        </>
      )}
    </section>
  );
}
