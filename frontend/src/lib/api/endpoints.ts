import "server-only";

import { apiGet, apiSend, type Query } from "./client";
import type { Created, Match, MatchDetail, PagedResult, Player, Scorer, Standing, Team } from "./types";

// Un endpoint de la API = una función tipada. Si la API cambia una ruta, se toca solo este archivo.
// Los Ids se codifican: nunca se pega texto crudo en una URL.
const id = (value: string) => encodeURIComponent(value);

export const getStandings = (query: Query) => apiGet<PagedResult<Standing>>("/api/standings", query);

export const getScorers = (query: Query) => apiGet<PagedResult<Scorer>>("/api/scorers", query);

export const getTeams = (query: Query) => apiGet<PagedResult<Team>>("/api/teams", query);

export const getTeam = (teamId: string) => apiGet<Team>(`/api/teams/${id(teamId)}`);

export const getTeamPlayers = (teamId: string, query: Query) =>
  apiGet<PagedResult<Player>>(`/api/teams/${id(teamId)}/players`, query);

export const getMatches = (query: Query) => apiGet<PagedResult<Match>>("/api/matches", query);

export const getMatch = (matchId: string) => apiGet<MatchDetail>(`/api/matches/${id(matchId)}`);

/** POST idempotente (§6): la misma key devuelve el mismo equipo aunque el formulario se envíe dos veces. */
export const createTeam = (input: { name: string; department: string }, idempotencyKey: string) =>
  apiSend<Created>("POST", "/api/teams", input, { "Idempotency-Key": idempotencyKey });

/** PUT: reemplaza el resultado completo (idempotente por diseño, no necesita key). */
export const registerMatchResult = (matchId: string, scorerPlayerIds: string[]) =>
  apiSend<void>("PUT", `/api/matches/${id(matchId)}/result`, { scorerPlayerIds });
