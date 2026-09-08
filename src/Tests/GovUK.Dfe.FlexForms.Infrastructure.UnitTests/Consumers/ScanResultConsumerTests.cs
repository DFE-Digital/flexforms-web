using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.CoreLibs.Messaging.Contracts.Messages.Enums;
using GovUK.Dfe.CoreLibs.Messaging.Contracts.Messages.Events;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Application.Notifications;
using GovUK.Dfe.FlexForms.Domain.Caching;
using GovUK.Dfe.FlexForms.Infrastructure.Consumers;
using GovUK.Dfe.FlexForms.Infrastructure.Messaging;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StackExchange.Redis;
using System.Text.Json;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Infrastructure.UnitTests.Consumers;

public class ScanResultConsumerTests
{
    private readonly IApplicationsClient _applications = Substitute.For<IApplicationsClient>();
    private readonly INotificationsClient _notifications = Substitute.For<INotificationsClient>();
    private readonly IFileUploadService _fileUpload = Substitute.For<IFileUploadService>();
    private readonly IConfiguration _configuration = new ConfigurationBuilder().Build();

    [Fact]
    public async Task Consume_CleanOutcome_DoesNotDeleteOrNotify()
    {
        var redis = CreateRedis(out _);
        var consumer = CreateConsumer(redis);
        var context = CreateContext(CreateScanResult(Guid.NewGuid().ToString(), VirusScanOutcome.Clean));

        await consumer.Consume(context);

        await _fileUpload.DidNotReceiveWithAnyArgs().DeleteFileAsync(default, default);
        await _notifications.DidNotReceiveWithAnyArgs().CreateNotificationAsync(default!);
    }

    [Fact]
    public async Task Consume_UnexpectedOutcome_DoesNotDeleteOrNotify()
    {
        var redis = CreateRedis(out _);
        var consumer = CreateConsumer(redis);
        var context = CreateContext(CreateScanResult(Guid.NewGuid().ToString(), VirusScanOutcome.Error));

        await consumer.Consume(context);

        await _fileUpload.DidNotReceiveWithAnyArgs().DeleteFileAsync(default, default);
        await _notifications.DidNotReceiveWithAnyArgs().CreateNotificationAsync(default!);
    }

    [Fact]
    public async Task Consume_InfectedInvalidFileId_ReturnsEarly()
    {
        var redis = CreateRedis(out _);
        var consumer = CreateConsumer(redis);
        var metadata = CreateInfectedMetadata();
        var context = CreateContext(
            CreateScanResult("not-a-guid", VirusScanOutcome.Infected, "EICAR", metadata),
            metadata);

        await consumer.Consume(context);

        await _fileUpload.DidNotReceiveWithAnyArgs().DeleteFileAsync(default, default);
        await _notifications.DidNotReceiveWithAnyArgs().CreateNotificationAsync(default!);
    }

    [Fact]
    public async Task Consume_InfectedMissingTenantId_ReturnsEarly()
    {
        var redis = CreateRedis(out _);
        var consumer = CreateConsumer(redis);
        var metadata = CreateInfectedMetadata(includeTenant: false);
        var context = CreateContext(
            CreateScanResult(Guid.NewGuid().ToString(), VirusScanOutcome.Infected, "EICAR", metadata),
            metadata,
            includeTenantHeader: false);

        await consumer.Consume(context);

        await _fileUpload.DidNotReceiveWithAnyArgs().DeleteFileAsync(default, default);
        await _notifications.DidNotReceiveWithAnyArgs().CreateNotificationAsync(default!);
    }

    [Fact]
    public async Task Consume_InfectedMissingUserId_ReturnsEarly()
    {
        var redis = CreateRedis(out _);
        var consumer = CreateConsumer(redis);
        var metadata = CreateInfectedMetadata(includeUser: false);
        var context = CreateContext(
            CreateScanResult(Guid.NewGuid().ToString(), VirusScanOutcome.Infected, "EICAR", metadata),
            metadata,
            includeUserHeader: false);

        await consumer.Consume(context);

        await _fileUpload.DidNotReceiveWithAnyArgs().DeleteFileAsync(default, default);
        await _notifications.DidNotReceiveWithAnyArgs().CreateNotificationAsync(default!);
    }

    [Fact]
    public async Task Consume_InfectedMissingReference_ReturnsEarly()
    {
        var redis = CreateRedis(out _);
        var consumer = CreateConsumer(redis);
        var metadata = CreateInfectedMetadata(includeReference: false);
        var context = CreateContext(
            CreateScanResult(Guid.NewGuid().ToString(), VirusScanOutcome.Infected, "EICAR", metadata),
            metadata);

        await consumer.Consume(context);

        await _fileUpload.DidNotReceiveWithAnyArgs().DeleteFileAsync(default, default);
        await _notifications.DidNotReceiveWithAnyArgs().CreateNotificationAsync(default!);
    }

    [Fact]
    public async Task Consume_InfectedMissingApplicationId_ReturnsEarly()
    {
        var redis = CreateRedis(out _);
        var consumer = CreateConsumer(redis);
        var metadata = CreateInfectedMetadata(includeApplicationId: false);
        var context = CreateContext(
            CreateScanResult(Guid.NewGuid().ToString(), VirusScanOutcome.Infected, "EICAR", metadata),
            metadata);

        await consumer.Consume(context);

        await _fileUpload.DidNotReceiveWithAnyArgs().DeleteFileAsync(default, default);
        await _notifications.DidNotReceiveWithAnyArgs().CreateNotificationAsync(default!);
    }

    [Fact]
    public async Task Consume_InfectedHappyPath_DeletesNotifiesAndBlacklistsInRedis()
    {
        var fileId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        const string reference = "REF-001";
        const string originalFileName = "upload.pdf";
        var dbRecordId = Guid.NewGuid();

        var metadata = CreateInfectedMetadata(
            tenantId: Guid.NewGuid(),
            userId: userId,
            applicationId: applicationId,
            templateId: templateId,
            reference: reference,
            originalFileName: originalFileName);

        var redis = CreateRedis(out var database);
        var consumer = CreateConsumer(redis);
        var context = CreateContext(
            CreateScanResult(fileId.ToString(), VirusScanOutcome.Infected, "EICAR", metadata),
            metadata);

        _fileUpload.GetFilesForApplicationAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns([new UploadDto { Id = dbRecordId, OriginalFileName = originalFileName }]);
        _applications.GetApplicationByReferenceAsync(reference, Arg.Any<CancellationToken>())
            .Returns(new ApplicationDto
            {
                ApplicationId = applicationId,
                ApplicationReference = reference,
                LatestResponse = null
            });

        await consumer.Consume(context);

        await _fileUpload.Received(1).DeleteFileAsync(fileId, applicationId);
        await _notifications.Received(1).CreateNotificationAsync(
            Arg.Is<AddNotificationRequest>(n =>
                n.UserId == userId
                && n.Category == "malware-detection"
                && n.Metadata != null
                && n.Metadata["fileId"].ToString() == fileId.ToString()
                && n.Metadata["applicationId"].ToString() == applicationId.ToString()
                && n.Metadata["templateId"].ToString() == templateId.ToString()));

        await database.Received().StringSetAsync(
            Arg.Is<RedisKey>(k => k.ToString().StartsWith(FlexFormsCacheKeys.InfectedFilePrefix, StringComparison.Ordinal)),
            Arg.Any<RedisValue>(),
            Arg.Any<TimeSpan?>());

        await database.Received().StringSetAsync(
            Arg.Is<RedisKey>(k => k.ToString().StartsWith(FlexFormsCacheKeys.InfectedFileNamePrefix, StringComparison.Ordinal)),
            Arg.Any<RedisValue>(),
            Arg.Any<TimeSpan?>());
    }

    [Fact]
    public async Task Consume_InfectedDeleteFailure_StillNotifiesAndBlacklists()
    {
        var fileId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        const string reference = "REF-002";
        const string originalFileName = "upload.pdf";

        var metadata = CreateInfectedMetadata(
            userId: userId,
            applicationId: applicationId,
            reference: reference,
            originalFileName: originalFileName,
            applicationName: "Transfers");

        var redis = CreateRedis(out _);
        var consumer = CreateConsumer(redis);
        var context = CreateContext(
            CreateScanResult(fileId.ToString(), VirusScanOutcome.Infected, "EICAR", metadata),
            metadata);

        _fileUpload.GetFilesForApplicationAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns([]);
        _fileUpload.DeleteFileAsync(fileId, applicationId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("already deleted")));
        _applications.GetApplicationByReferenceAsync(reference, Arg.Any<CancellationToken>())
            .Returns(new ApplicationDto
            {
                ApplicationId = applicationId,
                ApplicationReference = reference,
                LatestResponse = null
            });

        await consumer.Consume(context);

        await _notifications.Received(1).CreateNotificationAsync(Arg.Any<AddNotificationRequest>());
    }

    [Fact]
    public async Task Consume_InfectedNotificationFailure_DoesNotRethrow()
    {
        var fileId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        const string reference = "REF-003";
        var metadata = CreateInfectedMetadata(
            userId: userId,
            applicationId: applicationId,
            reference: reference,
            applicationName: "Transfers");

        var redis = CreateRedis(out _);
        var consumer = CreateConsumer(redis);
        var context = CreateContext(
            CreateScanResult(fileId.ToString(), VirusScanOutcome.Infected, "EICAR", metadata),
            metadata);

        _fileUpload.GetFilesForApplicationAsync(applicationId, Arg.Any<CancellationToken>()).Returns([]);
        _applications.GetApplicationByReferenceAsync(reference, Arg.Any<CancellationToken>())
            .Returns(new ApplicationDto
            {
                ApplicationId = applicationId,
                ApplicationReference = reference,
                LatestResponse = null
            });
        _notifications
            .When(x => x.CreateNotificationAsync(Arg.Any<AddNotificationRequest>()))
            .Do(_ => throw new InvalidOperationException("notification down"));

        var exception = await Record.ExceptionAsync(() => consumer.Consume(context));

        Assert.Null(exception);
    }

    [Fact]
    public async Task Consume_InfectedRedisFailure_StillCreatesNotification()
    {
        var fileId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        const string reference = "REF-004";
        var metadata = CreateInfectedMetadata(
            userId: userId,
            applicationId: applicationId,
            reference: reference,
            applicationName: "Transfers");

        var redis = CreateRedis(out var database);
        database.StringSetAsync(
                Arg.Is<RedisKey>(k => k.ToString().StartsWith(FlexFormsCacheKeys.InfectedFilePrefix, StringComparison.Ordinal)),
                Arg.Any<RedisValue>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<When>(),
                Arg.Any<CommandFlags>())
            .Returns(Task.FromException<bool>(new RedisException("redis unavailable")));

        var consumer = CreateConsumer(redis);
        var context = CreateContext(
            CreateScanResult(fileId.ToString(), VirusScanOutcome.Infected, "EICAR", metadata),
            metadata);

        _fileUpload.GetFilesForApplicationAsync(applicationId, Arg.Any<CancellationToken>()).Returns([]);
        _applications.GetApplicationByReferenceAsync(reference, Arg.Any<CancellationToken>())
            .Returns(new ApplicationDto
            {
                ApplicationId = applicationId,
                ApplicationReference = reference,
                LatestResponse = null
            });

        await consumer.Consume(context);

        await _notifications.Received(1).CreateNotificationAsync(Arg.Any<AddNotificationRequest>());
    }

    [Fact]
    public async Task Consume_InfectedFilenameAlreadyBlacklisted_StillCompletes()
    {
        var fileId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        const string reference = "REF-005";
        const string originalFileName = "upload.pdf";
        var metadata = CreateInfectedMetadata(
            userId: userId,
            applicationId: applicationId,
            reference: reference,
            originalFileName: originalFileName,
            applicationName: "Transfers");

        var redis = CreateRedis(out var database);
        database.KeyExistsAsync(
                Arg.Is<RedisKey>(k => k.ToString().StartsWith(FlexFormsCacheKeys.InfectedFileNamePrefix, StringComparison.Ordinal)),
                Arg.Any<CommandFlags>())
            .Returns(true);

        var consumer = CreateConsumer(redis);
        var context = CreateContext(
            CreateScanResult(fileId.ToString(), VirusScanOutcome.Infected, "EICAR", metadata),
            metadata);

        _fileUpload.GetFilesForApplicationAsync(applicationId, Arg.Any<CancellationToken>()).Returns([]);
        _applications.GetApplicationByReferenceAsync(reference, Arg.Any<CancellationToken>())
            .Returns(new ApplicationDto
            {
                ApplicationId = applicationId,
                ApplicationReference = reference,
                LatestResponse = null
            });

        await consumer.Consume(context);

        await database.Received().KeyExistsAsync(
            Arg.Is<RedisKey>(k => k.ToString().StartsWith(FlexFormsCacheKeys.InfectedFileNamePrefix, StringComparison.Ordinal)),
            Arg.Any<CommandFlags>());
        await _notifications.Received(1).CreateNotificationAsync(Arg.Any<AddNotificationRequest>());
    }

    [Fact]
    public async Task Consume_InfectedWithoutTemplateId_OmitsTemplateFromNotificationMetadata()
    {
        var fileId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        const string reference = "REF-006";
        var metadata = CreateInfectedMetadata(
            userId: userId,
            applicationId: applicationId,
            reference: reference,
            includeTemplateId: false,
            applicationName: "Transfers");

        var redis = CreateRedis(out _);
        var consumer = CreateConsumer(redis);
        var context = CreateContext(
            CreateScanResult(fileId.ToString(), VirusScanOutcome.Infected, "EICAR", metadata),
            metadata,
            includeTemplateHeader: false);

        _fileUpload.GetFilesForApplicationAsync(applicationId, Arg.Any<CancellationToken>()).Returns([]);
        _applications.GetApplicationByReferenceAsync(reference, Arg.Any<CancellationToken>())
            .Returns(new ApplicationDto
            {
                ApplicationId = applicationId,
                ApplicationReference = reference,
                LatestResponse = null
            });

        await consumer.Consume(context);

        await _notifications.Received(1).CreateNotificationAsync(
            Arg.Is<AddNotificationRequest>(n =>
                n.Metadata != null
                && !n.Metadata.ContainsKey("templateId")));
    }

    [Fact]
    public async Task Consume_InfectedUsesApplicationNameForNotificationContext()
    {
        var fileId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        const string reference = "REF-007";
        const string applicationName = "School Transfers";
        var metadata = CreateInfectedMetadata(
            userId: userId,
            applicationId: applicationId,
            reference: reference,
            applicationName: applicationName);

        var redis = CreateRedis(out _);
        var consumer = CreateConsumer(redis);
        var context = CreateContext(
            CreateScanResult(fileId.ToString(), VirusScanOutcome.Infected, "EICAR", metadata),
            metadata);

        _fileUpload.GetFilesForApplicationAsync(applicationId, Arg.Any<CancellationToken>()).Returns([]);
        _applications.GetApplicationByReferenceAsync(reference, Arg.Any<CancellationToken>())
            .Returns(new ApplicationDto
            {
                ApplicationId = applicationId,
                ApplicationReference = reference,
                LatestResponse = null
            });

        await consumer.Consume(context);

        var expectedContext = NotificationScopeContext.Build(applicationName, "malware-detection", fileId.ToString());
        await _notifications.Received(1).CreateNotificationAsync(
            Arg.Is<AddNotificationRequest>(n => n.Context == expectedContext));
    }

    [Fact]
    public async Task Consume_InfectedCleansInfectedFilesFromResponseData()
    {
        var fileId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        const string reference = "REF-008";
        const string originalFileName = "report.pdf";
        const string fieldKey = "supporting-doc";
        var metadata = CreateInfectedMetadata(
            userId: userId,
            applicationId: applicationId,
            reference: reference,
            originalFileName: originalFileName,
            applicationName: "Transfers");

        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var infectedFilesJson = JsonSerializer.Serialize(new[]
        {
            new UploadDto
            {
                Id = fileId,
                OriginalFileName = originalFileName,
                Name = originalFileName,
                FileName = originalFileName
            },
            new UploadDto
            {
                Id = Guid.NewGuid(),
                OriginalFileName = "clean.pdf",
                Name = "clean.pdf",
                FileName = "clean.pdf"
            }
        }, jsonOptions);
        var responseBody = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            [fieldKey] = new
            {
                question = "Upload evidence",
                value = infectedFilesJson,
                completed = true,
                dataType = "string"
            }
        });

        var redis = CreateRedis(out var database, out var server);
        server.Keys(pattern: Arg.Any<RedisValue>())
            .Returns(callInfo =>
            {
                var pattern = callInfo.Arg<RedisValue>().ToString();
                return pattern.Contains(FlexFormsCacheKeys.RedisKeyPrefix, StringComparison.Ordinal)
                    ? new[] { new RedisKey($"{FlexFormsCacheKeys.RedisKeyPrefix}{applicationId}") }
                    : Array.Empty<RedisKey>();
            });

        var consumer = CreateConsumer(redis);
        var context = CreateContext(
            CreateScanResult(fileId.ToString(), VirusScanOutcome.Infected, "EICAR", metadata),
            metadata);

        _fileUpload.GetFilesForApplicationAsync(applicationId, Arg.Any<CancellationToken>()).Returns([]);
        _applications.GetApplicationByReferenceAsync(reference, Arg.Any<CancellationToken>())
            .Returns(new ApplicationDto
            {
                ApplicationId = applicationId,
                ApplicationReference = reference,
                LatestResponse = new ApplicationResponseDetailsDto
                {
                    ResponseId = Guid.NewGuid(),
                    ResponseBody = responseBody,
                    CreatedOn = DateTime.UtcNow,
                    CreatedBy = userId
                }
            });

        await consumer.Consume(context);

        await _applications.Received(1).AddApplicationResponseAsync(
            applicationId,
            Arg.Any<AddApplicationResponseRequest>(),
            Arg.Any<CancellationToken>());
        await database.Received().KeyDeleteAsync(
            Arg.Is<RedisKey>(k => k.ToString().Contains(applicationId.ToString(), StringComparison.Ordinal)),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task Consume_InfectedMissingMalwareName_DoesNotHandleAsInfected()
    {
        var redis = CreateRedis(out _);
        var consumer = CreateConsumer(redis);
        var metadata = CreateInfectedMetadata();
        var context = CreateContext(
            CreateScanResult(Guid.NewGuid().ToString(), VirusScanOutcome.Infected, malwareName: " ", metadata),
            metadata);

        await consumer.Consume(context);

        await _fileUpload.DidNotReceiveWithAnyArgs().DeleteFileAsync(default, default);
        await _notifications.DidNotReceiveWithAnyArgs().CreateNotificationAsync(default!);
    }

    private ScanResultConsumer CreateConsumer(IConnectionMultiplexer redis) =>
        new(
            _applications,
            _notifications,
            redis,
            _fileUpload,
            _configuration,
            NullLogger<ScanResultConsumer>.Instance);

    private static IConnectionMultiplexer CreateRedis(out IDatabase database) =>
        CreateRedis(out database, out _);

    private static IConnectionMultiplexer CreateRedis(out IDatabase database, out IServer server)
    {
        database = Substitute.For<IDatabase>();
        database.KeyExistsAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).Returns(true);

        server = Substitute.For<IServer>();
        server.Keys(pattern: Arg.Any<RedisValue>()).Returns(Array.Empty<RedisKey>());

        var redis = Substitute.For<IConnectionMultiplexer>();
        redis.GetDatabase().Returns(database);
        redis.GetEndPoints().Returns([new System.Net.DnsEndPoint("localhost", 6379)]);
        redis.GetServer(Arg.Any<System.Net.EndPoint>()).Returns(server);
        return redis;
    }

    private static ConsumeContext<ScanResultEvent> CreateContext(
        ScanResultEvent message,
        IDictionary<string, object>? metadata = null,
        bool includeTenantHeader = true,
        bool includeUserHeader = true,
        bool includeTemplateHeader = true)
    {
        var context = Substitute.For<ConsumeContext<ScanResultEvent>>();
        context.Message.Returns(message);

        var headers = Substitute.For<Headers>();
        if (includeTenantHeader)
        {
            var tenantId = ScanEventRouting.GetMetadata(metadata, ScanEventRouting.TenantIdMetadata);
            if (!string.IsNullOrWhiteSpace(tenantId))
                headers.Get<string>(ScanEventRouting.TenantIdHeader).Returns(tenantId);
        }

        if (includeUserHeader)
        {
            var userId = ScanEventRouting.GetMetadata(metadata, ScanEventRouting.UserIdMetadata);
            if (!string.IsNullOrWhiteSpace(userId))
                headers.Get<string>(ScanEventRouting.UserIdHeader).Returns(userId);
        }

        if (includeTemplateHeader)
        {
            var templateId = ScanEventRouting.GetMetadata(metadata, ScanEventRouting.TemplateIdMetadata);
            if (!string.IsNullOrWhiteSpace(templateId))
                headers.Get<string>(ScanEventRouting.TemplateIdHeader).Returns(templateId);
        }

        context.Headers.Returns(headers);
        return context;
    }

    private static ScanResultEvent CreateScanResult(
        string fileId,
        VirusScanOutcome outcome,
        string? malwareName = null,
        IDictionary<string, object>? metadata = null) =>
        new(
            FileId: fileId,
            FileName: "upload.pdf",
            Reference: "REF-1",
            Path: "/files/upload.pdf",
            IsAzureFileShare: true,
            FileUri: "https://example.test/files/upload.pdf",
            ServiceName: "FlexForms",
            CorrelationId: Guid.NewGuid().ToString(),
            Outcome: outcome,
            MalwareName: malwareName,
            ScannedAt: DateTimeOffset.UtcNow,
            ScannerVersion: "1.0",
            Message: "Scan complete",
            Metadata: metadata is null ? null : new Dictionary<string, object>(metadata));

    private static Dictionary<string, object> CreateInfectedMetadata(
        Guid? tenantId = null,
        Guid? userId = null,
        Guid? applicationId = null,
        Guid? templateId = null,
        string reference = "REF-001",
        string originalFileName = "upload.pdf",
        string applicationName = "Transfers",
        bool includeTenant = true,
        bool includeUser = true,
        bool includeReference = true,
        bool includeApplicationId = true,
        bool includeTemplateId = true)
    {
        var metadata = new Dictionary<string, object>();

        if (includeTenant)
            metadata[ScanEventRouting.TenantIdMetadata] = (tenantId ?? Guid.NewGuid()).ToString();

        if (includeUser)
            metadata[ScanEventRouting.UserIdMetadata] = (userId ?? Guid.NewGuid()).ToString();

        if (includeReference)
            metadata[ScanEventRouting.ReferenceMetadata] = reference;

        if (includeApplicationId)
            metadata[ScanEventRouting.ApplicationIdMetadata] = (applicationId ?? Guid.NewGuid()).ToString();

        metadata[ScanEventRouting.OriginalFileNameMetadata] = originalFileName;

        if (includeTemplateId)
            metadata[ScanEventRouting.TemplateIdMetadata] = (templateId ?? Guid.NewGuid()).ToString();

        metadata[ScanEventRouting.ApplicationNameMetadata] = applicationName;

        return metadata;
    }
}
