using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.CoreLibs.Messaging.Contracts.Exceptions;
using GovUK.Dfe.CoreLibs.Messaging.Contracts.Messages.Enums;
using GovUK.Dfe.CoreLibs.Messaging.Contracts.Messages.Events;
using GovUK.Dfe.CoreLibs.Messaging.MassTransit.Helpers;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Api.Client.Security;
using GovUK.Dfe.FlexForms.Domain.Caching;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;

namespace GovUK.Dfe.FlexForms.Infrastructure.Consumers
{
    /// <summary>
    /// Consumer for file scan results from the virus scanner service.
    /// Listens to the file-scanner-results topic with subscription extweb.
    /// Handles infected files by cleaning them up from Redis sessions and notifying users.
    /// </summary>
    public sealed class ScanResultConsumer(
        IApplicationsClient applicationsClient,
        INotificationsClient notificationsClient,
        IConnectionMultiplexer redis,
        IFileUploadService fileUploadService,
        IConfiguration configuration,
        ILogger<ScanResultConsumer> logger) : IConsumer<ScanResultEvent>
    {
        public async Task Consume(ConsumeContext<ScanResultEvent> context)
        {
            var scanResult = context.Message;

            logger.LogInformation(
                "Received scan result - FileName: {FileName}, FileId: {FileId}, Status: {Status}, Outcome: {Outcome}, MalwareName: {MalwareName}",
                scanResult.FileName,
                scanResult.FileId,
                scanResult.Status,
                scanResult.Outcome,
                scanResult.MalwareName);

            // LOCAL ENVIRONMENT ONLY: Check if this message is for this instance
            // This allows developers to run locally without interfering with each other
            if (InstanceIdentifierHelper.IsLocalEnvironment())
            {
                var messageInstanceId = scanResult.Metadata?.ContainsKey("InstanceIdentifier") == true 
                    ? scanResult.Metadata["InstanceIdentifier"]?.ToString() 
                    : null;
                
                var localInstanceId = InstanceIdentifierHelper.GetInstanceIdentifier(configuration);
                
                if (!InstanceIdentifierHelper.IsMessageForThisInstance(messageInstanceId, localInstanceId))
                {
                    logger.LogDebug(
                        "Message {FileId} not for this instance (MessageInstanceId: '{MessageInstanceId}', LocalInstanceId: '{LocalInstanceId}') - throwing exception to requeue for other consumers",
                        scanResult.FileId,
                        messageInstanceId ?? "none",
                        localInstanceId ?? "none");
                    
                    // Throw exception to prevent acknowledgment and allow other consumers to process
                    // Service Bus will redeliver this message to another consumer instance
                    throw new MessageNotForThisInstanceException(
                        $"Message InstanceIdentifier '{messageInstanceId}' doesn't match local instance '{localInstanceId}'");
                }
            }

            // Check if the file is infected
            if (IsInfected(scanResult))
            {
                await HandleInfectedFileAsync(scanResult, ResolveNotificationContext(scanResult, context));
            }
            else if (scanResult.Outcome == VirusScanOutcome.Clean)
            {
                logger.LogInformation(
                    "File {FileName} ({FileId}) is clean",
                    scanResult.FileName,
                    scanResult.FileId);
            }
            else
            {
                logger.LogWarning(
                    "Scan completed with unexpected outcome - FileName: {FileName}, FileId: {FileId}, Outcome: {Outcome}",
                    scanResult.FileName,
                    scanResult.FileId,
                    scanResult.Outcome);
            }
        }

        /// <summary>
        /// Checks if the scan result indicates an infected file
        /// </summary>
        private bool IsInfected(ScanResultEvent scanResult)
        {
            return scanResult.Outcome == VirusScanOutcome.Infected
                   && !string.IsNullOrWhiteSpace(scanResult.MalwareName);
        }

        /// <summary>
        /// Handles an infected file by cleaning it up and notifying the user
        /// </summary>
        private async Task HandleInfectedFileAsync(ScanResultEvent scanResult, string notificationContext)
        {
            try
            {
                if (!Guid.TryParse(scanResult.FileId, out var fileId))
                {
                    logger.LogWarning(
                        "Invalid FileId in scan result: {FileId}",
                        scanResult.FileId);
                    return;
                }

                // Extract metadata
                if (scanResult.Metadata == null)
                {
                    logger.LogWarning("No Metadata found for infected file {FileId}", fileId);
                    return;
                }

                if (!scanResult.Metadata.ContainsKey("Reference"))
                {
                    logger.LogWarning("No Reference found in Metadata for infected file {FileId}", fileId);
                    return;
                }

                if (!scanResult.Metadata.ContainsKey("userId"))
                {
                    logger.LogWarning("No userId found in Metadata for infected file {FileId}", fileId);
                    return;
                }

                var reference = scanResult.Metadata["Reference"]?.ToString();
                if (string.IsNullOrWhiteSpace(reference))
                {
                    logger.LogWarning("Empty Reference in Metadata for infected file {FileId}", fileId);
                    return;
                }

                var userId = scanResult.Metadata["userId"]?.ToString();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    logger.LogWarning("Empty userId in Metadata for infected file {FileId}", fileId);
                    return;
                }

                // Get applicationId from Metadata (for cache clearing)
                Guid? applicationId = null;
                if (scanResult.Metadata.ContainsKey("applicationId") && 
                    Guid.TryParse(scanResult.Metadata["applicationId"]?.ToString(), out var appId))
                {
                    applicationId = appId;
                }

                var originalFileName = scanResult.Metadata.ContainsKey("originalFileName") 
                    ? scanResult.Metadata["originalFileName"]?.ToString() 
                    : scanResult.FileName;

                logger.LogWarning(
                    "Processing infected file - FileId: {FileId}, FileName: {FileName}, OriginalFileName: {OriginalFileName}, Reference: {Reference}, UserId: {UserId}, MalwareName: {MalwareName}",
                    fileId,
                    scanResult.FileName,
                    originalFileName,
                    reference,
                    userId,
                    scanResult.MalwareName);

                // Use service-to-service authentication for all API calls (database cleanup + notification)
                using (AuthenticationContext.UseServiceToServiceAuthScope())
                {
                    // IMPORTANT: The fileId from the scan result is the blob storage ID, not the database record ID.
                    // The web app displays files using database record IDs, so we need to find those IDs
                    // by looking up files by original filename for this application.
                    var databaseRecordIds = await FindDatabaseRecordIdsByOriginalFileNameAsync(
                        applicationId!.Value, 
                        originalFileName);

                    logger.LogInformation(
                        "Found {Count} database record ID(s) matching original filename '{OriginalFileName}' for application {ApplicationId}: {Ids}",
                        databaseRecordIds.Count,
                        originalFileName,
                        applicationId,
                        string.Join(", ", databaseRecordIds));

                    // Delete the file from Azure File Share and database using blob storage ID
                    try
                    {
                        await fileUploadService.DeleteFileAsync(fileId, applicationId!.Value);
                    }
                    catch (Exception e)
                    {
                        logger.LogWarning("File doesn't exist to delete, perhaps removed already. Error: {Error}", e.Message);
                    }

                    // Clean up infected file from database and clear Redis cache
                    // Pass the original filename so we can match files by name (not just by blob storage ID)
                    await RemoveInfectedFileFromDatabaseAndCacheAsync(reference, applicationId, fileId, originalFileName, userId);

                    // CRITICAL: Create blacklist entries for ALL database record IDs, not just the blob storage ID
                    // This ensures the web app's FilterInfectedFilesFromList can find and filter these files
                    foreach (var dbRecordId in databaseRecordIds)
                    {
                        await CreateBlacklistEntryAsync(dbRecordId, originalFileName, applicationId!.Value);
                    }
                    
                    // Also blacklist the blob storage ID (in case it's used somewhere)
                    await CreateBlacklistEntryAsync(fileId, originalFileName, applicationId!.Value);

                    // FALLBACK: Also create a filename-based blacklist entry
                    // This handles the case where the file was already deleted from the database
                    // before we could look up its database record ID
                    await CreateFilenameBlacklistEntryAsync(originalFileName, applicationId!.Value);

                    // clean up collection flow sessions in Redis
                    // This ensures infected files are removed from FlowProgress_* session keys
                    await CleanupCollectionFlowSessionsAsync(applicationId ?? Guid.Empty, fileId, originalFileName);

                    // Create user notification about the infected file
                    await CreateMalwareNotificationAsync(
                        fileId,
                        applicationId ?? Guid.Empty,
                        originalFileName,
                        scanResult.MalwareName!,
                        new Guid(userId),
                        notificationContext);
                }

                logger.LogInformation(
                "Successfully processed infected file {FileId} ({FileName}) from application {ApplicationId}",
                fileId,
                scanResult.FileName,
                applicationId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Error handling infected file - FileName: {FileName}, FileId: {FileId}",
                    scanResult.FileName,
                    scanResult.FileId);
                
                // Re-throw to let MassTransit handle retry logic
                throw;
            }
        }

        /// <summary>
        /// Removes infected file from database and clears all Redis cache to force fresh data load
        /// </summary>
        private async Task RemoveInfectedFileFromDatabaseAndCacheAsync(string reference, Guid? applicationId, Guid fileId, string fileName, string userId)
        {
            try
            {
                logger.LogInformation(
                    "Cleaning infected file {FileId} from database for application reference {Reference}",
                    fileId,
                    reference);

                // Step 1: Get the application from database using reference
                var application = await applicationsClient.GetApplicationByReferenceAsync(reference);
                if (application == null)
                {
                    logger.LogWarning(
                        "Application with reference {Reference} not found for infected file {FileId}",
                        reference,
                        fileId);
                    return;
                }

                // Step 2: Check if there's response data to clean
                if (application.LatestResponse == null || string.IsNullOrEmpty(application.LatestResponse.ResponseBody))
                {
                    logger.LogInformation(
                        "No response data found for application {Reference}, skipping database cleanup",
                        reference);
                    
                    // Still clear cache and create blacklist even if no database data
                    await ClearRedisCacheForApplicationAsync(application.ApplicationId, fileId, fileName);
                    return;
                }

                string responseJson = application.LatestResponse.ResponseBody;

                // Step 4: Parse and clean the response JSON
                var responseData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(responseJson);
                if (responseData == null)
                {
                    logger.LogWarning("Failed to deserialize response data for application {Reference}", reference);
                    return;
                }

                bool dataModified = false;

                // Clean each field in the response
                foreach (var (fieldKey, fieldData) in responseData.ToList())
                {
                    if (fieldData.ValueKind != JsonValueKind.Object)
                        continue;

                    // Each field has { "question": "...", "value": "...", "completed": true/false, "dataType": "..." }
                    if (!fieldData.TryGetProperty("value", out var valueElement))
                        continue;

                    if (valueElement.ValueKind != JsonValueKind.String)
                        continue;

                    var valueStr = valueElement.GetString();
                    if (string.IsNullOrEmpty(valueStr))
                        continue;

                    try
                    {
                        // Try to parse as file list
                        var files = JsonSerializer.Deserialize<List<UploadDto>>(valueStr);
                        if (files != null)
                        {
                            // Check for infected files by BOTH file ID and original filename
                            // The fileId from the scan result is the blob storage ID, which doesn't match database record IDs
                            // So we also match by original filename to ensure we find and remove the infected file
                            var infectedFiles = files.Where(f => 
                                f.Id == fileId || 
                                string.Equals(f.OriginalFileName, fileName, StringComparison.OrdinalIgnoreCase)
                            ).ToList();
                            
                            if (infectedFiles.Any())
                            {
                                // Remove all matching infected files
                                foreach (var infectedFile in infectedFiles)
                                {
                                    files.Remove(infectedFile);
                                    logger.LogInformation(
                                        "Removed infected file {FileId} ({FileName}) from field {FieldKey} in application {Reference}",
                                        infectedFile.Id,
                                        infectedFile.OriginalFileName,
                                        fieldKey,
                                        reference);
                                }
                                
                                // Update the field, preserving question/dataType when already present
                                var updatedValueJson = JsonSerializer.Serialize(files);
                                var isCompleted = !string.IsNullOrWhiteSpace(updatedValueJson) && files.Count > 0;

                                var preservedQuestion = fieldData.TryGetProperty("question", out var questionElement)
                                    && questionElement.ValueKind == JsonValueKind.String
                                        ? questionElement.GetString()
                                        : null;
                                var preservedDataType = fieldData.TryGetProperty("dataType", out var dataTypeElement)
                                    && dataTypeElement.ValueKind == JsonValueKind.String
                                        ? dataTypeElement.GetString()
                                        : null;
                                JsonElement? preservedFields = fieldData.TryGetProperty("fields", out var fieldsElement)
                                    && fieldsElement.ValueKind == JsonValueKind.Object
                                        ? fieldsElement.Clone()
                                        : null;

                                if (preservedFields.HasValue)
                                {
                                    responseData[fieldKey] = JsonSerializer.SerializeToElement(new
                                    {
                                        question = preservedQuestion ?? string.Empty,
                                        value = updatedValueJson,
                                        completed = isCompleted,
                                        dataType = preservedDataType ?? "string",
                                        fields = preservedFields.Value
                                    });
                                }
                                else
                                {
                                    responseData[fieldKey] = JsonSerializer.SerializeToElement(new
                                    {
                                        question = preservedQuestion ?? string.Empty,
                                        value = updatedValueJson,
                                        completed = isCompleted,
                                        dataType = preservedDataType ?? "string"
                                    });
                                }

                                dataModified = true;
                            }
                        }
                    }
                    catch (JsonException)
                    {
                        // Not a file list, skip
                    }
                }

                // Step 5: If data was modified, save it back to the database
                if (dataModified)
                {
                    var cleanedResponseJson = JsonSerializer.Serialize(responseData);
                    var encodedResponse = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(cleanedResponseJson));

                    var request = new AddApplicationResponseRequest { ResponseBody = encodedResponse };

                    try
                    {
                        await applicationsClient.AddApplicationResponseAsync(application.ApplicationId, request);

                        logger.LogInformation(
                            "Successfully saved cleaned data to database for application {Reference}",
                            reference);
                    }
                    catch (ExternalApplicationsException ex) when (ex.StatusCode == 200)
                    {
                        logger.LogInformation(
                            "Successfully saved cleaned data to database for application {Reference} (200 response)",
                            reference);
                    }
                }
                else
                {
                    logger.LogInformation(
                        "Infected file {FileId} not found in application {Reference} response data",
                        fileId,
                        reference);
                }

                // Step 6: Clear ALL Redis cache keys for this application to force fresh load from cleaned DB
                await ClearRedisCacheForApplicationAsync(application.ApplicationId, fileId, fileName);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Error cleaning up infected file {FileId} from database for application reference {Reference}",
                    fileId,
                    reference);
                throw;
            }
        }

        /// <summary>
        /// Clears all Redis cache keys related to an application and creates a blacklist entry for the infected file.
        /// The blacklist ensures the file is filtered out everywhere it appears.
        /// </summary>
        private async Task ClearRedisCacheForApplicationAsync(Guid applicationId, Guid fileId, string fileName)
        {
            try
            {
                var db = redis.GetDatabase();
                var server = redis.GetServer(redis.GetEndPoints().First());

                // Clear cache keys
                var cacheKeys = server.Keys(pattern: $"{FlexFormsCacheKeys.RedisKeyPrefix}*{applicationId}*").ToList();

                logger.LogInformation(
                    "Found {Count} Redis cache key(s) to clear for application {ApplicationId}",
                    cacheKeys.Count,
                    applicationId);

                foreach (var key in cacheKeys)
                {
                    await db.KeyDeleteAsync(key);
                    logger.LogDebug("Deleted Redis cache key: {Key}", key);
                }

                // CRITICAL: Store the infected file ID in a blacklist for 24 hours
                // This ensures the file is filtered out EVERYWHERE it appears, even in cached data
                var infectedFileKey = $"{FlexFormsCacheKeys.InfectedFilePrefix}{fileId}";
                var infectedFileData = JsonSerializer.Serialize(new
                {
                    FileId = fileId,
                    FileName = fileName,
                    ApplicationId = applicationId,
                    MalwareName = "infected",
                    RemovedAt = DateTimeOffset.UtcNow.ToString("o")
                });
                await db.StringSetAsync(infectedFileKey, infectedFileData, TimeSpan.FromHours(24));
                
                logger.LogInformation(
                    "Successfully cleared {CacheCount} cache key(s) and created blacklist entry for infected file {FileId}",
                    cacheKeys.Count,
                    fileId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Error clearing Redis cache and sessions for application {ApplicationId}",
                    applicationId);
                // Don't re-throw - cache clearing failure shouldn't fail the entire process
            }
        }

        /// <summary>
        /// Finds database record IDs by matching the original filename for an application.
        /// The virus scanner returns a blob storage ID, but the web app uses database record IDs.
        /// This method bridges the gap by looking up files by their original filename.
        /// </summary>
        private async Task<List<Guid>> FindDatabaseRecordIdsByOriginalFileNameAsync(Guid applicationId, string? originalFileName)
        {
            var result = new List<Guid>();
            
            if (string.IsNullOrWhiteSpace(originalFileName))
            {
                logger.LogWarning("Cannot find database record IDs: originalFileName is empty");
                return result;
            }

            try
            {
                // Get all files for the application from the database
                var allFiles = await fileUploadService.GetFilesForApplicationAsync(applicationId);
                
                // Find files with matching original filename
                var matchingFiles = allFiles
                    .Where(f => string.Equals(f.OriginalFileName, originalFileName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                result = matchingFiles.Select(f => f.Id).ToList();

                if (!result.Any())
                {
                    logger.LogWarning(
                        "No files found with original filename '{OriginalFileName}' for application {ApplicationId}",
                        originalFileName,
                        applicationId);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Error finding database record IDs for original filename '{OriginalFileName}' in application {ApplicationId}",
                    originalFileName,
                    applicationId);
            }

            return result;
        }

        /// <summary>
        /// Creates a blacklist entry in Redis for an infected file.
        /// This allows the web app to filter out infected files before display or save.
        /// </summary>
        private async Task CreateBlacklistEntryAsync(Guid fileId, string? fileName, Guid applicationId)
        {
            try
            {
                var db = redis.GetDatabase();
                
                var infectedFileKey = $"{FlexFormsCacheKeys.InfectedFilePrefix}{fileId}";
                var infectedFileData = JsonSerializer.Serialize(new
                {
                    FileId = fileId,
                    FileName = fileName,
                    ApplicationId = applicationId,
                    MalwareName = "infected",
                    RemovedAt = DateTimeOffset.UtcNow.ToString("o")
                });
                
                await db.StringSetAsync(infectedFileKey, infectedFileData, TimeSpan.FromHours(24));
                
                logger.LogInformation(
                    "Created blacklist entry for infected file {FileId} ({FileName})",
                    fileId,
                    fileName);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Error creating blacklist entry for infected file {FileId}",
                    fileId);
                // Don't re-throw - blacklist failure shouldn't fail the entire process
            }
        }

        /// <summary>
        /// Creates a blacklist entry by original filename.
        /// This is a fallback when we can't determine the database record ID.
        /// The web app can filter files by checking if their original filename is blacklisted.
        /// </summary>
        private async Task CreateFilenameBlacklistEntryAsync(string originalFileName, Guid applicationId)
        {
            if (string.IsNullOrWhiteSpace(originalFileName))
            {
                logger.LogWarning("CreateFilenameBlacklistEntryAsync: originalFileName is null/empty, skipping");
                return;
            }

            try
            {
                var db = redis.GetDatabase();
                
                // Create a key based on filename + application ID
                // This allows the web app to filter by original filename
                var blacklistKey = $"{FlexFormsCacheKeys.InfectedFileNamePrefix}{applicationId}:{originalFileName}";
                var blacklistData = JsonSerializer.Serialize(new
                {
                    OriginalFileName = originalFileName,
                    ApplicationId = applicationId,
                    MalwareName = "infected",
                    RemovedAt = DateTimeOffset.UtcNow.ToString("o")
                });
                
                await db.StringSetAsync(blacklistKey, blacklistData, TimeSpan.FromHours(24));
                
                // Verify the key was created
                var keyExists = await db.KeyExistsAsync(blacklistKey);
                
                logger.LogWarning(
                    "BLACKLIST CREATED: Key='{BlacklistKey}', Verified={KeyExists}",
                    blacklistKey,
                    keyExists);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Error creating filename blacklist entry for '{OriginalFileName}'",
                    originalFileName);
            }
        }

        /// <summary>
        /// Clears infected files from all collection flow session data in Redis.
        /// This ensures infected files are removed from FlowProgress_* keys so they don't get re-saved.
        /// Matches by both file ID and original filename.
        /// </summary>
        private async Task CleanupCollectionFlowSessionsAsync(Guid applicationId, Guid fileId, string? originalFileName)
        {
            try
            {
                var db = redis.GetDatabase();
                var server = redis.GetServer(redis.GetEndPoints().First());

                // Find all FlowProgress session keys (collection flows)
                var flowProgressKeys = server.Keys(pattern: "*FlowProgress_*").ToList();

                logger.LogInformation(
                    "Found {Count} FlowProgress session key(s) to check for infected file {FileId} ({OriginalFileName})",
                    flowProgressKeys.Count,
                    fileId,
                    originalFileName);

                int cleanedCount = 0;

                foreach (var sessionKey in flowProgressKeys)
                {
                    var sessionData = await db.StringGetAsync(sessionKey);
                    if (sessionData.IsNullOrEmpty)
                        continue;

                    try
                    {
                        // Parse the flow progress data
                        var flowData = JsonSerializer.Deserialize<Dictionary<string, object>>((string)sessionData!);
                        if (flowData == null)
                            continue;

                        bool modified = false;

                        // Check each field in the flow progress
                        foreach (var fieldKey in flowData.Keys.ToList())
                        {
                            var fieldValue = flowData[fieldKey]?.ToString();
                            if (string.IsNullOrEmpty(fieldValue))
                                continue;

                            try
                            {
                                // Try to parse as file list
                                var files = JsonSerializer.Deserialize<List<UploadDto>>(fieldValue);
                                if (files != null)
                                {
                                    // Match by BOTH file ID and original filename
                                    var infectedFiles = files.Where(f => 
                                        f.Id == fileId || 
                                        string.Equals(f.OriginalFileName, originalFileName, StringComparison.OrdinalIgnoreCase)
                                    ).ToList();
                                    
                                    if (infectedFiles.Any())
                                    {
                                        // Remove all matching infected files
                                        foreach (var infectedFile in infectedFiles)
                                        {
                                            files.Remove(infectedFile);
                                            logger.LogInformation(
                                                "Removed infected file {FileId} ({FileName}) from field {FieldKey} in session {SessionKey}",
                                                infectedFile.Id,
                                                infectedFile.OriginalFileName,
                                                fieldKey,
                                                sessionKey);
                                        }
                                        
                                        // Update the field
                                        flowData[fieldKey] = JsonSerializer.Serialize(files);
                                        modified = true;
                                    }
                                }
                            }
                            catch (JsonException)
                            {
                                // Not a file list, skip
                            }
                        }

                        // Save back to Redis if modified
                        if (modified)
                        {
                            var updatedSessionData = JsonSerializer.Serialize(flowData);
                            await db.StringSetAsync(sessionKey, updatedSessionData);
                            cleanedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Error processing session key {SessionKey}", sessionKey);
                    }
                }

                logger.LogInformation(
                    "Cleaned infected file {FileId} from {Count} collection flow session(s)",
                    fileId,
                    cleanedCount);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Error cleaning collection flow sessions for infected file {FileId}",
                    fileId);
                // Don't re-throw - session cleanup failure shouldn't fail the entire process
            }
        }

        /// <summary>
        /// Resolves the notification context for a multi-tenant platform host.
        /// Prefer message metadata, then MassTransit tenant headers — never host APPLICATION_NAME.
        /// </summary>
        private static string ResolveNotificationContext(ScanResultEvent scanResult, ConsumeContext context)
        {
            if (scanResult.Metadata is not null)
            {
                foreach (var key in new[] { "ApplicationName", "TenantName", "applicationName", "tenantName" })
                {
                    if (scanResult.Metadata.TryGetValue(key, out var value))
                    {
                        var text = value?.ToString();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            return text;
                        }
                    }
                }
            }

            var tenantName = context.Headers.Get<string>("TenantName");
            if (!string.IsNullOrWhiteSpace(tenantName))
            {
                return tenantName!;
            }

            return "platform";
        }

        /// <summary>
        /// Creates a user notification about the infected file
        /// </summary>
        private async Task CreateMalwareNotificationAsync(
            Guid fileId,
            Guid applicationId,
            string? fileName,
            string malwareName,
            Guid? userId,
            string notificationContext)
        {
            try
            {
                var notification = new AddNotificationRequest
                {
                    Message = $"The selected file '{fileName}' contains a virus called [{malwareName}]. We have deleted the file. Upload a new one.",
                    Category = "malware-detection",
                    Context = notificationContext,
                    Type = NotificationType.Error,
                    AutoDismiss = false,
                    Metadata = new Dictionary<string, object>
                    {
                        ["fileId"] = fileId.ToString(),
                        ["fileName"] = fileName,
                        ["malwareName"] = malwareName,
                        ["applicationId"] = applicationId.ToString(),
                        ["detectedAt"] = DateTimeOffset.UtcNow.ToString("o")
                    },
                    UserId = userId
                };

                await notificationsClient.CreateNotificationAsync(notification);

                logger.LogInformation(
                    "Created malware notification for file {FileId} ({FileName})",
                    fileId,
                    fileName);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Error creating malware notification for file {FileId}",
                    fileId);
                // Don't re-throw - notification failure shouldn't fail the entire process
            }
        }

    }
}
