using System.Globalization;
using System.Text.Json;
using Lingarr.Server.Interfaces.Services;
using Lingarr.Server.Models;

namespace Lingarr.Server.Services.Translation.Base;

public abstract class BaseLanguageService : BaseTranslationService
{
    private readonly string _languageFilePath;
    protected string? _contextPrompt;
    protected string? _contextPromptEnabled;
    protected Dictionary<string, string> _replacements;
    protected List<KeyValuePair<string, object>>? _customParameters;

    protected BaseLanguageService(
        ISettingService settings,
        ILogger logger,
        string languageFilePath) : base(settings, logger)
    {
        _languageFilePath = languageFilePath;
        _replacements = new Dictionary<string, string>();
    }

    /// <summary>
    /// Prepares custom parameters from settings for use in API requests.
    /// </summary>
    /// <param name="settings">Dictionary containing application settings.</param>
    /// <param name="parameterKey">The key to access the custom parameters in the settings.</param>
    protected List<KeyValuePair<string, object>>? PrepareCustomParameters(Dictionary<string, string> settings, string parameterKey)
    {
        if (!settings.TryGetValue(parameterKey, out var parametersJson) ||
            string.IsNullOrEmpty(parametersJson))
        {
            return null;
        }

        try
        {
            var parametersArray = JsonSerializer.Deserialize<JsonElement[]>(parametersJson);
            if (parametersArray == null)
            {
                return null;
            }

            var parameters = new List<KeyValuePair<string, object>>();
            foreach (var param in parametersArray)
            {
                if (!param.TryGetProperty("key", out var key) ||
                    !param.TryGetProperty("value", out var value)) continue;

                object valueObj = value.ValueKind switch
                {
                    JsonValueKind.String => TryParseNumeric(value.GetString()!),
                    JsonValueKind.Number => value.TryGetInt64(out var intVal) ? intVal : value.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => value.GetString()!
                };

                parameters.Add(new KeyValuePair<string, object>(key.GetString()!, valueObj));
            }
            return parameters;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse custom parameters: {Parameters}", parametersJson);
            return null;
        }
    }

    private static object TryParseNumeric(string value)
    {
        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatVal))
        {
            return floatVal;
        }

        return value;
    }

    protected string ReplacePlaceholders(string promptTemplate, Dictionary<string, string> replacements)
    {
        if (string.IsNullOrEmpty(promptTemplate))
            return promptTemplate;

        var result = promptTemplate;
        foreach (var replacement in replacements)
        {
            result = result.Replace($"{{{replacement.Key}}}", replacement.Value);
        }

        return result;
    }

    protected string ApplyContextIfEnabled(
        string text,
        List<string>? contextLinesBefore,
        List<string>? contextLinesAfter,
        Dictionary<string, string>? contextProperties = null)
    {
        if (_contextPromptEnabled != "true" || string.IsNullOrEmpty(_contextPrompt))
        {
            _logger.LogInformation("Context prompt disabled or empty. Enabled={Enabled}, Prompt={Prompt}",
                _contextPromptEnabled, string.IsNullOrEmpty(_contextPrompt) ? "NULL/EMPTY" : "SET");
            return text;
        }

        // Format context lines with markers to clearly separate each subtitle entry
        var beforeContext = contextLinesBefore != null && contextLinesBefore.Count > 0
            ? string.Join("\n", contextLinesBefore.Select((line, index) => $"[{index + 1}] {line}"))
            : "";
        var afterContext = contextLinesAfter != null && contextLinesAfter.Count > 0
            ? string.Join("\n", contextLinesAfter.Select((line, index) => $"[{index + 1}] {line}"))
            : "";

        _logger.LogInformation(
            "Applying context prompt. Before lines: {BeforeCount}, After lines: {AfterCount}",
            contextLinesBefore?.Count ?? 0,
            contextLinesAfter?.Count ?? 0);

        // Show first 100 chars of context to debug
        var beforePreview = string.IsNullOrEmpty(beforeContext) ? "EMPTY" :
            (beforeContext.Length > 100 ? beforeContext.Substring(0, 100) + "..." : beforeContext);
        var afterPreview = string.IsNullOrEmpty(afterContext) ? "EMPTY" :
            (afterContext.Length > 100 ? afterContext.Substring(0, 100) + "..." : afterContext);

        _logger.LogInformation(
            "Context preview - Before: [{Before}], After: [{After}]",
            beforePreview, afterPreview);

        // Use a local replacements dictionary to avoid mutating service-level state
        var replacements = new Dictionary<string, string>(_replacements)
        {
            ["contextBefore"] = beforeContext,
            ["lineToTranslate"] = text,
            ["contextAfter"] = afterContext
        };

        // Add individual context properties to replacements as {context.<key>}
        if (contextProperties != null && contextProperties.Count > 0)
        {
            foreach (var kv in contextProperties)
            {
                var key = $"context.{kv.Key}";
                replacements[key] = kv.Value ?? "";
            }

            // Composite string representation for convenience and a JSON form
            replacements["context"] = string.Join("\n", contextProperties.Select(kv => $"{kv.Key}: {kv.Value}"));
            try
            {
                replacements["contextJson"] = JsonSerializer.Serialize(contextProperties);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to serialize context properties to JSON");
            }

            _logger.LogInformation("Context properties applied: {Keys}", string.Join(", ", contextProperties.Keys));
        }

        var result = ReplacePlaceholders(_contextPrompt, replacements);
        var resultPreview = result.Length > 200 ? result.Substring(0, 200) + "..." : result;
        _logger.LogInformation("Final prompt preview (first 200 chars): {Result}", resultPreview);

        return result;
    }


    /// <summary>
    /// Adds custom parameters to the request data if they exist.
    /// </summary>
    /// <param name="requestData">The dictionary containing the base request parameters.</param>
    protected Dictionary<string, object> AddCustomParameters(Dictionary<string, object> requestData)
    {
        if (_customParameters != null && _customParameters.Count > 0)
        {
            foreach (var param in _customParameters)
            {
                requestData[param.Key] = param.Value;
            }
        }

        return requestData;
    }

    /// <inheritdoc />
    public override async Task<List<SourceLanguage>> GetLanguages()
    {
        _logger.LogInformation($"Retrieving |Green|{_languageFilePath}|/Green| languages");
        var sourceLanguages = await GetJson();

        var languageCodes = sourceLanguages.Select(l => l.Code).ToHashSet();
        return sourceLanguages
            .Select(lang => new SourceLanguage
            {
                Code = lang.Code,
                Name = lang.Name,
                Targets = languageCodes
                    .Where(code => code != lang.Code)
                    .ToList()
            })
            .ToList();
    }

    /// <summary>
    /// Reads and deserializes the language configuration from a JSON file.
    /// </summary>
    /// <returns>A list of language configurations from the JSON file</returns>
    /// <exception cref="JsonException">Thrown when deserialization of the JSON file fails</exception>
    /// <exception cref="IOException">Thrown when the file cannot be read</exception>
    private async Task<List<JsonLanguage>> GetJson()
    {
        string jsonContent = await File.ReadAllTextAsync(_languageFilePath);
        var sourceLanguages = JsonSerializer.Deserialize<List<JsonLanguage>>(jsonContent);
        if (sourceLanguages == null)
        {
            throw new JsonException($"Failed to deserialize {_languageFilePath}");
        }

        return sourceLanguages;
    }

    /// <inheritdoc />
    public override async Task<ModelsResponse> GetModels()
    {
        return await Task.FromResult(new ModelsResponse());
    }

    /// <summary>
    /// Converts a two-letter ISO language code to a full language name.
    /// </summary>
    /// <param name="twoLetterIsoLanguageName">The two-letter ISO language code to convert.</param>
    /// <returns>The full language name or the original code if no match is found.</returns>
    protected static string GetFullLanguageName(string twoLetterIsoLanguageName)
    {
        if (string.IsNullOrWhiteSpace(twoLetterIsoLanguageName))
            return twoLetterIsoLanguageName;

        try
        {
            var culture = CultureInfo.GetCultures(CultureTypes.AllCultures)
                .FirstOrDefault(c => string.Equals(c.TwoLetterISOLanguageName,
                    twoLetterIsoLanguageName, StringComparison.OrdinalIgnoreCase));

            return culture?.DisplayName ?? twoLetterIsoLanguageName;
        }
        catch
        {
            return twoLetterIsoLanguageName;
        }
    }
}
