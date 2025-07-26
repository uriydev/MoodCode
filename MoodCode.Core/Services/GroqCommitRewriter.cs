using MoodCode.Core.Models;
using Newtonsoft.Json;
using RestSharp;
using System.Text.RegularExpressions;

namespace MoodCode.Core.Services;

public class GroqCommitRewriter : ICommitRewriter
{
    private readonly RestClient _client;
    private readonly string _apiKey;

    public GroqCommitRewriter()
    {
        _apiKey = Environment.GetEnvironmentVariable("GROQ_API_KEY") 
                 ?? throw new InvalidOperationException("GROQ_API_KEY environment variable not set");
        
        _client = new RestClient("https://api.groq.com/openai/v1/");
        _client.AddDefaultHeader("Authorization", $"Bearer {_apiKey}");
        _client.AddDefaultHeader("Content-Type", "application/json");
    }

    public async Task<string> RewriteAsync(string gitDiff, string currentMessage)
    {
        try
        {
            var prompt = BuildPrompt(gitDiff, currentMessage);
            var request = CreateRequest(prompt);
            
            var response = await _client.ExecuteAsync(request);
            
            if (!response.IsSuccessful)
            {
                throw new Exception($"Groq API error: {response.ErrorMessage}");
            }

            var groqResponse = JsonConvert.DeserializeObject<GroqResponse>(response.Content!);
            var rawResponse = groqResponse?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();
            
            Console.WriteLine($"[DEBUG] Raw API response: \"{rawResponse}\"");
            
            // Очищаем ответ от пояснительных текстов
            var rewrittenMessage = CleanResponseText(rawResponse, currentMessage);
            
            Console.WriteLine($"[DEBUG] Cleaned response: \"{rewrittenMessage}\"");

            return !string.IsNullOrEmpty(rewrittenMessage) ? rewrittenMessage : currentMessage;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error rewriting commit: {ex.Message}");
            return currentMessage; // Fallback to original message
        }
    }
    
    public async Task<string> GenerateFromDiffAsync(string gitDiff)
    {
        try
        {
            var prompt = BuildDiffBasedPrompt(gitDiff);
            var request = CreateRequest(prompt);
            
            var response = await _client.ExecuteAsync(request);
            
            if (!response.IsSuccessful)
            {
                throw new Exception($"Groq API error: {response.ErrorMessage}");
            }

            var groqResponse = JsonConvert.DeserializeObject<GroqResponse>(response.Content!);
            var rawResponse = groqResponse?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();
            
            Console.WriteLine($"[DEBUG] Raw API response: \"{rawResponse}\"");
            
            // Очищаем ответ от пояснительных текстов
            var generatedMessage = CleanResponseText(rawResponse, "Update code");
            
            Console.WriteLine($"[DEBUG] Cleaned response: \"{generatedMessage}\"");

            return !string.IsNullOrEmpty(generatedMessage) ? generatedMessage : "Update code";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error generating commit message from diff: {ex.Message}");
            return "Update code"; // Fallback message
        }
    }

    /// <summary>
    /// Очищает ответ API от пояснительных текстов и возвращает только сообщение коммита
    /// </summary>
    private string CleanResponseText(string? rawResponse, string fallbackMessage)
    {
        if (string.IsNullOrEmpty(rawResponse))
            return fallbackMessage;

        Console.WriteLine($"[DEBUG] Cleaning response: \"{rawResponse}\"");

        // Извлекаем сообщение из блока кода markdown, если оно есть
        var codeBlockMatch = Regex.Match(rawResponse, @"```(?:bash|sh)?\s*\n(.*?)\n```", RegexOptions.Singleline);
        if (codeBlockMatch.Success)
        {
            var extractedMessage = codeBlockMatch.Groups[1].Value.Trim();
            Console.WriteLine($"[DEBUG] Extracted message from code block: \"{extractedMessage}\"");
            if (!string.IsNullOrWhiteSpace(extractedMessage))
            {
                return extractedMessage;
            }
        }

        // Если ответ содержит несколько строк, разделим их
        var lines = rawResponse.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length > 1)
        {
            Console.WriteLine($"[DEBUG] Response contains {lines.Length} lines");
            
            // Ищем строку, которая выглядит как сообщение коммита
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                // Игнорируем строки с маркерами markdown и пояснительным текстом
                if (!string.IsNullOrEmpty(trimmedLine) && 
                    !trimmedLine.StartsWith("```") && 
                    !trimmedLine.StartsWith("Here's") && 
                    !trimmedLine.StartsWith("Based on") &&
                    !trimmedLine.StartsWith("Following") &&
                    !trimmedLine.StartsWith("This rewritten") &&
                    !trimmedLine.Contains("conventional commits format"))
                {
                    // Проверяем, похоже ли это на сообщение коммита
                    if (IsLikelyCommitMessage(trimmedLine))
                    {
                        Console.WriteLine($"[DEBUG] Found valid commit message line: \"{trimmedLine}\"");
                        return trimmedLine;
                    }
                }
            }
        }

        // Удаляем распространенные пояснительные префиксы
        var prefixesToRemove = new[]
        {
            "Here's a rewritten version of the commit message( following the provided guidelines)?:",
            "Here is the commit message:",
            "Here's a commit message:",
            "Improved commit message:",
            "Generated commit message:",
            "Rewritten commit message:",
            "Suggested commit message:",
            "Based on the provided rules and examples, I will rewrite the commit message to be clear and professional.",
            "Based on the provided (rules|guidelines) and examples,",
            "Following the provided (rules|guidelines),",
            "I will rewrite the commit message to be",
            "The improved commit message is:",
            "The rewritten commit message is:"
        };

        string cleanedResponse = rawResponse;
        
        foreach (var prefix in prefixesToRemove)
        {
            var before = cleanedResponse;
            cleanedResponse = Regex.Replace(cleanedResponse, $"^{prefix}", "", RegexOptions.IgnoreCase).Trim();
            if (before != cleanedResponse)
            {
                Console.WriteLine($"[DEBUG] Removed prefix matching pattern: \"{prefix}\"");
            }
        }

        // Если ответ содержит полное пояснение и не соответствует формату коммита, 
        // попробуем извлечь сообщение коммита из текста
        if (string.IsNullOrWhiteSpace(cleanedResponse) || (!cleanedResponse.Contains(":") && cleanedResponse.Length > 20))
        {
            // Попытка найти сообщение коммита в формате "feat: message" в тексте
            var match = Regex.Match(rawResponse, @"(feat|fix|docs|style|refactor|perf|test|build|ci|chore|revert)(\([a-z-]+\))?:\s+[^\n]+", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                Console.WriteLine($"[DEBUG] Extracted commit message from text: \"{match.Value}\"");
                cleanedResponse = match.Value.Trim();
            }
            else if (string.IsNullOrWhiteSpace(cleanedResponse))
            {
                // Если после очистки ничего не осталось, создаем сообщение на основе оригинального
                Console.WriteLine($"[DEBUG] Empty response after cleaning, creating a default commit message");
                
                // Используем оригинальное сообщение для определения типа коммита
                string commitType = "fix";
                if (fallbackMessage.Contains("feat") || fallbackMessage.Contains("add") || fallbackMessage.Contains("new"))
                {
                    commitType = "feat";
                }
                else if (fallbackMessage.Contains("doc") || fallbackMessage.Contains("readme"))
                {
                    commitType = "docs";
                }
                
                // Создаем более конкретное сообщение на основе оригинального
                if (fallbackMessage.ToLower() == "fix stuff")
                {
                    cleanedResponse = "fix: resolve code issues and improve functionality";
                }
                else
                {
                    cleanedResponse = $"{commitType}: {fallbackMessage}";
                }
            }
        }

        // Удаляем кавычки, если сообщение в них обернуто
        var beforeQuotes = cleanedResponse;
        cleanedResponse = Regex.Replace(cleanedResponse, "^[\"'](.+)[\"']$", "$1").Trim();
        if (beforeQuotes != cleanedResponse)
        {
            Console.WriteLine($"[DEBUG] Removed surrounding quotes");
        }
        
        return cleanedResponse;
    }

    /// <summary>
    /// Проверяет, похожа ли строка на сообщение коммита
    /// </summary>
    private bool IsLikelyCommitMessage(string line)
    {
        // Проверяем на соответствие формату conventional commits
        if (Regex.IsMatch(line, @"^(feat|fix|docs|style|refactor|perf|test|build|ci|chore|revert)(\([a-z-]+\))?:", RegexOptions.IgnoreCase))
        {
            return true;
        }
        
        // Проверяем другие признаки сообщения коммита
        if (line.Length < 72 && // Хорошие сообщения коммитов обычно короче 72 символов
            !line.EndsWith(".") && // Обычно не заканчиваются точкой
            line.Split(' ').Length < 10 && // Не слишком много слов
            char.IsUpper(line[0])) // Начинаются с заглавной буквы
        {
            return true;
        }
        
        return false;
    }

    private string BuildPrompt(string gitDiff, string currentMessage)
    {
        // Определяем тип изменений на основе diff
        string changeType = DetermineChangeType(gitDiff);
        string fileContext = ExtractFileContext(gitDiff);

        return $@"Rewrite this git commit message to be clear and professional:

Current message: ""{currentMessage}""

Change type: {changeType}
Files modified: {fileContext}

Git changes:
{TruncateGitDiff(gitDiff)}

Rules:
- Keep it under 72 characters
- Use imperative mood (""Add"" not ""Added"")
- Be specific about what changed
- Follow conventional commits format when possible (feat:, fix:, docs:, etc.)
- Return ONLY the improved commit message, nothing else
- DO NOT include phrases like 'Here's a rewritten version' or 'Here is the commit message'
- DO NOT wrap your response in markdown code blocks or quotes
- Start directly with the commit message
- Focus on the specific changes in the mentioned files

Examples:
- ""fix stuff"" → ""fix: resolve user authentication validation error""
- ""update"" → ""feat: add user profile picture upload functionality""
- ""test"" → ""test: add unit tests for payment processing""

Improved commit message:";
    }

    private string DetermineChangeType(string gitDiff)
    {
        // Определяем тип изменений на основе diff
        bool isCodeChange = Regex.IsMatch(gitDiff, @"\+|\-", RegexOptions.Multiline);
        bool isCommentChange = Regex.IsMatch(gitDiff, @"\+\s*//|\+\s*\/\*|\+\s*\*", RegexOptions.Multiline);
        bool isDocChange = Regex.IsMatch(gitDiff, @"README|CHANGELOG|CONTRIBUTING", RegexOptions.IgnoreCase);
        bool isTestChange = Regex.IsMatch(gitDiff, @"test|assert|expect", RegexOptions.IgnoreCase);

        if (isDocChange) return "docs";
        if (isTestChange) return "test";
        if (isCommentChange) return "docs";
        if (isCodeChange) return "fix";

        return "chore";
    }

    private string ExtractFileContext(string gitDiff)
    {
        // Извлекаем имена измененных файлов
        var fileMatches = Regex.Matches(gitDiff, @"^\+\+\+ b/(.+)$", RegexOptions.Multiline);
        var files = fileMatches
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .Take(3) // Ограничиваем количество файлов
            .ToList();

        return files.Any() ? string.Join(", ", files) : "unknown files";
    }
    
    private string BuildDiffBasedPrompt(string gitDiff)
    {
        return $@"Generate a concise and professional git commit message based on the following changes:

Git changes:
{TruncateGitDiff(gitDiff)}

Rules:
- Keep it under 72 characters
- Use imperative mood (""Add"" not ""Added"")
- Be specific about what changed
- Follow conventional commits format when possible (feat:, fix:, docs:, etc.)
- Return ONLY the commit message, nothing else
- DO NOT include phrases like 'Here's a commit message' or 'Here is the commit message'
- DO NOT wrap your response in markdown code blocks or quotes
- Start directly with the commit message
- Focus on the most important changes if there are many

Examples:
- For added user authentication code → ""feat: implement user authentication flow""
- For fixed validation bug → ""fix: resolve input validation in registration form""
- For documentation updates → ""docs: update API documentation with new endpoints""

Generated commit message:";
    }

    private RestRequest CreateRequest(string prompt)
    {
        var request = new RestRequest("chat/completions", Method.Post);
        
        var requestBody = new
        {
            model = "llama-3.1-8b-instant",
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            max_tokens = 100,
            temperature = 0.3
        };

        request.AddJsonBody(requestBody);
        return request;
    }

    private string TruncateGitDiff(string gitDiff)
    {
        // Limit git diff to prevent token overflow
        const int maxLength = 2000;
        if (gitDiff.Length <= maxLength)
            return gitDiff;

        return gitDiff.Substring(0, maxLength) + "\n... (truncated)";
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}