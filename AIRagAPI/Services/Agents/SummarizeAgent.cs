using AIRagAPI.Agents;
using Microsoft.SemanticKernel;

namespace AIRagAPI.Services.Agents;

/// <summary>
/// MAF Agent summarize result from Question and Context
/// </summary>
/// <param name="kernel"></param>
public class SummarizeAgent (Kernel kernel): IAgent
{
    public async Task<string> RunAsync(string question, List<string> context, Guid userId)
    {
        const string assistantName = "Little Phoenix";
        var prompt = $@"
        You are an AI assistant named {assistantName}. Always respond clearly and concisely.
        Do not include typos or irrelevant debugging information. Your goal is to answer the question for a human reader. You can explain a bit.

        
        Context:
        {string.Join("\n", context)}

        Question:
        {question}

        Answer:
        ";
        var result = await kernel.InvokePromptAsync(prompt);
        return result.ToString();
    }
}