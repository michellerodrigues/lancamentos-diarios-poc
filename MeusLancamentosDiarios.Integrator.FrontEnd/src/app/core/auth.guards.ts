import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from './auth.service';

/** Telas da conta: sem sessao, vai para o login. */
export const exigeLogin: CanActivateFn = () =>
  inject(AuthService).autenticado() || inject(Router).createUrlTree(['/entrar']);

/** Login e cadastro: quem ja entrou vai direto para o saldo. */
export const somenteConvidado: CanActivateFn = () =>
  !inject(AuthService).autenticado() || inject(Router).createUrlTree(['/']);
