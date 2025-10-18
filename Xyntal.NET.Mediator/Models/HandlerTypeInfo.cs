namespace Xyntal.NET.Mediator.Models;

internal class HandlerTypeInfo
{
	public Type Interface { get; set; } = null!;
	public Type Implementations { get; set; } = null!;
	public Type RequestType { get; set; } = null!;
	public Type ResponseType { get; set; } = null!;
}
