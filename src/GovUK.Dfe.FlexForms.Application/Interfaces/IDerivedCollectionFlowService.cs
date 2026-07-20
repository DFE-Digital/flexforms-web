using GovUK.Dfe.FlexForms.Domain.Models;

namespace GovUK.Dfe.FlexForms.Application.Interfaces
{
    /// <summary>
    /// Service for handling derived collection flows that generate forms based on other field values
    /// </summary>
    public interface IDerivedCollectionFlowService
    {
        /// <summary>
        /// Generates collection items based on values from a source field
        /// </summary>
        /// <param name="sourceFieldId">The field ID to derive items from</param>
        /// <param name="formData">The current form data containing source field values</param>
        /// <param name="config">The derived collection flow configuration</param>
        /// <returns>List of derived collection items</returns>
        List<DerivedCollectionItem> GenerateItemsFromSourceField(
            string sourceFieldId, 
            Dictionary<string, object> formData,
            DerivedCollectionFlowConfiguration config);
        
        /// <summary>
        /// Gets the status of each derived item (signed/not signed)
        /// </summary>
        /// <param name="fieldId">The field ID where statuses are stored</param>
        /// <param name="formData">The current form data</param>
        /// <returns>Dictionary mapping item IDs to their status</returns>
        Dictionary<string, string> GetItemStatuses(
            string fieldId, 
            Dictionary<string, object> formData);
            
        /// <summary>
        /// Gets existing declaration data for a specific item
        /// </summary>
        /// <param name="fieldId">The field ID where declarations are stored</param>
        /// <param name="itemId">The specific item ID</param>
        /// <param name="formData">The current form data</param>
        /// <returns>Declaration data for the item, or empty dictionary if not found</returns>
        Dictionary<string, object> GetItemDeclarationData(
            string fieldId, 
            string itemId, 
            Dictionary<string, object> formData);
            
        /// <summary>
        /// Saves declaration data and status for a specific item
        /// </summary>
        /// <param name="fieldId">The field ID where declarations are stored</param>
        /// <param name="itemId">The specific item ID</param>
        /// <param name="declarationData">The declaration form data</param>
        /// <param name="status">The status to set (e.g., "Signed")</param>
        /// <param name="formData">The current form data dictionary to update</param>
        void SaveItemDeclaration(
            string fieldId, 
            string itemId, 
            Dictionary<string, object> declarationData, 
            string status, 
            Dictionary<string, object> formData);
    }
}
