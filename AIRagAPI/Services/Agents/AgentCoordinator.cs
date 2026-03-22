using AIRagAPI.Agents;

namespace AIRagAPI.Services.Agents;

public class AgentCoordinator(List<IAgent> agents)
{
    public async Task<string> AskAsync(string question)
    {
        List<string> context = new List<string>();

        foreach (var agent in agents)
        {
            var output = await agent.RunAsync(question, context);
            context.Add(output); // Each agent builds on previous context
            
        }

        return context.Last(); //Final answer from the last agent
    }
}