using AIRagAPI.Agents;

namespace AIRagAPI.Services.Agents;

public class AgentCoordinator
{
    private readonly List<IAgent> _agents;
    public AgentCoordinator(IEnumerable<IAgent> agents)
    {
        _agents = agents.ToList();
    }
    // Runs Retrieval and Summarize agent. Uses Previous response to feed the later
    public async Task<string> AskAsync(string question, Guid userId)
    {
        List<string> context = new List<string>();

        foreach (var agent in _agents)
        {
            var output = await agent.RunAsync(question, context, userId);
            context.Add(output); // Each agent builds on previous context
            
        }

        return context.Last(); //Final answer from the last agent
    }
}