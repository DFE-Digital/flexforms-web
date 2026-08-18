using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.FormEngine;

namespace GovUK.Dfe.FlexForms.Infrastructure.Services
{
    /// <summary>
    /// Application adapter over <see cref="FormStepPolicy"/>.
    /// </summary>
    public class FormStateManager : IFormStateManager
    {
        public FormState GetCurrentState(string referenceNumber, string taskId, string pageId)
        {
            if (FormStepPolicy.IsCollectionFlowPage(pageId))
                return FormState.SubFlowPage;

            if (FormStepPolicy.IsFormPage(pageId))
                return FormState.FormPage;

            if (FormStepPolicy.IsTaskSummary(taskId, pageId))
                return FormState.TaskSummary;

            return FormState.TaskList;
        }

        public bool ShouldShowTaskList(string pageId) => string.IsNullOrEmpty(pageId);

        public bool ShouldShowTaskSummary(string taskId, string pageId) =>
            FormStepPolicy.IsTaskSummary(taskId, pageId);

        public bool ShouldShowApplicationPreview(string pageId) =>
            FormStepPolicy.IsApplicationPreview(pageId);

        public bool ShouldShowCollectionFlowSummary(Domain.Models.Task task) =>
            FormStepPolicy.IsCollectionFlowSummary(task);

        public bool ShouldShowDerivedCollectionFlowSummary(Domain.Models.Task task) =>
            FormStepPolicy.IsDerivedCollectionFlowSummary(task);

        public bool IsInSubFlow(string flowId, string pageId) =>
            FormStepPolicy.IsInSubFlow(flowId, pageId);
    }
}
