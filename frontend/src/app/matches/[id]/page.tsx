import Link from "next/link";
import { notFound } from "next/navigation";

import { ErrorAlert } from "@/components/ErrorAlert";
import { getMatch, getTeamPlayers } from "@/lib/api/endpoints";
import { formatDateTime, statusLabel } from "@/lib/format";

import { RegisterResultForm } from "./RegisterResultForm";

export default async function MatchPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;

  const match = await getMatch(id);
  if (!match.ok) {
    if (match.problem.status === 404) {
      notFound();
    }
    return <ErrorAlert problem={match.problem} />;
  }

  const m = match.data;
  // Los dos planteles en paralelo (un equipo tiene pocos jugadores: 100 es el máximo de la API).
  const [homePlayers, awayPlayers] = await Promise.all([
    getTeamPlayers(m.homeTeamId, { pageSize: 100 }),
    getTeamPlayers(m.awayTeamId, { pageSize: 100 }),
  ]);

  // Goles actuales por jugador, para precargar el formulario cuando se corrige un resultado.
  const goalsByPlayer: Record<string, number> = {};
  for (const goal of m.goals) {
    goalsByPlayer[goal.playerId] = (goalsByPlayer[goal.playerId] ?? 0) + 1;
  }

  return (
    <section className="space-y-6">
      <div>
        <Link className="text-sm underline" href="/matches">
          ← Partidos
        </Link>
        <h1 className="mt-2 text-xl font-semibold">
          {m.homeTeamName} {m.status === "Played" ? `${m.homeGoals} - ${m.awayGoals}` : "vs"} {m.awayTeamName}
        </h1>
        <p className="text-sm text-gray-600">
          {formatDateTime(m.scheduledAtUtc)} · {statusLabel(m.status)}
        </p>
      </div>

      {m.goals.length > 0 && (
        <div>
          <h2 className="font-semibold">Goles</h2>
          <ul className="list-inside list-disc text-sm">
            {m.goals.map((goal, index) => (
              <li key={`${goal.playerId}-${index}`}>
                {goal.playerName} ({goal.teamId === m.homeTeamId ? m.homeTeamName : m.awayTeamName})
              </li>
            ))}
          </ul>
        </div>
      )}

      <div className="space-y-2">
        <h2 className="font-semibold">{m.status === "Played" ? "Corregir resultado" : "Registrar resultado"}</h2>
        {!homePlayers.ok ? (
          <ErrorAlert problem={homePlayers.problem} />
        ) : !awayPlayers.ok ? (
          <ErrorAlert problem={awayPlayers.problem} />
        ) : (
          <RegisterResultForm
            matchId={m.id}
            home={{ teamName: m.homeTeamName, players: homePlayers.data.data }}
            away={{ teamName: m.awayTeamName, players: awayPlayers.data.data }}
            goalsByPlayer={goalsByPlayer}
          />
        )}
      </div>
    </section>
  );
}
