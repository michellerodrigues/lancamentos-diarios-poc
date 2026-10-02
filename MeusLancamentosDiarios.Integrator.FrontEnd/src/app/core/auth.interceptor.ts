import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, from, switchMap, throwError } from 'rxjs';

import { API_BASE_URL } from './api.config';
import { AuthService } from './auth.service';

/**
 * Poe o Bearer em toda chamada ao BFF. Num 401, renova a sessao uma vez e repete o
 * pedido; se a renovacao for recusada, a sessao acabou e o usuario volta ao login.
 *
 * As rotas de autenticacao passam sem token e sem renovacao: e por elas que o
 * token se consegue, e renovar dentro de /auth/renovar giraria em circulo.
 */
export const autenticacaoInterceptor: HttpInterceptorFn = (pedido, proximo) => {
  if (!pedido.url.startsWith(API_BASE_URL) || pedido.url.startsWith(`${API_BASE_URL}/auth/`)) {
    return proximo(pedido);
  }

  const auth = inject(AuthService);
  const token = auth.tokenAcesso();

  return proximo(comToken(pedido, token)).pipe(
    catchError((erro: unknown) => {
      if (!(erro instanceof HttpErrorResponse) || erro.status !== 401 || !token) {
        return throwError(() => erro);
      }

      return from(auth.renovar()).pipe(
        switchMap((novo) => {
          if (!novo) {
            void auth.expirou();
            return throwError(() => erro);
          }

          return proximo(comToken(pedido, novo));
        }),
      );
    }),
  );
};

function comToken(pedido: HttpRequest<unknown>, token: string | null): HttpRequest<unknown> {
  return token ? pedido.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : pedido;
}
