using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Web.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

namespace GovUK.Dfe.FlexForms.Web.Pages.FormEngine
{
    public class UploadFileModel(
        IFileUploadService fileUploadService,
        IApplicationResponseService applicationResponseService,
        INotificationsClient notificationsClient,
        IFormErrorStore formErrorStore)
        : PageModel
    {
        [BindProperty(SupportsGet = true)] public string ApplicationId { get; set; }
        [BindProperty(SupportsGet = true)] public string FieldId { get; set; }
        [BindProperty(SupportsGet = true, Name = "referenceNumber")] public string ReferenceNumber { get; set; }
        [BindProperty(SupportsGet = true, Name = "taskId")] public string TaskId { get; set; }
        [BindProperty(SupportsGet = true, Name = "pageId")] public string CurrentPageId { get; set; }
        [BindProperty] public string ReturnUrl { get; set; }
        [BindProperty] public string FlowId { get; set; } = string.Empty;
        [BindProperty] public string InstanceId { get; set; } = string.Empty;
        public IReadOnlyList<UploadDto> Files { get; set; } = new List<UploadDto>();
        public string SuccessMessage { get; set; }
        public string ErrorMessage { get; set; }
        
        private bool IsCollectionFlow => !string.IsNullOrEmpty(FlowId) && !string.IsNullOrEmpty(InstanceId);

        public async Task<IActionResult> OnGetAsync()
        {
            if (!Guid.TryParse(ApplicationId, out var appId))
                return NotFound();
            
            // Get only files for this specific field ID
            Files = await GetFilesForFieldAsync(appId, FieldId);
            return Page();
        }

        public async Task<IActionResult> OnPostUploadFileAsync()
        {
            // Debug: Check for validation errors


            
            // Clear validation errors for FlowId and InstanceId when not in collection flow
            if (!IsCollectionFlow)
            {
                ModelState.Remove("FlowId");
                ModelState.Remove("InstanceId");
            }
            


            
            var addRequest = new AddNotificationRequest
            {
                Message = string.Empty, // set later when known
                Category = "file-upload",
                Context = FieldId + "FileUpload",
                Type = NotificationType.Success,
                AutoDismiss = false,
                AutoDismissSeconds = 5
            };

            if (!Guid.TryParse(ApplicationId, out var appId))
                return NotFound();
            var file = Request.Form.Files["UploadFile"];
            var name = Request.Form["UploadName"].ToString();
            var description = Request.Form["UploadDescription"].ToString();
            if (file == null || file.Length == 0)
            {
                ErrorMessage = "Please select a file to upload.";
                ModelState.AddModelError("UploadFile", ErrorMessage);
                if (!string.IsNullOrEmpty(FieldId))
                {
                    // Persist field-level errors only to avoid duplicate summary lines
                    formErrorStore.Save(FieldId, ModelState);
                }

                // If we have a return URL, redirect back with error
                if (!string.IsNullOrEmpty(ReturnUrl))
                {
                    return Redirect(ReturnUrl);
                }
                
                Files = await GetFilesForFieldAsync(appId, FieldId);
                return Page();
            }

            using var stream = file.OpenReadStream();
            var fileParam = new FileParameter(stream, file.FileName, file.ContentType);
            var uploadedFile = await fileUploadService.UploadFileAsync(appId, file.FileName, description, fileParam);
            SuccessMessage = $"Your file '{file.FileName}' uploaded.";

            // Get the current files for this field
            var currentFieldFiles = (await GetFilesForFieldAsync(appId, FieldId)).ToList();

            if (!currentFieldFiles.Any(cf => cf.Id == uploadedFile.Id))
            {
                currentFieldFiles.Add(uploadedFile);
            }
            

            
            Files = currentFieldFiles.AsReadOnly();
            UpdateSessionFileList(appId, FieldId, Files);
            await SaveUploadedFilesToResponseAsync(appId, FieldId, Files);
            
            // If we have a return URL (from partial form), redirect back
            if (!string.IsNullOrEmpty(ReturnUrl))
            {
                addRequest.Message = SuccessMessage;
                await TryCreateFileNotificationAsync(addRequest);
                return Redirect(ReturnUrl);
            }

            return Page();
        }

        public override void OnPageHandlerExecuted(PageHandlerExecutedContext context)
        {
            base.OnPageHandlerExecuted(context);
            
            // If there are ModelState errors (from the filter), persist them via the error store
            if (!ModelState.IsValid && !string.IsNullOrEmpty(FieldId))
            {
                formErrorStore.Save(FieldId, ModelState);
                
                // If we have a return URL, redirect back with errors
                if (!string.IsNullOrEmpty(ReturnUrl))
                {
                    context.Result = new RedirectResult(ReturnUrl);
                }
            }
        }

        public async Task<IActionResult> OnPostDeleteFileAsync()
        {
            var addRequest = new AddNotificationRequest
            {
                Message = string.Empty,
                Category = "file-upload",
                Context = FieldId + "FileDeletion",
                Type = NotificationType.Success,
                AutoDismiss = false,
                AutoDismissSeconds = 5
            };

            if (!Guid.TryParse(ApplicationId, out var appId))
                return NotFound();
            var fileIdStr = Request.Form["FileId"].ToString();
            if (!Guid.TryParse(fileIdStr, out var fileId))
            {
                ErrorMessage = "Invalid file ID.";
                if (!string.IsNullOrEmpty(FieldId))
                {
                    formErrorStore.Save(FieldId, ModelState, ErrorMessage);
                }
                
                // If we have a return URL, redirect back with error
                if (!string.IsNullOrEmpty(ReturnUrl))
                {
                    return Redirect(ReturnUrl);
                }
                
                Files = await GetFilesForFieldAsync(appId, FieldId);
                return Page();
            }

            await fileUploadService.DeleteFileAsync(fileId, appId);
            SuccessMessage = "File deleted.";

            // Get current files for this field and remove the deleted one
            var currentFieldFiles = (await GetFilesForFieldAsync(appId, FieldId)).ToList();
            currentFieldFiles.RemoveAll(f => f.Id == fileId);
            
            Files = currentFieldFiles.AsReadOnly();
            UpdateSessionFileList(appId, FieldId, Files);
            await SaveUploadedFilesToResponseAsync(appId, FieldId, Files);
            
            // If we have a return URL (from partial form), redirect back
            if (!string.IsNullOrEmpty(ReturnUrl))
            {
                addRequest.Message = SuccessMessage;
                await TryCreateFileNotificationAsync(addRequest);
                return Redirect(ReturnUrl);
            }

            return Page();
        }

        public async Task<IActionResult> OnPostDownloadFileAsync()
        {
            if (!Guid.TryParse(ApplicationId, out var appId))
                return NotFound();
            var fileIdStr = Request.Form["FileId"].ToString();
            if (!Guid.TryParse(fileIdStr, out var fileId))
                return NotFound();

            var fileResponse = await fileUploadService.DownloadFileAsync(fileId, appId);

            // Extract content type
            var contentType = fileResponse.Headers.TryGetValue("Content-Type", out var ct)
                ? ct.FirstOrDefault()
                : "application/octet-stream";

            string fileName = "downloadedfile";
            if (fileResponse.Headers.TryGetValue("Content-Disposition", out var cd))
            {
                var disposition = cd.FirstOrDefault();
                if (!string.IsNullOrEmpty(disposition))
                {
                    var fileNameMatch = System.Text.RegularExpressions.Regex.Match(
                        disposition,
                        @"filename\*=UTF-8''(?<fileName>.+)|filename=""?(?<fileName>[^\"";]+)""?"
                    );
                    if (fileNameMatch.Success)
                        fileName = System.Net.WebUtility.UrlDecode(fileNameMatch.Groups["fileName"].Value);
                }
            }

            return File(fileResponse.Stream, contentType, fileName);
        }


        private void UpdateSessionFileList(Guid appId, string fieldId, IReadOnlyList<UploadDto> files)
        {

            
            if (IsCollectionFlow)
            {
                // For collection flows, store in flow progress system
                var progressKey = GetFlowProgressSessionKey(FlowId, InstanceId);

                
                // CRITICAL FIX: Try multiple sources to find existing flow data
                var existingProgress = LoadFlowProgress();

                
                // CRITICAL FIX: The 'files' parameter contains ALL files (existing + new), so just save it directly
                // No need to merge because GetFilesForFieldAsync already combined existing and new files
                var serializedFiles = JsonSerializer.Serialize(files);

                existingProgress[fieldId] = serializedFiles;
                
                // Force session to commit immediately
                var progressJson = JsonSerializer.Serialize(existingProgress);
                HttpContext.Session.SetString(progressKey, progressJson);
                

                
                // Flow progress saved successfully

            }
            else
            {
                // For regular forms, use the original session key
                var key = $"UploadedFiles_{appId}_{fieldId}";

                HttpContext.Session.SetString(key, JsonSerializer.Serialize(files));
            }
        }

        private async Task SaveUploadedFilesToResponseAsync(Guid appId, string fieldId, IReadOnlyList<UploadDto> files)
        {
            if (string.IsNullOrEmpty(fieldId))
            {
                return;
            }

            if (IsCollectionFlow)
            {
                // For collection flows, files are saved via flow progress system
                // This happens in UpdateSessionFileList, no need to save to main application response here
                return;
            }

            var json = JsonSerializer.Serialize(files);
            var data = new Dictionary<string, object> { { fieldId, json } };

            await applicationResponseService.SaveApplicationResponseAsync(appId, data, HttpContext.Session);
        }

        /// <summary>
        /// Gets files for a specific field ID by filtering from existing session data first,
        /// then cross-referencing with database files to ensure we only get files for this field
        /// </summary>
        private async Task<IReadOnlyList<UploadDto>> GetFilesForFieldAsync(Guid appId, string fieldId)
        {
            if (string.IsNullOrEmpty(fieldId))
            {
                return new List<UploadDto>().AsReadOnly();
            }

            string? sessionFilesJson = null;

            if (IsCollectionFlow)
            {
                // For collection flows, get files from flow progress system

                var progressData = LoadFlowProgress();

                
                if (progressData.TryGetValue(fieldId, out var progressValue))
                {
                    sessionFilesJson = progressValue?.ToString();

                }
                else
                {
                    // CRITICAL FIX: Flow progress not found, initialize from database if possible

                    
                    // 1. Check if any files exist in database for this application
                    // Note: Since UploadDto doesn't have FieldId, we'll rely on session data for field association
                    try
                    {
                        var allDbFiles = await fileUploadService.GetFilesForApplicationAsync(appId);

                        
                        // For now, we can't filter by field ID since UploadDto doesn't have that property
                        // We'll rely on session data to maintain field-specific file associations

                    }
                    catch (Exception ex)
                    {

                    }
                    
                    // 2. If still no files, check accumulated form data
                    if (string.IsNullOrWhiteSpace(sessionFilesJson))
                    {
                        var alternativeAccumulatedData = applicationResponseService.GetAccumulatedFormData(HttpContext.Session);
                        if (alternativeAccumulatedData.TryGetValue(fieldId, out var accFieldValue))
                        {
                            sessionFilesJson = accFieldValue?.ToString();

                        }
                    }
                    
                    // 3. If still not found, search all session keys for this field data
                    if (string.IsNullOrWhiteSpace(sessionFilesJson))
                    {

                        foreach (var sessionKey in HttpContext.Session.Keys)
                        {
                            var keyValue = HttpContext.Session.GetString(sessionKey);
                            if (!string.IsNullOrWhiteSpace(keyValue))
                            {
                                // Check if this key contains our field data
                                if (sessionKey.Contains(fieldId, StringComparison.OrdinalIgnoreCase) ||
                                    (keyValue.StartsWith("[") && keyValue.Contains("\"id\"") && keyValue.Contains(fieldId)))
                                {

                                    sessionFilesJson = keyValue;
                                    break;
                                }
                                
                                // Also check if the key contains flow progress for our specific flow
                                if (sessionKey.Contains($"FlowProgress_{FlowId}") && keyValue.Contains(fieldId))
                                {

                                    try
                                    {
                                        var flowData = JsonSerializer.Deserialize<Dictionary<string, object>>(keyValue);
                                        if (flowData != null && flowData.TryGetValue(fieldId, out var fieldData))
                                        {
                                            sessionFilesJson = fieldData?.ToString();

                                            break;
                                        }
                                    }
                                    catch (Exception ex)
                                    {

                                    }
                                }
                            }
                        }
                    }
                }
                

            }
            else
            {
                // For regular forms, get files from session
                var sessionKey = $"UploadedFiles_{appId}_{fieldId}";
                sessionFilesJson = HttpContext.Session.GetString(sessionKey);
            }
            
            if (!string.IsNullOrEmpty(sessionFilesJson))
            {
                try
                {
                    var sessionFiles = JsonSerializer.Deserialize<List<UploadDto>>(sessionFilesJson);
                    if (sessionFiles != null)
                    {
                        // Cross-reference with database to make sure files still exist
                        var validSessionFiles = await FilterFilesAgainstDatabaseAsync(appId, sessionFiles);
                        
                        return validSessionFiles.AsReadOnly();
                    }
                }
                catch (JsonException)
                {
                    // Session data is corrupted, fall through to check accumulated data
                }
            }

            // If no session data, try to get from accumulated form data (for existing applications)
            var accumulatedData = applicationResponseService.GetAccumulatedFormData(HttpContext.Session);
            if (accumulatedData.TryGetValue(fieldId, out var fieldValue))
            {
                var fieldValueStr = fieldValue?.ToString();
                if (!string.IsNullOrEmpty(fieldValueStr))
                {
                    try
                    {
                        var existingFiles = JsonSerializer.Deserialize<List<UploadDto>>(fieldValueStr);
                        if (existingFiles != null)
                        {
                            // Cross-reference with database to make sure files still exist
                            var validFiles = await FilterFilesAgainstDatabaseAsync(appId, existingFiles);
                            
                            return validFiles.AsReadOnly();
                        }
                    }
                    catch (JsonException)
                    {
                        // Data is corrupted, return empty list
                    }
                }
            }

            // If no existing data for this field, return empty list
            // Don't return all database files, as that would include files from other fields
            return new List<UploadDto>().AsReadOnly();
        }

        private async Task TryCreateFileNotificationAsync(AddNotificationRequest addRequest)
        {
            try
            {
                await notificationsClient.CreateNotificationAsync(addRequest);
            }
            catch
            {
                // Upload/delete succeeded; notification is optional when user lacks notification permissions
            }
        }

        private async Task<List<UploadDto>> FilterFilesAgainstDatabaseAsync(Guid appId, List<UploadDto> files)
        {
            try
            {
                var allDbFiles = await fileUploadService.GetFilesForApplicationAsync(appId);
                return files.Where(sf => allDbFiles.Any(dbf => dbf.Id == sf.Id)).ToList();
            }
            catch (ExternalApplicationsException ex) when (ex.StatusCode is 401 or 403)
            {
                // User may have write but not read permission; trust session data
                return files;
            }
        }

        // legacy method removed in favour of IFormErrorStore

        /// <summary>
        /// Helper methods for collection flow support
        /// </summary>
        private static string GetFlowProgressSessionKey(string flowId, string instanceId) => $"FlowProgress_{flowId}_{instanceId}";

        private Dictionary<string, object> LoadFlowProgress()
        {
            if (!IsCollectionFlow)
            {

                return new Dictionary<string, object>();
            }

            var key = GetFlowProgressSessionKey(FlowId, InstanceId);

            
            // Debug: List all session keys to see what's actually in the session


            
            // Try to get all session keys

                var sessionKeys = new List<string>();
                foreach (var sessionKey in HttpContext.Session.Keys)
                {
                    sessionKeys.Add(sessionKey);
                }

            
            var json = HttpContext.Session.GetString(key);

            
            if (string.IsNullOrWhiteSpace(json))
            {

                return new Dictionary<string, object>();
            }

            try
            {
                var result = JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new Dictionary<string, object>();

                return result;
            }
            catch (Exception ex)
            {

                return new Dictionary<string, object>();
            }
        }
    }
}