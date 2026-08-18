using GovUK.Dfe.FlexForms.Domain.Models;

namespace GovUK.Dfe.FlexForms.Application.FormEngine;

public static class FormEngineConstants
{
    public const string UploadFieldSessionPlaceholder = "UPLOAD_FIELD_SESSION_DATA";

    public const string CurrentAccumulatedApplicationIdWriteKey = "CurrentAccumulatedApplicationId";

    public static FormTemplate CreateDummyTemplate() =>
        new()
        {
            TemplateId = "dummy",
            TemplateName = "dummy",
            Description = "dummy",
            TaskGroups = []
        };
}
