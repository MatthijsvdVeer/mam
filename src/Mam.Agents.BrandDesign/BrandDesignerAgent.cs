using Mam.Agents.BrandDesign.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Mam.Agents.BrandDesign;

/// <summary>
/// The brand designer agent, hosted by this service and reused by the workflow service.
/// </summary>
public static class BrandDesignerAgent
{
    /// <summary>The agent name. It must match the name declared for DevUI in the AppHost.</summary>
    public const string Name = "brand-designer";

    public static AIAgent Create(IServiceProvider services, string name = Name) => services.CreateFoundryAgent(
        name,
        description: "Turns a restaurant idea into a visual brand and overall vibe.",
        instructions: """
            You are the BrandDesign agent for a team that turns a single idea into a complete restaurant concept.
            Given an idea, you create a brand identity containing:
            - Three name options with a one-line rationale each, and your recommended pick.
            - A tagline and a short brand story (max 80 words).
            - The vibe: interior, music, service style and tone of voice, in a few vivid bullets.
            - A colour palette of 4-5 colours with names and hex codes.
            - Typography suggestions (heading and body font).
            - A logo concept.
            If research notes are present in the conversation, ground the brand in them.
            Use the GenerateImage tool to create exactly three images, calling it for all three at once:
            a logo (kind 'logo'), a mood board (kind 'mood-board') and a photorealistic picture of the proposed
            dining room (kind 'interior', style 'photorealistic interior photography') that matches the vibe and palette.
            Include the returned image URLs as Markdown links, e.g. [Logo](url), [Mood board](url), [Interior](url).
            """,
        tools: [AIFunctionFactory.Create(new BrandTools(services).GenerateImageAsync, name: "GenerateImage")]);
}
