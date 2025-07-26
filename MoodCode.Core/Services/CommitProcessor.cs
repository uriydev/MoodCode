using MoodCode.Core.Models;

namespace MoodCode.Core.Services;

/// <summary>
/// Процессор для анализа и улучшения сообщений коммитов.
/// Использует GitAnalyzer для анализа изменений и ICommitRewriter для генерации улучшенных сообщений.
/// </summary>
public class CommitProcessor
{
    private readonly GitAnalyzer _gitAnalyzer;
    private readonly ICommitRewriter _commitRewriter;

    /// <summary>
    /// Создает новый экземпляр CommitProcessor.
    /// </summary>
    /// <param name="gitAnalyzer">Анализатор Git для работы с репозиторием.</param>
    /// <param name="commitRewriter">Сервис для переписывания сообщений коммитов.</param>
    public CommitProcessor(GitAnalyzer gitAnalyzer, ICommitRewriter commitRewriter)
    {
        _gitAnalyzer = gitAnalyzer ?? throw new ArgumentNullException(nameof(gitAnalyzer));
        _commitRewriter = commitRewriter ?? throw new ArgumentNullException(nameof(commitRewriter));
    }

    /// <summary>
    /// Анализирует сообщение коммита и предлагает улучшения на основе изменений в репозитории.
    /// </summary>
    /// <param name="commitMessage">Исходное сообщение коммита.</param>
    /// <param name="repositoryPath">Путь к репозиторию Git. Если null, будет использован текущий репозиторий.</param>
    /// <returns>Результат анализа, содержащий оригинальное и улучшенное сообщения.</returns>
    /// <exception cref="InvalidOperationException">Если нет подготовленных изменений в репозитории.</exception>
    public async Task<CommitAnalysis> AnalyzeCommitAsync(string commitMessage, string? repositoryPath = null)
    {
        if (string.IsNullOrEmpty(commitMessage))
            throw new ArgumentNullException(nameof(commitMessage));

        repositoryPath ??= _gitAnalyzer.GetCurrentRepositoryPath();

        if (!_gitAnalyzer.HasStagedChanges(repositoryPath))
        {
            throw new InvalidOperationException("No staged changes found. Please stage your changes first.");
        }

        var analysis = new CommitAnalysis
        {
            OriginalMessage = commitMessage,
            NeedsImprovement = _gitAnalyzer.IsBadCommitMessage(commitMessage),
            ModifiedFiles = _gitAnalyzer.GetModifiedFiles(repositoryPath),
            GitDiff = _gitAnalyzer.GetStagedDiff(repositoryPath)
        };

        if (analysis.NeedsImprovement)
        {
            Console.WriteLine("🤖 Generating improved commit message...");
            analysis.SuggestedMessage = await _commitRewriter.RewriteAsync(
                analysis.GitDiff, 
                analysis.OriginalMessage
            );
        }
        else
        {
            analysis.SuggestedMessage = analysis.OriginalMessage;
        }

        return analysis;
    }

    public void DisplayAnalysis(CommitAnalysis analysis)
    {
        Console.WriteLine();
        WriteColor("=== MoodCode Analysis ===", ConsoleColor.Cyan);
        
        if (analysis.NeedsImprovement)
        {
            WriteColor($"❌ Оригинал: \"{analysis.OriginalMessage}\"", ConsoleColor.Red);
            WriteColor($"✅ Предложение: \"{analysis.SuggestedMessage}\"", ConsoleColor.Green);
            Console.WriteLine();
            WriteColor($"📁 Измененные файлы: {string.Join(", ", analysis.ModifiedFiles)}", ConsoleColor.Yellow);
        }
        else
        {
            WriteColor($"✅ Хорошее сообщение коммита: \"{analysis.OriginalMessage}\"", ConsoleColor.Green);
            WriteColor("Нет необходимости в изменениях!", ConsoleColor.Green);
        }
        
        Console.WriteLine();
    }

    public bool PromptUserForApproval()
    {
        WriteColor("Принять это предложение? [Д/н/р(едактировать)]: ", ConsoleColor.Yellow, inline: true);
        var input = Console.ReadLine()?.ToLowerInvariant().Trim();
        
        return input is "" or "y" or "yes" or "д" or "да";
    }

    public string PromptUserForEdit(string currentMessage)
    {
        WriteColor($"Текущее предложение: {currentMessage}", ConsoleColor.Yellow);
        WriteColor("Введите предпочитаемое сообщение: ", ConsoleColor.Yellow, inline: true);
        var userInput = Console.ReadLine()?.Trim();
        
        return !string.IsNullOrEmpty(userInput) ? userInput : currentMessage;
    }

    private void WriteColor(string message, ConsoleColor color, bool inline = false)
    {
        Console.ForegroundColor = color;
        if (inline)
        {
            Console.Write(message);
        }
        else
        {
            Console.WriteLine(message);
        }
        Console.ResetColor();
    }
}
