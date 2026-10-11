// La API trabaja en UTC; se muestra en la hora de El Salvador, explícita para que el servidor de Next
// (que puede estar en otra zona, p. ej. el contenedor en UTC) siempre muestre lo mismo.
const TIME_ZONE = "America/El_Salvador";

const dateTime = new Intl.DateTimeFormat("es-SV", {
  dateStyle: "medium",
  timeStyle: "short",
  timeZone: TIME_ZONE,
});

export const formatDateTime = (isoUtc: string) => dateTime.format(new Date(isoUtc));

export const formatGoalDifference = (value: number) => (value > 0 ? `+${value}` : String(value));

export const statusLabel = (status: string) => (status === "Played" ? "Jugado" : "Programado");

/** Una fecha del filtro (YYYY-MM-DD) como inicio o fin de ese día en El Salvador (UTC−6), con zona horaria. */
export const startOfDay = (date?: string) => (date ? `${date}T00:00:00-06:00` : undefined);
export const endOfDay = (date?: string) => (date ? `${date}T23:59:59-06:00` : undefined);
