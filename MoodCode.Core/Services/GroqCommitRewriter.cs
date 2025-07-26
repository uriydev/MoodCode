using MoodCode.Core.Models;
using Newtonsoft.Json;
using RestSharp;

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
            var rewrittenMessage = groqResponse?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();

            return !string.IsNullOrEmpty(rewrittenMessage) ? rewrittenMessage : currentMessage;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error rewriting commit: {ex.Message}");
            return currentMessage; // Fallback to original message
        }
    }

    private string BuildPrompt(string gitDiff, string currentMessage)
    {
        return $@"Rewrite this git commit message to be clear and professional:

Current message: ""{currentMessage}""

Git changes:
{TruncateGitDiff(gitDiff)}

Rules:
- Keep it under 72 characters
- Use imperative mood (""Add"" not ""Added"")
- Be specific about what changed
- Follow conventional commits format when possible (feat:, fix:, docs:, etc.)
- Return ONLY the improved commit message, nothing else

Examples:
- ""fix stuff"" → ""fix: resolve user authentication validation error""
- ""update"" → ""feat: add user profile picture upload functionality""
- ""test"" → ""test: add unit tests for payment processing""

Improved commit message:";
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
            temperature = 0.3,
            stop = new[] { "\n" } // Stop at first newline
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