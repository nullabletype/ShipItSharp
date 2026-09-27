# ShipItSharp Agent Guidance

## CLI confirmation safety

- Every command that can change Octopus or local filesystem state must give the operator an explicit confirmation or continue/cancel step before its first mutation. An interactive selection flow with clear Continue and Exit choices satisfies this rule. The automation-oriented `deploy profile`, `deploy profiledirectory`, and idempotent `env ensure` flows are the explicit exceptions.
- Skip that confirmation only when the operator explicitly supplies the command's no-prompt option. Read-only, help, and dry-run flows do not require confirmation.
- Cover the confirmation boundary with tests proving that rejection performs no mutation and that the no-prompt path performs the requested action without reading input.
