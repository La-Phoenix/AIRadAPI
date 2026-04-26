namespace AIRagAPI.Domain.Enums;

public enum ChatType
{
    DirectMessage = 1,  // User-to-User or User-to-Assistant
    GroupChat = 2,      // Multiple users, no assistant
    RagAssistantGroup = 3  // Multiple users + assistant for document Q&A
}
