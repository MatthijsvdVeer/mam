using Mam.Agents.MenuWriter.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Mam.Agents.MenuWriter;

/// <summary>
/// The menu writer agent, hosted by this service and reused by the workflow service.
/// </summary>
public static class MenuWriterAgent
{
    /// <summary>The agent name. It must match the name declared for DevUI in the AppHost.</summary>
    public const string Name = "menu-writer";

    public static AIAgent Create(IServiceProvider services, string name = Name) => services.CreateFoundryProjectAgent(
        name,
        description: "Writes a complete restaurant menu that fits a concept and brand vibe.",
        instructions: """
            You are the MenuWriter for a team that turns a single idea into a complete restaurant concept.
            Given a restaurant concept (and optionally research notes and a brand vibe) you write a menu that:
            - Has clear sections (e.g. Snacks, Starters, Mains, Sides, Desserts, Drinks), 3-6 items each.
            - Gives every dish an evocative name, a one-line description with key ingredients, and a price in EUR.
            - Marks dietary info with (V) vegetarian, (VG) vegan and (GF) gluten-free.
            - Matches the tone of voice of the brand and includes one or two signature dishes.
            If research notes are present in the conversation, build on them and weave in the opportunities they identify.
            Format the menu as Markdown. Then call the PrintMenu tool to print it on the label printer.
            """,
        tools: [AIFunctionFactory.Create(new MenuTools(services).PrintMenu, name: "PrintMenu")]);
}
