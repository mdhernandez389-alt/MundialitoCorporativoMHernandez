// Contratos de la API (espejo de los DTOs de Mundialito.Application). Las páginas solo conocen estos tipos.

/** Respuesta de todo listado (§10). */
export type PagedResult<T> = {
  data: T[];
  pageNumber: number;
  pageSize: number;
  totalRecords: number;
  totalPages: number;
};

export type Team = {
  id: string;
  name: string;
  department: string;
};

export type Player = {
  id: string;
  teamId: string;
  fullName: string;
  shirtNumber: number;
};

export type MatchStatus = "Scheduled" | "Played";

export type Match = {
  id: string;
  homeTeamId: string;
  homeTeamName: string;
  awayTeamId: string;
  awayTeamName: string;
  scheduledAtUtc: string;
  status: MatchStatus;
  homeGoals: number | null;
  awayGoals: number | null;
};

export type Goal = {
  playerId: string;
  playerName: string;
  teamId: string;
};

export type MatchDetail = Match & { goals: Goal[] };

export type Standing = {
  position: number;
  teamId: string;
  teamName: string;
  department: string;
  played: number;
  won: number;
  drawn: number;
  lost: number;
  goalsFor: number;
  goalsAgainst: number;
  goalDifference: number;
  points: number;
};

export type Scorer = {
  position: number;
  playerId: string;
  playerName: string;
  shirtNumber: number;
  teamId: string;
  teamName: string;
  goals: number;
};

export type Created = { id: string };

/**
 * Error de la API (ProblemDetails + code) o de red. El frontend decide por `code`, nunca por el texto.
 * `traceId` permite buscar el request en Seq y Jaeger.
 */
export type Problem = {
  status: number;
  code: string;
  detail: string;
  traceId?: string;
};

/** El Result Pattern del backend, del lado del frontend: éxito con datos o fallo con un Problem. */
export type ApiResult<T> = { ok: true; data: T } | { ok: false; problem: Problem };
