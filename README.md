# Idea → Restaurant (Agent Framework + Aspire + DevUI demo)

A demo that turns a single idea into a restaurant concept with research, a brand and a menu. It's built with
[Microsoft Agent Framework](https://learn.microsoft.com/agent-framework/) (not Semantic Kernel), uses
[Aspire](https://aspire.dev) to run it, and you try out the agents in **DevUI**.

All packages come from NuGet (`Microsoft.Agents.AI.Foundry`, `Aspire.Hosting.AgentFramework.DevUI`, `Microsoft.Agents.AI.DevUI`, ...).
You don't need a clone of the Agent Framework repo.

## Agents

| Agent (DevUI name) | Project | What it does | Tools (stubbed) |
|---|---|---|---|
| `researcher` | `src/Mam.Researcher` | Researches a topic (cuisine, location, trends, competitors) and returns findings with sources | `WebSearch`, `WebFetch` |
| `menu-writer` | `src/Mam.MenuWriter` | Writes a full restaurant menu that fits the concept and its vibe | `PrintMenu` |
| `brand-designer` | `src/Mam.BrandDesign` | Turns the idea into a visual brand: name, tagline, vibe, palette, typography, logo | `GenerateImage` |

Each agent runs in its own ASP.NET Core service. It's created with `AIProjectClient.AsAIAgent(...)` and exposed
through OpenAI-compatible Responses/Conversations endpoints (`Microsoft.Agents.AI.Hosting.OpenAI`). The Aspire
AppHost connects all three services to a single **DevUI** resource.

The tools are stubs that return canned data (see `*Tools.cs` in each project), so the demo runs without any
external services besides the model. The agents aren't wired together yet; workflows come in a later step.

```
src/
  Mam.AppHost/          Aspire AppHost: existing Foundry account + project, 3 agent services, DevUI
  Mam.ServiceDefaults/  Health checks, OpenTelemetry, service discovery, AddFoundryProjectClient(...)
  Mam.Researcher/       researcher agent
  Mam.MenuWriter/       menu-writer agent
  Mam.BrandDesign/      brand-designer agent
```

## Prerequisites

- .NET 10 SDK
- Optional: the [Aspire CLI](https://aspire.dev/get-started/install-cli/), for `aspire run`. You can also use `dotnet run` or F5.
- An existing **Microsoft Foundry** account with a **project** and a `gpt-4.1` model deployment
- Azure CLI signed in (`az login`). Your user needs the **Azure AI User** role (or higher) on the Foundry project.

## Configure

Set the Foundry project endpoint in `src/Mam.AppHost/appsettings.json`. You can also use user secrets, or enter
it when the Aspire dashboard asks for it.

```json
{
  "ConnectionStrings": {
    "project": "Endpoint=https://<foundry-account>.services.ai.azure.com/api/projects/<project>"
  },
  "Parameters": {
    "modelDeployment": "gpt-4.1"
  }
}
```

The AppHost passes this to each agent as `ConnectionStrings__project`. It's a plain connection string, so Aspire
doesn't provision or deploy anything to Azure. The agents authenticate with `DefaultAzureCredential`, which uses
your `az login`.

## Run

From the repository root:

```bash
aspire run
# or
dotnet run --project src/Mam.AppHost
```

Open the **devui** URL in the Aspire dashboard, pick an agent and start chatting. The dashboard **Traces** view
shows every agent run and tool call, because each agent is wrapped with `UseOpenTelemetry()`.

### Demo prompts

- **researcher**: *"Research the market for a Korean-Mexican fusion street food restaurant in Rotterdam."*
- **brand-designer**: *"Create a brand for a cosy, late-night Korean-Mexican taqueria aimed at students."*
- **menu-writer**: *"Write a menu for 'Seoul Taco', a playful Korean-Mexican street food spot. Mid-range prices."*

## Gotcha: agent names

The agent name passed to `WithAgentService(..., agents: [new("<name>")])` in the AppHost must match
`AddAIAgent("<name>", ...)` in the service. Otherwise DevUI returns 404s. In this repo, the Aspire resource names
are the same as the agent names to keep it simple.
