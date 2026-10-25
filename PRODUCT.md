# Product

<!-- impeccable:product-schema 1 -->

> Written by `/impeccable init` without an interview, because the user wasn't available. Facts marked *(inferred)* are
> guesses from the repository and the demo work; confirm or correct them.

## Platform

web

## Users

- **The presenter** (the repo owner): runs the demos live, from a laptop on a projector. They narrate while the agents
  work and click through DevUI, the workflow board, the harness console and the Aspire dashboard.
- **The audience**: watches from a distance, sees each screen for seconds and never touches it. *(inferred: developers
  interested in .NET, AI agents and Aspire)*
- **Developers after the talk**: clone the repo and run it themselves, using the README as their guide.

## Product Purpose

A demo app for live talks. It turns one idea ("a Surinamese roti shop on a Rotterdam rooftop") into a complete
restaurant concept (research, brand, name, logos, menu and a pitch) to show what Microsoft Agent Framework (.NET), Aspire and DevUI can do.
Success means the audience can follow which agent is working, sees real output appear (text and generated images) and
remembers one interactive moment: picking the restaurant name.

## Positioning

The same three agents are orchestrated two ways and shown side by side: a deterministic **workflow** (graph, fan-out and
fan-in, human-in-the-loop) and an autonomous **harness** agent that plans and delegates. Everything runs under one Aspire
AppHost, so each run is also visible as a single trace.

## Operating Context

- Live stage: projector, a room at a distance, often low light, limited time (a workflow run takes about 2–3 minutes). *(inferred)*
- The presenter talks over the screen, so the UI must read at a glance and must never need a long explanation.
- Surfaces:
  - **DevUI** (third-party, not styled here) for chatting with agents.
  - **The workflow board** at `/board/` in `Mam.Workflow`: a kitchen-pass metaphor with tickets on a rail.
  - **The harness console** (terminal UI).
  - **The Aspire dashboard** (third-party).

## Capabilities and Constraints

- Agents: `researcher`, `brand-designer`, `menu-writer`, `presenter`. Workflow steps: intake → researcher → brief → (brand-designer ∥
  menu-writer) → gather → shortlist ⇄ pick-name (human) → presenter.
- The board is static HTML, CSS and JS served by the ASP.NET workflow service. It has no build step and no framework.
  The only vendored library is `marked`. Live updates arrive over SSE.
- Runs are kept in memory only, so a restart loses them. Images come from `gpt-image-2.5-flare` and take about 20 s.
- The name pick works on the board (click a card or press 1–3) and in DevUI (reply with a number or a name). DevUI can't
  show workflow approvals in this framework version.
- Terminology: "workflow board", "the pass", "tickets", "Fire the order", "New order", "pick-name".

## Brand Commitments

- Project name "mam". The board uses a restaurant-kitchen voice (tickets, the pass, firing an order). Keep it consistent.
- No other brand assets are binding. *(inferred)*

## Evidence on Hand

- Real generated output from runs: restaurant names, logos, mood boards, interiors and menus (saved to `%TEMP%\mam-images` while running).
- README sections with a suggested demo script.
- There are no testimonials, metrics or customer claims. Don't invent any.

## Product Principles

1. **Readable from the back of the room.** Big type, few words and clear state beat density.
2. **Show the work happening.** Make progress, parallelism and waiting visible; never leave a blank screen.
3. **One moment of participation.** The human pick is the climax; make it obvious and easy.
4. **The tech is the subject.** UI should make Agent Framework concepts (executors, edges, fan-out, request ports) legible, not hide them.
5. **Runs offline from the talk.** Anything shown must also work for someone cloning the repo, without extra services.

## Accessibility & Inclusion

Projector conditions: high contrast, large text and colour that isn't the only signal. Keys (1–3) must be optional
shortcuts, not the only way to do something. *(inferred)*
