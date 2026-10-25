# Vendored Harness sample console

This folder is a copy of the terminal UI from the Agent Framework Harness samples, taken from tag
[`dotnet-1.25.0`](https://github.com/microsoft/agent-framework/tree/dotnet-1.25.0/dotnet/samples/02-agents/Harness)
(MIT licensed, copyright Microsoft):

- `ConsoleReactiveFramework/` and `ConsoleReactiveComponents/`: a small reactive terminal UI framework.
- `Harness_Shared_Console/`: `HarnessConsole.RunAgentAsync(...)`, the observers, commands and tool formatters.
  `HarnessTracing.cs` and `FileSpanExporter.cs` were left out; this repo sends telemetry to Aspire instead.

These are samples, not a NuGet package, so they're copied unchanged. To update, copy the same folders from the
tag that matches the `Microsoft.Agents.AI.Harness` version in `Directory.Packages.props`.
