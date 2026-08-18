namespace GovUK.Dfe.FlexForms.Application.FormEngine;

/// <summary>
/// Session-backed in-progress data for a multi-collection flow instance.
/// </summary>
public interface ICollectionFlowProgressStore
{
    Dictionary<string, object> Load(string flowId, string instanceId);

    void Save(string flowId, string instanceId, Dictionary<string, object> latest);

    void SetField(string flowId, string instanceId, string fieldId, object value);

    void Clear(string flowId, string instanceId);
}
