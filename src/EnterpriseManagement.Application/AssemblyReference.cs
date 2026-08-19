namespace EnterpriseManagement.Application;

/// <summary>
/// Stable anchor type for assembly scanning (architecture tests, validator and
/// DI registration). Never contains logic.
/// </summary>
public static class AssemblyReference
{
    /// <summary>The <see cref="System.Reflection.Assembly"/> for this layer.</summary>
    public static readonly System.Reflection.Assembly Assembly = typeof(AssemblyReference).Assembly;
}
