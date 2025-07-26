namespace MoodCode.Core.Models;

public class CommitAnalysis
{
    public string OriginalMessage { get; set; } = string.Empty;
    public string SuggestedMessage { get; set; } = string.Empty;
    public bool NeedsImprovement { get; set; }
    public List<string> ModifiedFiles { get; set; } = new();
    public string GitDiff { get; set; } = string.Empty;
}