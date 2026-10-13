import Link from "next/link";
import { notFound } from "next/navigation";
import { connection } from "next/server";

import { ErrorAlert } from "@/components/ErrorAlert";
import { Field, FilterForm } from "@/components/FilterForm";
import { Pagination } from "@/components/Pagination";
import { SortableHeader } from "@/components/SortableHeader";
import { getTeam, getTeamPlayers } from "@/lib/api/endpoints";
import { readPaging, readParam, type SearchParams } from "@/lib/search-params";

import { DeleteTeamButton } from "./DeleteTeamButton";
import { EditTeamForm } from "./EditTeamForm";
import { RegisterPlayerForm } from "./RegisterPlayerForm";

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

      <div className="space-y-2">
        <h2 className="font-semibold">Registrar jugador</h2>
        <NewPlayer teamId={team.data.id} />
      </div>

      <div className="space-y-2">
        <h2 className="font-semibold">Editar equipo</h2>
        <EditTeamForm team={team.data} />
      </div>

      <div className="space-y-2">
        <h2 className="font-semibold">Eliminar equipo</h2>
        <p className="text-sm text-gray-600">Solo si no tiene partidos. Se eliminan también sus jugadores.</p>
        <DeleteTeamButton teamId={team.data.id} teamName={team.data.name} />
      </div>
    </section>
  );
}

/** Genera una Idempotency-Key por cada vez que se muestra el formulario (también tras registrar un jugador). */
async function NewPlayer({ teamId }: { teamId: string }) {
  // Con Cache Components, un valor distinto en cada request (randomUUID) exige pasar primero por connection().
  await connection();
  return <RegisterPlayerForm teamId={teamId} idempotencyKey={crypto.randomUUID()} />;
}
