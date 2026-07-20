//using GovUK.Dfe.FlexForms.Domain.Models;
//using Microsoft.AspNetCore.Mvc.ModelBinding;

//namespace GovUK.Dfe.FlexForms.Application.Interfaces;

///// <summary>
///// Handles mapping of API errors to model state validation errors
///// </summary>
//public interface IModelStateErrorHandler
//{
//    /// <summary>
//    /// Adds API validation errors to the model state with proper field mapping
//    /// </summary>
//    /// <param name="modelState">The model state to add errors to</param>
//    /// <param name="apiError">The parsed API error response</param>
//    /// <param name="fieldMappings">Optional mapping of API field names to model property names</param>
//    void AddApiErrorsToModelState(ModelStateDictionary modelState, ApiErrorResponse apiError, 
//        Dictionary<string, string>? fieldMappings = null);

//    /// <summary>
//    /// Adds a general error message to the model state
//    /// </summary>
//    /// <param name="modelState">The model state to add the error to</param>
//    /// <param name="errorMessage">The error message to add</param>
//    void AddGeneralError(ModelStateDictionary modelState, string errorMessage);
//} 