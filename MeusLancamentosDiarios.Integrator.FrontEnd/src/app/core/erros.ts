import { HttpErrorResponse } from '@angular/common/http';

/** Erros no formato ProblemDetails, como a WebApi devolve e o BFF repassa. */
interface Problema {
  title?: string;
  errors?: Record<string, string[]>;
}

/**
 * Transforma a falha de uma chamada em mensagens para a tela. Validacao traz a
 * lista do FluentValidation; recusas (401, 409) trazem so o titulo.
 */
export function mensagensDeErro(erro: unknown, padrao: string): string[] {
  if (!(erro instanceof HttpErrorResponse)) return [padrao];

  if (erro.status === 0) return ['Sem conexão com o servidor. Tente novamente.'];

  if (erro.status === 403) return ['Você não tem acesso a esta conta.'];

  const problema = erro.error as Problema | null;
  const mensagens = Object.values(problema?.errors ?? {}).flat();

  if (mensagens.length > 0) return mensagens;

  return problema?.title ? [problema.title] : [padrao];
}
