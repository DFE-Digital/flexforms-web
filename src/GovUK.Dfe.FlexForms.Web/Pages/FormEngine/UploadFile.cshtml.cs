using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Application.FormEngine;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Application.Notifications;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Web.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GovUK.Dfe.FlexForms.Web.Pages.FormEngine
{
    public class UploadFileModel(
        IFileUploadService fileUploadService,
        IApplicationResponseService applicationResponseService,
        IFormFileFieldService formFileFieldService,
        INotificationsClient notificationsClient,
        IFormErrorStore formErrorStore,
        IRequestAppConfiguration requestConfiguration)
        : PageModel
    {
        private string ApplicationContext =>
            requestConfiguration["ApplicationName"]
            ?? requestConfiguration["TenantName"]
            ?? throw new InvalidOperationException(
                "ApplicationName (or TenantName) is required in tenant configuration for notifications.");
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
                AutoDismissSeconds = 5,
                ReplaceExistingContext = false
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

            // Get current files for this field and add the new one
            var currentFieldFiles = (await GetFilesForFieldAsync(appId, FieldId)).ToList();
            currentFieldFiles.Add(uploadedFile);

            Files = currentFieldFiles.AsReadOnly();
            UpdateSessionFileList(appId, FieldId, Files);
            await SaveUploadedFilesToResponseAsync(appId, FieldId, Files);

            // If we have a return URL (from partial form), redirect back
            if (!string.IsNullOrEmpty(ReturnUrl))
            {
                addRequest.Message = SuccessMessage;
                addRequest.Context = $"file-upload|{uploadedFile.Id}";
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
                Type = NotificationType.Success,
                AutoDismiss = false,
                AutoDismissSeconds = 5,
                ReplaceExistingContext = false
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
                addRequest.Context = $"file-delete|{fileId}";
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


        private void UpdateSessionFileList(Guid appId, string fieldId, IReadOnlyList<UploadDto> files) =>
            formFileFieldService.SaveFiles(new FormFileFieldContext(appId, FlowId, InstanceId), fieldId, files);

        /// <summary>
        /// Gets files for a specific field, then drops any that no longer exist in the database.
        /// </summary>
        private async Task<IReadOnlyList<UploadDto>> GetFilesForFieldAsync(Guid appId, string fieldId)
        {
            var files = formFileFieldService.GetFiles(new FormFileFieldContext(appId, FlowId, InstanceId), fieldId).ToList();
            var validFiles = await FilterFilesAgainstDatabaseAsync(appId, files);
            return validFiles.AsReadOnly();
        }

        private async Task TryCreateFileNotificationAsync(AddNotificationRequest addRequest)
        {
            try
            {
                addRequest.Context = NotificationScopeContext.PrefixDetail(ApplicationContext, addRequest.Context);
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

        private async Task SaveUploadedFilesToResponseAsync(Guid appId, string fieldId, IReadOnlyList<UploadDto> files)
        {
            if (string.IsNullOrEmpty(fieldId) || IsCollectionFlow)
                return;

            var json = System.Text.Json.JsonSerializer.Serialize(files);
            var data = new Dictionary<string, object> { { fieldId, json } };
            await applicationResponseService.SaveApplicationResponseAsync(appId, data);
        }
    }
}