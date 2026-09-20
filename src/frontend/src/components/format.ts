// Formato de números, porcentajes y fechas para la interfaz, en español de Costa Rica. Las fechas del servidor llegan en
// UTC; pasarlas a la hora local es responsabilidad de la capa de presentación (02-arquitectura-y-flujo.md).

const oneDecimal = new Intl.NumberFormat('es-CR', { maximumFractionDigits: 1 });
const integer = new Intl.NumberFormat('es-CR', { maximumFractionDigits: 0 });
const dateTime = new Intl.DateTimeFormat('es-CR', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' });

const MISSING = '—';

export function formatNumber(value: number | null | undefined): string {
  return value === null || value === undefined ? MISSING : oneDecimal.format(value);
}

export function formatInteger(value: number | null | undefined): string {
  return value === null || value === undefined ? MISSING : integer.format(value);
}

/** Un valor que ya viene en porcentaje (0–100). */
export function formatPercent(value: number | null | undefined): string {
  return value === null || value === undefined ? MISSING : `${oneDecimal.format(value)} %`;
}

/** Una fracción (0–1) mostrada como porcentaje. */
export function formatRatio(value: number | null | undefined): string {
  return value === null || value === undefined ? MISSING : `${oneDecimal.format(value * 100)} %`;
}

export function formatDate(iso: string | null | undefined): string {
  if (!iso) {
    return MISSING;
  }
  const date = new Date(iso);
  return Number.isNaN(date.getTime()) ? MISSING : dateTime.format(date);
}

/** Puesto en ordinal masculino: 1.º, 2.º... */
export function ordinal(position: number): string {
  return `${position}.º`;
}

/** Segundos como m:ss para el temporizador. */
export function formatClock(totalSeconds: number): string {
  const seconds = Math.max(0, Math.floor(totalSeconds));
  return `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, '0')}`;
}
