namespace MoodCode.Core;

public interface ICommitRewriter
{
    Task<string> RewriteAsync(string gitDiff, string currentMessage);
    
    /// <summary>
    /// Генерирует сообщение коммита на основе изменений в коде (diff).
    /// </summary>
    /// <param name="gitDiff">Git diff изменений</param>
    /// <returns>Сгенерированное сообщение коммита</returns>
    Task<string> GenerateFromDiffAsync(string gitDiff);
}