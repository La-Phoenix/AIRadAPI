namespace AIRagAPI.Agents;

public interface IAgent
{
    Task<string> RunAsync(string question, List<string> context, Guid userId);
}