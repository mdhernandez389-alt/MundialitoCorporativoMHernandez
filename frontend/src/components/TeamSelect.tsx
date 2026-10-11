import { getTeams } from "@/lib/api/endpoints";

/** Lista de equipos para filtrar. Pide hasta 100 (el máximo de la API); el torneo tiene pocos equipos. */
export async function TeamSelect({ name, selected }: { name: string; selected?: string }) {
  const result = await getTeams({ pageSize: 100, sortBy: "name" });
  const teams = result.ok ? result.data.data : [];

  return (
    <select name={name} defaultValue={selected ?? ""}>
      <option value="">Todos</option>
      {teams.map((team) => (
        <option key={team.id} value={team.id}>
          {team.name}
        </option>
      ))}
    </select>
  );
}
