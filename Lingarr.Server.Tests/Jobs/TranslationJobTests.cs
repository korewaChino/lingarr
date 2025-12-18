using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lingarr.Core.Configuration;
using Lingarr.Core.Data;
using Lingarr.Core.Entities;
using Lingarr.Core.Enum;
using Lingarr.Server.Interfaces.Services;
using Lingarr.Server.Interfaces.Services.Translation;
using Lingarr.Server.Jobs;
using Lingarr.Server.Models.FileSystem;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Lingarr.Server.Tests.Jobs;

public class TranslationJobTests : IDisposable
{
    private readonly Mock<ILogger<TranslationJob>> _loggerMock;
    private readonly Mock<ISettingService> _settingServiceMock;
    private readonly LingarrDbContext _dbContext;
    private readonly Mock<IProgressService> _progressServiceMock;
    private readonly Mock<ISubtitleService> _subtitleServiceMock;
    private readonly Mock<IScheduleService> _scheduleServiceMock;
    private readonly Mock<IStatisticsService> _statisticsServiceMock;
    private readonly Mock<ITranslationServiceFactory> _translationServiceFactoryMock;
    private readonly Mock<ITranslationRequestService> _translationRequestServiceMock;
    private readonly TranslationJob _translationJob;

    public TranslationJobTests()
    {
        _loggerMock = new Mock<ILogger<TranslationJob>>();
        _settingServiceMock = new Mock<ISettingService>();

        var options = new DbContextOptionsBuilder<LingarrDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new LingarrDbContext(options);

        _progressServiceMock = new Mock<IProgressService>();
        _subtitleServiceMock = new Mock<ISubtitleService>();
        _scheduleServiceMock = new Mock<IScheduleService>();
        _statisticsServiceMock = new Mock<IStatisticsService>();
        _translationServiceFactoryMock = new Mock<ITranslationServiceFactory>();
        _translationRequestServiceMock = new Mock<ITranslationRequestService>();

        _translationJob = new TranslationJob(
            _loggerMock.Object,
            _settingServiceMock.Object,
            _dbContext,
            _progressServiceMock.Object,
            _subtitleServiceMock.Object,
            _scheduleServiceMock.Object,
            _statisticsServiceMock.Object,
            _translationServiceFactoryMock.Object,
            _translationRequestServiceMock.Object
        );
    }

    [Fact]
    public async Task Execute_WithContextSettings_ShouldRetrieveContextBeforeAndAfterSettingsCorrectly()
    {
        // Arrange
        var settings = new Dictionary<string, string>
        {
            [SettingKeys.Translation.ServiceType] = "OpenAI",
            [SettingKeys.Translation.FixOverlappingSubtitles] = "false",
            [SettingKeys.Translation.StripSubtitleFormatting] = "false",
            [SettingKeys.Translation.AddTranslatorInfo] = "false",
            [SettingKeys.SubtitleValidation.ValidateSubtitles] = "false",
            [SettingKeys.SubtitleValidation.MaxFileSizeBytes] = "2097152",
            [SettingKeys.SubtitleValidation.MaxSubtitleLength] = "500",
            [SettingKeys.SubtitleValidation.MinSubtitleLength] = "2",
            [SettingKeys.SubtitleValidation.MinDurationMs] = "500",
            [SettingKeys.SubtitleValidation.MaxDurationSecs] = "10",
            [SettingKeys.Translation.AiContextPromptEnabled] = "true",
            [SettingKeys.Translation.AiContextBefore] = "2",
            [SettingKeys.Translation.AiContextAfter] = "3",
            [SettingKeys.Translation.UseBatchTranslation] = "false",
            [SettingKeys.Translation.MaxBatchSize] = "10000",
            [SettingKeys.Translation.RemoveLanguageTag] = "false",
            [SettingKeys.Translation.UseSubtitleTagging] = "false",
            [SettingKeys.Translation.SubtitleTag] = ""
        };

        _settingServiceMock
            .Setup(s => s.GetSettings(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(settings);

        var translationRequest = new TranslationRequest
        {
            Id = 1,
            Title = "Test Movie",
            MediaType = MediaType.Movie,
            SubtitleToTranslate = "/tmp/test.srt",
            SourceLanguage = "en",
            TargetLanguage = "es",
            Status = TranslationStatus.Pending
        };

        await _dbContext.TranslationRequests.AddAsync(translationRequest);
        await _dbContext.SaveChangesAsync();

        _translationRequestServiceMock
            .Setup(s => s.UpdateTranslationRequest(It.IsAny<TranslationRequest>(), It.IsAny<TranslationStatus>(), It.IsAny<string>()))
            .ReturnsAsync(translationRequest);

        var mockTranslationService = new Mock<ITranslationService>();
        mockTranslationService.Setup(s => s.ModelName).Returns("test-model");

        _translationServiceFactoryMock
            .Setup(f => f.CreateTranslationService(It.IsAny<string>()))
            .Returns(mockTranslationService.Object);

        _subtitleServiceMock
            .Setup(s => s.ReadSubtitles(It.IsAny<string>()))
            .ReturnsAsync(new List<SubtitleItem>
            {
                new() { Position = 1, Lines = new List<string> { "Test subtitle" } }
            });

        _subtitleServiceMock
            .Setup(s => s.ValidateSubtitle(It.IsAny<string>(), It.IsAny<Server.Models.SubtitleValidationOptions>()))
            .Returns(true);

        _subtitleServiceMock
            .Setup(s => s.CreateFilePath(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("/tmp/test.es.srt");

        mockTranslationService
            .Setup(s => s.TranslateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<string>?>(),
                It.IsAny<List<string>?>(),
                It.IsAny<Dictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Subtítulo de prueba");

        // Act
        try
        {
            await _translationJob.Execute(translationRequest, CancellationToken.None);
        }
        catch (Exception)
        {
            // Expected to fail due to missing file/infrastructure, but we can verify the settings were retrieved
        }

        // Assert - Verify that GetSettings was called with the correct keys including both context settings
        _settingServiceMock.Verify(
            s => s.GetSettings(It.Is<IEnumerable<string>>(keys =>
                keys.Any(k => k == SettingKeys.Translation.AiContextBefore) &&
                keys.Any(k => k == SettingKeys.Translation.AiContextAfter))),
            Times.Once,
            "GetSettings should be called with both AiContextBefore and AiContextAfter keys");
    }

    [Fact]
    public async Task Execute_WithMissingContextSettings_ShouldNotThrowException()
    {
        // Arrange - Settings dictionary without context settings
        var settings = new Dictionary<string, string>
        {
            [SettingKeys.Translation.ServiceType] = "OpenAI",
            [SettingKeys.Translation.FixOverlappingSubtitles] = "false",
            [SettingKeys.Translation.StripSubtitleFormatting] = "false",
            [SettingKeys.Translation.AddTranslatorInfo] = "false",
            [SettingKeys.SubtitleValidation.ValidateSubtitles] = "false",
            [SettingKeys.SubtitleValidation.MaxFileSizeBytes] = "2097152",
            [SettingKeys.SubtitleValidation.MaxSubtitleLength] = "500",
            [SettingKeys.SubtitleValidation.MinSubtitleLength] = "2",
            [SettingKeys.SubtitleValidation.MinDurationMs] = "500",
            [SettingKeys.SubtitleValidation.MaxDurationSecs] = "10",
            [SettingKeys.Translation.AiContextPromptEnabled] = "true",
            // Intentionally omitting AiContextBefore and AiContextAfter
            [SettingKeys.Translation.UseBatchTranslation] = "false",
            [SettingKeys.Translation.MaxBatchSize] = "10000",
            [SettingKeys.Translation.RemoveLanguageTag] = "false",
            [SettingKeys.Translation.UseSubtitleTagging] = "false",
            [SettingKeys.Translation.SubtitleTag] = ""
        };

        _settingServiceMock
            .Setup(s => s.GetSettings(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(settings);

        var translationRequest = new TranslationRequest
        {
            Id = 2,
            Title = "Test Movie 2",
            MediaType = MediaType.Movie,
            SubtitleToTranslate = "/tmp/test2.srt",
            SourceLanguage = "en",
            TargetLanguage = "es",
            Status = TranslationStatus.Pending
        };

        await _dbContext.TranslationRequests.AddAsync(translationRequest);
        await _dbContext.SaveChangesAsync();

        _translationRequestServiceMock
            .Setup(s => s.UpdateTranslationRequest(It.IsAny<TranslationRequest>(), It.IsAny<TranslationStatus>(), It.IsAny<string>()))
            .ReturnsAsync(translationRequest);

        var mockTranslationService = new Mock<ITranslationService>();
        mockTranslationService.Setup(s => s.ModelName).Returns("test-model");

        _translationServiceFactoryMock
            .Setup(f => f.CreateTranslationService(It.IsAny<string>()))
            .Returns(mockTranslationService.Object);

        _subtitleServiceMock
            .Setup(s => s.ReadSubtitles(It.IsAny<string>()))
            .ReturnsAsync(new List<SubtitleItem>
            {
                new() { Position = 1, Lines = new List<string> { "Test subtitle" } }
            });

        _subtitleServiceMock
            .Setup(s => s.ValidateSubtitle(It.IsAny<string>(), It.IsAny<Server.Models.SubtitleValidationOptions>()))
            .Returns(true);

        _subtitleServiceMock
            .Setup(s => s.CreateFilePath(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("/tmp/test2.es.srt");

        mockTranslationService
            .Setup(s => s.TranslateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<List<string>?>(),
                It.IsAny<List<string>?>(),
                It.IsAny<Dictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Subtítulo de prueba");

        // Act & Assert - Should not throw KeyNotFoundException
        try
        {
            await _translationJob.Execute(translationRequest, CancellationToken.None);
        }
        catch (KeyNotFoundException)
        {
            Assert.Fail("TranslationJob.Execute should not throw KeyNotFoundException when context settings are missing");
        }
        catch (Exception)
        {
            // Other exceptions are acceptable (file I/O, etc.)
        }
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
        GC.SuppressFinalize(this);
    }
}
