using MoodCode.Core;
using MoodCode.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net.Http;
using System.IO;
using System.Reflection;

namespace MoodCode.CLI;

class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            var host = CreateHostBuilder(args).Build();
            var processor = host.Services.GetRequiredService<CommitProcessor>();

            if (args.Length == 0)
            {
                Console.WriteLine("Usage: moodcode-hook <commit-message>");
                Console.WriteLine("This tool is typically called by git hooks.");
                return;
            }

            var commitMessage = string.Join(" ", args);
            var analysis = await processor.AnalyzeCommitAsync(commitMessage);
            
            processor.DisplayAnalysis(analysis);

            if (analysis.NeedsImprovement)
            {
                if (processor.PromptUserForApproval())
                {
                    // Output the suggested message for git hook to use
                    Console.WriteLine($"APPROVED_MESSAGE:{analysis.SuggestedMessage}");
                }
                else
                {
                    // User can edit or reject
                    var editedMessage = processor.PromptUserForEdit(analysis.SuggestedMessage);
                    Console.WriteLine($"APPROVED_MESSAGE:{editedMessage}");
                }
            }
            else
            {
                // Keep original message
                Console.WriteLine($"APPROVED_MESSAGE:{analysis.OriginalMessage}");
            }
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"MoodCode Error: {ex.Message}");
            LogException(ex);
            Environment.Exit(1);
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"MoodCode Error: Failed to communicate with AI service. Please check your network connection and API key. Details: {ex.Message}");
            LogException(ex);
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An unexpected error occurred: {ex.Message}");
            LogException(ex);
            Environment.Exit(1);
        }
    }

    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton<GitAnalyzer>();
                services.AddSingleton<ICommitRewriter, GroqCommitRewriter>();
                services.AddSingleton<CommitProcessor>();
            });

    private static void LogException(Exception ex)
    {
        var logDirectory = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory, "Logs");
        if (!Directory.Exists(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
        }

        var logFilePath = Path.Combine(logDirectory, $"error_{DateTime.Now:yyyyMMddHHmmss}.log");
        var errorMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Error: {ex.Message}\nStackTrace: {ex.StackTrace}\n";
        if (ex.InnerException != null)
        {
            errorMessage += $"Inner Exception: {ex.InnerException.Message}\nInner StackTrace: {ex.InnerException.StackTrace}\n";
        }
        File.AppendAllText(logFilePath, errorMessage);
        Console.WriteLine($"A detailed error log has been saved to: {logFilePath}");
    }
}