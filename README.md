# Event-Sourced Ledger

Motor de razão contábil com **double-entry accounting**, **event sourcing** e **idempotência** ponta a ponta — o núcleo técnico de qualquer fintech, banco ou processador de pagamentos.

> Em construção — Fase 1 (Domain Core + Event Store). Spec completo em [`docs/superpowers/specs/2026-05-28-ledger-phase-1-design.md`](https://github.com/anapaulaendler/portfolio-planning/blob/main/docs/superpowers/specs/2026-05-28-ledger-phase-1-design.md) (repo `portfolio-planning`).

## Princípios técnicos

| Princípio | Manifestação |
|-----------|--------------|
| Double-entry | Toda transação = N postings; Σ débitos = Σ créditos. Invariante checada no construtor e em property tests. |
| Event sourcing | Estado = projeção de eventos imutáveis. Sem `UPDATE`; só `INSERT` em event store. |
| Idempotência | Toda escrita exige `Idempotency-Key`; retries não duplicam. Padrão Stripe/Adyen. *(Fase 2)* |
| Auditabilidade | Replay completo a partir de eventos; toda projeção é reconstruível. |
| Reconciliação | Diff entre ledger interno e extrato externo. *(Fase 3)* |

## Stack

- **Backend:** .NET 10 + C# + ASP.NET Core Minimal API *(Fase 2)*
- **Banco:** PostgreSQL 16 + Npgsql + EF Core
- **Testes:** xUnit + FsCheck (property-based) + Testcontainers (integration)
- **Frontend:** Angular 21 *(Fase 4)*

## Como rodar localmente

```bash
# Subir Postgres + pgAdmin (a partir da Fase 2)
podman-compose up -d

# Rodar testes
dotnet test
```
