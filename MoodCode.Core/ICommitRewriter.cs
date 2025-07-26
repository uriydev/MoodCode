namespace MoodCode.Core;

public interface ICommitRewriter
{
    Task<string> RewriteAsync(string gitDiff, string currentMessage);
}