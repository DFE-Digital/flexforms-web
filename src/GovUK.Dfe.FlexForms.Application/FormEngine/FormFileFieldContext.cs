namespace GovUK.Dfe.FlexForms.Application.FormEngine;

public sealed record FormFileFieldContext(
    Guid? ApplicationId,
    string? FlowId,
    string? InstanceId)
{
    public bool IsCollectionFlow =>
        !string.IsNullOrEmpty(FlowId) && !string.IsNullOrEmpty(InstanceId);
}
