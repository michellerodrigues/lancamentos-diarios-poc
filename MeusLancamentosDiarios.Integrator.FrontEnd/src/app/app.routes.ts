import { Routes } from '@angular/router';

import { exigeLogin, somenteConvidado } from './core/auth.guards';

export const routes: Routes = [
  {
    path: '',
    title: 'Saldo Consolidado',
    canActivate: [exigeLogin],
    loadComponent: () =>
      import('./saldo-consolidado/saldo-consolidado').then(
        (m) => m.SaldoConsolidadoComponent,
      ),
  },
  {
    path: 'novo-lancamento',
    title: 'Novo Lançamento',
    canActivate: [exigeLogin],
    loadComponent: () =>
      import('./novo-lancamento/novo-lancamento').then(
        (m) => m.NovoLancamentoComponent,
      ),
  },
  {
    path: 'entrar',
    title: 'Entrar',
    canActivate: [somenteConvidado],
    loadComponent: () => import('./auth/entrar/entrar').then((m) => m.EntrarComponent),
  },
  {
    path: 'cadastro',
    title: 'Criar conta',
    canActivate: [somenteConvidado],
    loadComponent: () => import('./auth/cadastro/cadastro').then((m) => m.CadastroComponent),
  },
  {
    path: 'esqueci-senha',
    title: 'Esqueci a senha',
    loadComponent: () =>
      import('./auth/esqueci-senha/esqueci-senha').then((m) => m.EsqueciSenhaComponent),
  },
  {
    // Sem guard: o link do e-mail pode ser aberto com outra sessao ativa.
    path: 'redefinir-senha',
    title: 'Redefinir senha',
    loadComponent: () =>
      import('./auth/redefinir-senha/redefinir-senha').then((m) => m.RedefinirSenhaComponent),
  },
  { path: '**', redirectTo: '' },
];
