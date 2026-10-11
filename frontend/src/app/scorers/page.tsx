import Link from "next/link";

import { ErrorAlert } from "@/components/ErrorAlert";
import { Field, FilterForm } from "@/components/FilterForm";
import { Pagination } from "@/components/Pagination";
import { SortableHeader } from "@/components/SortableHeader";
import { TeamSelect } from "@/components/TeamSelect";
import { getScorers } from "@/lib/api/endpoints";
import { readPaging, readParam, type SearchParams } from "@/lib/search-params";

const PATH = "/scorers";

export default async function ScorersPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const params = await searchParams;
  const result = await getScorers({
    ...readPaging(params),
    teamId: readParam(params, "teamId"),
    playerName: readParam(params, "playerName"),
  });

  const header = (label: string, field: string) => (
    <SortableHeader label={label} field={field} path={PATH} params={params} defaultField="position" />
  );

  return (
    <section className="space-y-4">
      <div>
        <h1 className="text-xl font-semibold">Goleadores</h1>
        <p className="text-sm text-gray-600">Mismos goles = posición compartida.</p>
      </div>

      <FilterForm path={PATH} params={params}>
        <Field label="Equipo">
          <TeamSelect name="teamId" selected={readParam(params, "teamId")} />
        </Field>
        <Field label="Jugador">
          <input name="playerName" defaultValue={readParam(params, "playerName")} />
        </Field>
      </FilterForm>

      {!result.ok ? (
        <ErrorAlert problem={result.problem} />
      ) : (
        <>
          <table className="w-full text-sm">
            <thead>
              <tr>
                {header("Pos", "position")}
                {header("Jugador", "playerName")}
                <th className="px-2 py-1 text-left">#</th>
                {header("Equipo", "teamName")}
                {header("Goles", "goals")}
              </tr>
            </thead>
            <tbody>
              {result.data.data.map((row) => (
                <tr key={row.playerId}>
                  <td className="px-2 py-1">{row.position}</td>
                  <td className="px-2 py-1">{row.playerName}</td>
                  <td className="px-2 py-1">{row.shirtNumber}</td>
                  <td className="px-2 py-1">
                    <Link className="underline" href={`/teams/${row.teamId}`}>
                      {row.teamName}
                    </Link>
                  </td>
                  <td className="px-2 py-1 font-semibold">{row.goals}</td>
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
