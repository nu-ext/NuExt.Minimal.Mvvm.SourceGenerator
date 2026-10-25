namespace Minimal.Mvvm;

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
internal sealed class CommandAttribute : Attribute
{
    public CommandAttribute()
    {
    }

    public CommandAttribute(string propertyName)
    {
        PropertyName = propertyName;
    }

    public string PropertyName { get; set; } = null!;
    public AccessModifier Getter { get; set; }
    public AccessModifier Setter { get; set; }
}