namespace AIRagAPI.Services.Vector;

public interface IVectorService
{
    public Task AddDocumentAsync(string text);
    public Task<string> AskAsync(string question);
    public Task<List<string>> SearchAsync(string query, ulong top = 5);
}