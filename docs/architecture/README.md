# ShipItSharp Architecture Diagrams

This directory contains the LikeC4 source for ShipItSharp architecture diagrams.

The source is split by architecture boundary:

```text
docs/architecture/
  specs.c4
  landscape.c4
  relationships.c4
  views.c4
  externals/
  containers/
```

LikeC4 merges all `.c4` files in this project. Cross-file references use full FQNs such as
`shipitsharp.octopusAdapter.releaseRepository`.

The diagram set is organized as a C4-style drill-down:

1. `C1 - System context` shows ShipItSharp as a system and its external actors/integrations.
2. `C2 - ShipItSharp containers` expands the system into the CLI host and supporting .NET libraries.
3. `C3 - ... components` views expand individual containers into their main code-level components.

The `Cross-cutting - ...` and `Workflow - ...` views are supporting diagrams for runtime dependencies,
external integrations, deployment flow, and environment administration flow.

Build the static review site from the repository root:

```bash
likec4 build docs/architecture -o docs/architecture/site --title "ShipItSharp Architecture" --use-hash-history
```

For a local live preview instead:

```bash
likec4 start docs/architecture
```
