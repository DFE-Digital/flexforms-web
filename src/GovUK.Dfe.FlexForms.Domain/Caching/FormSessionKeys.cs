namespace GovUK.Dfe.FlexForms.Domain.Caching;

/// <summary>
/// HTTP-session key names used by the form engine.
/// Keep these stable; existing in-flight applications depend on them.
/// </summary>
public static class FormSessionKeys
{
    public const string AccumulatedFormData = "AccumulatedFormData";
    public const string ApplicationId = "ApplicationId";
    public const string ApplicationReference = "ApplicationReference";
    public const string TemplateId = "TemplateId";
    public const string CurrentAccumulatedApplicationId = "CurrentAccumulatedApplicationId";
    public const string NavHistoryPrefix = "NavHistory_";

    public static string FlowProgress(string flowId, string instanceId) =>
        $"FlowProgress_{flowId}_{instanceId}";

    public static string FlowItemExisted(string flowId, string instanceId) =>
        $"FlowItemExisted_{flowId}_{instanceId}";

    public static string UploadedFiles(Guid applicationId, string fieldId) =>
        $"UploadedFiles_{applicationId}_{fieldId}";
}
