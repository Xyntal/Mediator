namespace Xyntal.NET.Mediator.Models;

public readonly struct Void : IEquatable<Void>, IComparable<Void>, IComparable
{
    private static readonly Void _value = new();
    public static ref readonly Void Value => ref _value;
    public static Task<Void> Task => System.Threading.Tasks.Task.FromResult(_value);

    public int CompareTo(Void other) => 0;
    public int CompareTo(object obj) => 0;
    public bool Equals(Void other) => true;
}
