namespace AIRagAPI.Services.Vector;

public interface IVectorService
{
    public Task AddDocumentAsync(string text, Guid userId);
    public Task<List<string>> SearchAsync(string query, Guid userId, ulong top = 5);
}