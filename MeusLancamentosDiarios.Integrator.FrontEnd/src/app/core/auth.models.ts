export type Role = 'Cliente' | 'Admin';

export interface Usuario {
  id: string;
  nome: string;
  email: string;

  /** A unica conta do usuario. */
  contaId: string;
  role: Role;
}

/** O que todo login devolve: cadastro, senha, Google e renovacao. */
export interface Sessao {
  /** JWT de acesso, curto. Vai no header Authorization. */
  tokenAcesso: string;
  expiraEm: string;

  /** Opaco e de uso unico: troca por um par novo quando o acesso vence. */
  tokenRenovacao: string;
  usuario: Usuario;
}

export interface ConfiguracaoAuth {
  /** Vazio quando o login com Google esta desligado. */
  googleClientId: string;
}
