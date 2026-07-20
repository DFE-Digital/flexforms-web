namespace GovUK.Dfe.FlexForms.Application.Interfaces
{
    public interface IComplexFieldRendererFactory
    {
        IComplexFieldRenderer GetRenderer(string fieldType);
    }
} 