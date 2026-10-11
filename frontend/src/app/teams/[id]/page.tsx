import Link from "next/link";
import { notFound } from "next/navigation";

import { ErrorAlert } from "@/components/ErrorAlert";
import { Field, FilterForm } from "@/components/FilterForm";
import { Pagination } from "@/components/Pagination";
import { SortableHeader } from "@/components/SortableHeader";
import { getTeam, getTeamPlayers } from "@/lib/api/endpoints";
import { readPaging, readParam, type SearchParams } from "@/lib/search-params";

export default async function TeamPage({
  params,
  searchParams,
}: {
  params: Promise<{ id: string }>;
  searchParams: Promise<SearchParams>;
}) {
  const { id } = await params;
  const query = await searchParams;
  const path = `/teams/${id}`;

  const team = await getTeam(id);
  if (!team.ok) {
    if (team.problem.status === 404) {
      notFound();
    }
    return <ErrorAlert problem={team.problem} />;
  }

  const players = await getTeamPlayers(id, { ...readPaging(query), name: readParam(query, "name") });

  const header = (label: string, field: string) => (
    <SortableHeader label={label} field={field} path={path} params={query} defaultField="shirtNumber" />
  );

  return (
    <section className="space-y-4">
      <div>
        <h1 className="text-xl font-semibold">{team.data.name}</h1>
        <p className="text-sm text-gray-600">{team.data.department}</p>
        <Link className="text-sm underline" href={`/matches?teamId=${team.data.id}`}>
          Ver sus partidos
        </Link>
      </div>

      <h2 className="font-semibold">Jugadores</h2>
      <FilterForm path={path} params={query}>
        <Field label="Nombre">
          <input name="name" defaultValue={readParam(query, "name")} />
        </Field>
      </FilterForm>

      {!players.ok ? (
        <ErrorAlert problem={players.problem} />
      ) : (
        <>
          <table className="w-full text-sm">
            <thead>
              <tr>
                {header("#", "shirtNumber")}
                {header("Nombre", "fullName")}
              </tr>
            </thead>
            <tbody>
              {players.data.data.map((player) => (
                <tr key={player.id}>
                  <td className="px-2 py-1">{player.shirtNumber}</td>
                  <td className="px-2 py-1">{player.fullName}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <Pagination path={path} params={query} page={players.data} />
        </>
      )}
    </section>
  );
}
