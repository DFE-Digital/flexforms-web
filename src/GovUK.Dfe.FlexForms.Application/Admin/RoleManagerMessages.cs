namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// User-facing copy for Role Manager. Keep these strings identical to the previous PageModel.
/// </summary>
public static class RoleManagerMessages
{
    public const string TemplateRequired = "Choose a role template.";

    public const string NameRequired = "Enter a role name.";

    public const string Deleted = "Role deleted.";

    public const string Renamed = "Role renamed.";

    public const string CreateFailed = "Could not create the role.";

    public const string CreateFromTemplateFailed = "Could not create the role from template.";

    public const string DeleteFailed = "Could not delete the role.";

    public const string RenameFailed = "Could not rename the role.";

    public const string LoadFailed = "Could not load roles for this tenant.";

    public static string Created(string name) =>
        $"Role '{name}' has been created.";

    public static string CreatedFromTemplate(string name, string templateKey) =>
        $"Role '{name}' has been created from the {templateKey} template.";
}
