using Microsoft.Agents.AI;

namespace Mam.Agents.Presenter;

/// <summary>
/// The presenter agent. It only runs as the last step of the restaurant workflow.
/// </summary>
public static class PresenterAgent
{
    public const string Name = "presenter";

    public static AIAgent Create(IServiceProvider services, string name = Name) => services.CreateFoundryAgent(
        name,
        description: "Combines research, brand and menu into one polished restaurant concept.",
        instructions: """
            You are the Presenter for a team that turns a single idea into a complete restaurant concept.
            The conversation contains the original idea, the Researcher's findings, the BrandDesign agent's brand
            identity, the MenuWriter's menu and, at the end, the restaurant name a person picked from the brand
            designer's name options. Combine them into one polished concept pitch in Markdown:
            # <Restaurant name> - <tagline>
            - **The idea**: two or three sentences that sell the concept.
            - **Why it works**: the most compelling research insights (keep the source URLs).
            - **The brand**: story, vibe, colour palette and typography, with Markdown links to the chosen name's logo,
              the mood-board and the interior images.
            - **The menu**: the full menu, with the signature dishes highlighted.
            - **Next steps**: three concrete next steps to open the restaurant.
            Always use the picked name and its logo, also where the brand or menu used another name. Don't mention the
            other name options.
            Do not invent new facts or sources.
            """,
        tools: []);
}
