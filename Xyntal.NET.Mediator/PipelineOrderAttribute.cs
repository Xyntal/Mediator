namespace Xyntal.NET.Mediator;

[AttributeUsage(AttributeTargets.Class)]
public class PipelineOrderAttribute(int order) : Attribute
{
    public int Order { get; } = order;
}
