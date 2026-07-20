using System.ComponentModel;

namespace GovUK.Dfe.FlexForms.Domain.Models;

public enum TaskStatus
{
    /// <summary>
    /// Task has not been started - no fields have been completed
    /// </summary>
    [Description("Not started")]
    NotStarted = 0,

    /// <summary>
    /// Task is in progress - some but not all required fields have been completed
    /// </summary>
    [Description("In progress")]
    InProgress = 1,

    /// <summary>
    /// Task is completed - all required fields have been completed
    /// </summary>
    [Description("Completed")]
    Completed = 2,
    
    /// <summary>
    /// Task cannot be completed - validation errors or missing dependencies
    /// </summary>
    [Description("Cannot start yet")]
    CannotStartYet = 3
} 