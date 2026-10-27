// Formatos de números del catálogo (P3) con la convención colombiana (es-CO).

const miles = new Intl.NumberFormat('es-CO', { maximumFractionDigits: 0 })

/** 1500 → "1.500". */
export function formatoMiles(valor: number): string {
  return miles.format(Math.round(valor))
}

/** 120000000 → "$ 120.000.000" (espacio normal, sin decimales). */
export function formatoCop(valor: number): string {
  return `$ ${formatoMiles(valor)}`
}

/** 85000 → "85.000 km". */
export function formatoKm(valor: number): string {
  return `${formatoMiles(valor)} km`
}

/** 3500 → "3.500 kg". */
export function formatoKg(valor: number): string {
  return `${formatoMiles(valor)} kg`
}

/** 4500 → "4.500 cc". */
export function formatoCc(valor: number): string {
  return `${formatoMiles(valor)} cc`
}

/** Deja solo los dígitos de lo que escribe el usuario ("120.000" → "120000"). */
export function soloDigitos(texto: string): string {
  return texto.replace(/\D/g, '')
}

/**
 * Muestra un valor guardado como dígitos con separador de miles mientras se
 * escribe. Si no son solo dígitos (p. ej. viene así de la URL), se deja igual
 * para que la validación lo marque.
 */
export function mostrarConMiles(digitos: string): string {
  if (digitos === '' || !/^\d+$/.test(digitos)) return digitos
  const numero = Number(digitos)
  return Number.isSafeInteger(numero) ? formatoMiles(numero) : digitos
}

/** "1 vehículo encontrado" / "12 vehículos encontrados". */
export function textoTotal(total: number): string {
  return total === 1 ? '1 vehículo encontrado' : `${formatoMiles(total)} vehículos encontrados`
}

/** "Bogotá, Cundinamarca", o solo lo que haya. */
export function textoUbicacion(municipio: string | null, departamento: string | null): string {
  return [municipio, departamento].filter((v): v is string => !!v).join(', ')
}
