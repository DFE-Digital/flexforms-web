namespace GovUK.Dfe.FlexForms.Application.Interfaces
{
    /// <summary>
    /// Service for formatting and processing confirmation display data
    /// </summary>
    public interface IConfirmationDataService
    {
        /// <summary>
        /// Formats form data for display on the confirmation page
        /// </summary>
        /// <param name="formData">The raw form data</param>
        /// <param name="displayFields">The fields to include in the display</param>
        /// <returns>A dictionary of display-friendly field names and values</returns>
        Dictionary<string, string> FormatDisplayData(Dictionary<string, object> formData, string[] displayFields);

        /// <summary>
        /// Gets a user-friendly display name for a field
        /// </summary>
        /// <param name="fieldName">The field name</param>
        /// <returns>A user-friendly display name</returns>
        string GetFieldDisplayName(string fieldName);

        /// <summary>
        /// Formats a field value for display
        /// </summary>
        /// <param name="fieldName">The field name</param>
        /// <param name="value">The field value</param>
        /// <returns>A formatted display value</returns>
        string FormatFieldValue(string fieldName, object value);
    }
}

