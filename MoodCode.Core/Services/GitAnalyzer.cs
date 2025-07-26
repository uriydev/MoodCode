using LibGit2Sharp;

namespace MoodCode.Core.Services;

public class GitAnalyzer
{
    public string GetStagedDiff(string repositoryPath)
    {
        using var repo = new Repository(repositoryPath);
        
        // Get diff between index (staged) and HEAD
        var patch = repo.Diff.Compare<Patch>(repo.Head.Tip?.Tree, DiffTargets.Index);
        
        return patch?.Content ?? string.Empty;
    }

    public bool IsBadCommitMessage(string message)
    {
        var normalizedMessage = message.ToLowerInvariant().Trim();

        // Rule 1: Message is too short or empty
        if (string.IsNullOrWhiteSpace(normalizedMessage) || normalizedMessage.Length < 3)
            return true;

        // Rule 2: Message exactly matches a common lazy pattern
        var exactBadPatterns = new[]
        {
            "fix", "test", "update", "change", "stuff", "things", "work", "temp",
            "wip", "asdf", "qwerty", "shit", "fuck", "damn", "crap", "whatever",
            "idk", "meh", "ok", "done", "finished", "final", "last", "end",
            "refactor", "chore", "docs", "feat", "style", "perf", "ci", "build", "revert",
            "bug", "patch"
        };
        if (exactBadPatterns.Contains(normalizedMessage))
            return true;

        // Rule 3: Message starts with a conventional commit type but lacks description
        // e.g., "fix:", "feat:", "docs:"
        var conventionalCommitTypes = new[]
        {
            "fix:", "feat:", "build:", "chore:", "ci:", "docs:", "perf:", "refactor:", "revert:", "style:", "test:"
        };
        if (conventionalCommitTypes.Any(type => normalizedMessage == type.ToLowerInvariant()))
            return true;

        // Rule 4: Message starts with a bad pattern and is relatively short (e.g., "fix bug")
        if (normalizedMessage.Length < 15 && exactBadPatterns.Any(pattern =>
            normalizedMessage.StartsWith(pattern + " ") || normalizedMessage == pattern))
            return true;
        
        // Rule 5: Message contains repetitive or placeholder words
        if (ContainsRepetitiveWords(normalizedMessage))
            return true;

        return false;
    }

    private bool ContainsRepetitiveWords(string message)
    {
        var words = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 2) return false;

        var lastWord = words[^1]; // Get the last word
        var secondLastWord = words.Length > 1 ? words[^2] : string.Empty;

        // Check for common repetitive patterns like "fix fix" or "update update"
        return words.Any(word => (word.Length > 2 && message.Split(word).Length - 1 > 1) && !IsCommonNonRepetitiveWord(word));
    }

    private bool IsCommonNonRepetitiveWord(string word)
    {
        // Add common words that might appear multiple times but are not necessarily repetitive
        var commonWords = new HashSet<string> { "and", "or", "the", "a", "an", "to", "for", "in", "on", "with", "of" };
        return commonWords.Contains(word);
    }

    public string GetCurrentRepositoryPath()
    {
        var currentDir = Directory.GetCurrentDirectory();
        
        while (currentDir != null)
        {
            if (Directory.Exists(Path.Combine(currentDir, ".git")))
                return currentDir;
                
            currentDir = Directory.GetParent(currentDir)?.FullName;
        }
        
        throw new InvalidOperationException("Not in a git repository");
    }

    public bool HasStagedChanges(string repositoryPath)
    {
        using var repo = new Repository(repositoryPath);
        return repo.RetrieveStatus().Staged.Any();
    }

    public List<string> GetModifiedFiles(string repositoryPath)
    {
        using var repo = new Repository(repositoryPath);
        var status = repo.RetrieveStatus();
        
        return status.Staged
            .Select(item => item.FilePath)
            .ToList();
    }
}