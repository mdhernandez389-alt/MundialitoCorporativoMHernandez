import Link from "next/link";

import { ErrorAlert } from "@/components/ErrorAlert";
import { Field, FilterForm } from "@/components/FilterForm";
import { Pagination } from "@/components/Pagination";
import { SortableHeader } from "@/components/SortableHeader";
import { getStandings } from "@/lib/api/endpoints";
import { formatGoalDifference } from "@/lib/format";
import { readPaging, readParam, type SearchParams } from "@/lib/search-params";

const PATH = "/";

/** Tabla de posiciones (§8): la posición la calcula la API con el orden oficial; aquí solo se muestra. */
export default async function StandingsPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const params = await searchParams;
  const result = await getStandings({
    ...readPaging(params),
    teamName: readParam(params, "teamName"),
    department: readParam(params, "department"),
  });

  const header = (label: string, field: string) => (
    <SortableHeader label={label} field={field} path={PATH} params={params} defaultField="position" />
  );

  return (
    <section className="space-y-4">
      <div>
        <h1 className="text-xl font-semibold">Tabla de posiciones</h1>
        <p className="text-sm text-gray-600">
          Orden oficial: puntos → diferencia de gol → goles a favor. Empate total = posición compartida.
        </p>
      </div>

      <FilterForm path={PATH} params={params}>
        <Field label="Equipo">
          <input name="teamName" defaultValue={readParam(params, "teamName")} />
        </Field>
        <Field label="Departamento">
          <input name="department" defaultValue={readParam(params, "department")} />
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
                {header("Equipo", "teamName")}
                <th className="px-2 py-1 text-right">PJ</th>
                <th className="px-2 py-1 text-right">G</th>
                <th className="px-2 py-1 text-right">E</th>
                <th className="px-2 py-1 text-right">P</th>
                {header("GF", "goalsFor")}
                <th className="px-2 py-1 text-right">GC</th>
                {header("DG", "goalDifference")}
                {header("Pts", "points")}
              </tr>
            </thead>
            <tbody>
              {result.data.data.map((row) => (
                <tr key={row.teamId}>
                  <td className="px-2 py-1">{row.position}</td>
                  <td className="px-2 py-1">
                    <Link className="underline" href={`/teams/${row.teamId}`}>
                      {row.teamName}
                    </Link>
                  </td>
                  <td className="px-2 py-1 text-right">{row.played}</td>
                  <td className="px-2 py-1 text-right">{row.won}</td>
                  <td className="px-2 py-1 text-right">{row.drawn}</td>
                  <td className="px-2 py-1 text-right">{row.lost}</td>
                  <td className="px-2 py-1">{row.goalsFor}</td>
                  <td className="px-2 py-1 text-right">{row.goalsAgainst}</td>
                  <td className="px-2 py-1">{formatGoalDifference(row.goalDifference)}</td>
                  <td className="px-2 py-1 font-semibold">{row.points}</td>
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
