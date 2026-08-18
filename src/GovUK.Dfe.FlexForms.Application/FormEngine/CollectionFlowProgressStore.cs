using System.Text.Json;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Caching;

namespace GovUK.Dfe.FlexForms.Application.FormEngine;

public sealed class CollectionFlowProgressStore(IFormSessionStore sessionStore) : ICollectionFlowProgressStore
{
    public Dictionary<string, object> Load(string flowId, string instanceId)
    {
        if (string.IsNullOrEmpty(flowId) || string.IsNullOrEmpty(instanceId))
            return new Dictionary<string, object>();

        var json = sessionStore.GetString(FormSessionKeys.FlowProgress(flowId, instanceId));
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, object>();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(json)
                   ?? new Dictionary<string, object>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, object>();
        }
    }

    public void Save(string flowId, string instanceId, Dictionary<string, object> latest)
    {
        if (string.IsNullOrEmpty(flowId) || string.IsNullOrEmpty(instanceId))
            return;

        var existing = Load(flowId, instanceId);
        foreach (var kv in latest)
            existing[kv.Key] = kv.Value;

        sessionStore.SetString(FormSessionKeys.FlowProgress(flowId, instanceId), JsonSerializer.Serialize(existing));
    }

    public void SetField(string flowId, string instanceId, string fieldId, object value)
    {
        if (string.IsNullOrEmpty(flowId) || string.IsNullOrEmpty(instanceId) || string.IsNullOrEmpty(fieldId))
            return;

        var existing = Load(flowId, instanceId);
        existing[fieldId] = value;
        sessionStore.SetString(FormSessionKeys.FlowProgress(flowId, instanceId), JsonSerializer.Serialize(existing));
    }

    public void Clear(string flowId, string instanceId)
    {
        if (string.IsNullOrEmpty(flowId) || string.IsNullOrEmpty(instanceId))
            return;

        sessionStore.Remove(FormSessionKeys.FlowProgress(flowId, instanceId));
    }
}
