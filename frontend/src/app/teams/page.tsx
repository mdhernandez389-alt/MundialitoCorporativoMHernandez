import Link from "next/link";
import { connection } from "next/server";

import { ErrorAlert } from "@/components/ErrorAlert";
import { Field, FilterForm } from "@/components/FilterForm";
import { Pagination } from "@/components/Pagination";
import { SortableHeader } from "@/components/SortableHeader";
import { getTeams } from "@/lib/api/endpoints";
import { readPaging, readParam, type SearchParams } from "@/lib/search-params";

import { CreateTeamForm } from "./CreateTeamForm";

const PATH = "/teams";

export default async function TeamsPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const params = await searchParams;
  const result = await getTeams({
    ...readPaging(params),
    name: readParam(params, "name"),
    department: readParam(params, "department"),
  });

  const header = (label: string, field: string) => (
    <SortableHeader label={label} field={field} path={PATH} params={params} defaultField="name" />
  );

  return (
    <section className="space-y-6">
      <div className="space-y-4">
        <h1 className="text-xl font-semibold">Equipos</h1>

        <FilterForm path={PATH} params={params}>
          <Field label="Nombre">
            <input name="name" defaultValue={readParam(params, "name")} />
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
                  {header("Nombre", "name")}
                  {header("Departamento", "department")}
                </tr>
              </thead>
              <tbody>
                {result.data.data.map((team) => (
                  <tr key={team.id}>
                    <td className="px-2 py-1">
                      <Link className="underline" href={`/teams/${team.id}`}>
                        {team.name}
                      </Link>
                    </td>
                    <td className="px-2 py-1">{team.department}</td>
                  </tr>
                ))}
              </tbody>
            </table>
            <Pagination path={PATH} params={params} page={result.data} />
          </>
        )}
      </div>

      <div className="space-y-2">
        <h2 className="font-semibold">Nuevo equipo</h2>
        <NewTeam />
      </div>
    </section>
  );
}

/** Genera una Idempotency-Key por cada vez que se muestra el formulario. */
async function NewTeam() {
  // Con Cache Components, un valor distinto en cada request (randomUUID) exige pasar primero por connection().
  await connection();
  return <CreateTeamForm idempotencyKey={crypto.randomUUID()} />;
}
