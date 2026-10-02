/**
 * Mascara de digitacao de valor. O usuario digita so numeros e o texto cresce
 * da direita para a esquerda, como num terminal de caixa: 1 -> 0,01 -> 0,12 -> 1,23.
 */
export function formatarEntradaMoeda(bruto: string): string {
  let digitos = (bruto ?? '').replace(/\D/g, '');
  if (!digitos) return '';

  digitos = digitos.replace(/^0+(?=\d)/, '');
  while (digitos.length < 3) digitos = '0' + digitos;

  const centavos = digitos.slice(-2);
  const inteiros = digitos.slice(0, -2).replace(/^0+(?=\d)/, '');
  const comMilhar = inteiros.replace(/\B(?=(\d{3})+(?!\d))/g, '.');

  return `${comMilhar},${centavos}`;
}

/** "1.234,56" -> 1234.56 */
export function paraNumero(texto: string): number {
  if (!texto) return 0;

  const normalizado = texto.replace(/\./g, '').replace(',', '.');
  const valor = Number.parseFloat(normalizado);

  return Number.isFinite(valor) ? valor : 0;
}

/** Data de hoje em yyyy-MM-dd, no fuso local — o formato que o input[type=date] usa. */
export function hojeIso(): string {
  const agora = new Date();
  const mes = String(agora.getMonth() + 1).padStart(2, '0');
  const dia = String(agora.getDate()).padStart(2, '0');

  return `${agora.getFullYear()}-${mes}-${dia}`;
}
