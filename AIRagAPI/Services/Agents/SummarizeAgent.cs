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
        You are an expert AI assistant named {assistantName}. Your job is to answer questions accurately and concisely using ONLY the provided context.
        Do not include typos or irrelevant debugging information. Your goal is to answer the question for a HUMAN READER so they understand.

        Rules:
        You answer questions STRICTLY from the context provided below.

        STRICT RULES — no exceptions:
        • Answer STRICTLY from the context below.
        • IF NOTHING OF RELEVANCE is found in the context below, you can answer from prior knowledge but specify it was from prior knowledge.
        • Be concise but complete. Use bullet points for multi-part answers if necessary.
        
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