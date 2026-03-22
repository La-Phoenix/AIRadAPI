using AIRagAPI.Agents;
using AIRagAPI.Services.Vector;

namespace AIRagAPI.Services.Agents;

/// <summary>
/// MAF agent do a hybrid ranked search and output context
/// </summary>
/// <param name="vectorService"></param>
public class RetrieverAgent(IVectorService vectorService) : IAgent
{
    public async Task<string> RunAsync(string question, List<string> context)
    {
        var docs = await vectorService.SearchAsync(question);
        return string.Join("\n", docs);
    }
}