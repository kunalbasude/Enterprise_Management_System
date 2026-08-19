using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using EnterpriseManagement.Domain.Common;

namespace EnterpriseManagement.Tests.Architecture;

/// <summary>
/// Guards that the Domain layer stays persistence-ignorant.
/// </summary>
/// <remarks>
/// The assembly-reference tests cannot catch this: DataAnnotations lives in the
/// base class library, so decorating an entity with <c>[Table]</c> or
/// <c>[Required]</c> adds no assembly reference and compiles happily. Mapping
/// belongs in Infrastructure's Fluent API configurations, where it can be
/// changed without touching the domain model.
/// </remarks>
public class DomainPurityTests
{
    private static readonly Type[] EntityTypes = Domain.AssemblyReference.Assembly
        .GetTypes()
        .Where(t => t.IsClass && !t.IsAbstract && typeof(BaseEntity).IsAssignableFrom(t))
        .ToArray();

    private static readonly Type[] ForbiddenAttributes =
    [
        typeof(TableAttribute),
        typeof(ColumnAttribute),
        typeof(KeyAttribute),
        typeof(RequiredAttribute),
        typeof(MaxLengthAttribute),
        typeof(StringLengthAttribute),
        typeof(ForeignKeyAttribute),
        typeof(NotMappedAttribute),
        typeof(DatabaseGeneratedAttribute)
    ];

    [Fact]
    public void Entities_are_discovered()
    {
        // Guards the guard: a reflection test that silently matches nothing
        // passes forever and protects nothing.
        Assert.True(EntityTypes.Length >= 8,
            $"Expected at least 8 entity types, found {EntityTypes.Length}.");
    }

    [Fact]
    public void Entities_should_not_use_persistence_attributes()
    {
        var violations = new List<string>();

        foreach (var type in EntityTypes)
        {
            foreach (var attribute in type.GetCustomAttributes())
            {
                if (ForbiddenAttributes.Contains(attribute.GetType()))
                {
                    violations.Add($"{type.Name} (type) uses [{attribute.GetType().Name}]");
                }
            }

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                foreach (var attribute in property.GetCustomAttributes())
                {
                    if (ForbiddenAttributes.Contains(attribute.GetType()))
                    {
                        violations.Add($"{type.Name}.{property.Name} uses [{attribute.GetType().Name}]");
                    }
                }
            }
        }

        Assert.True(violations.Count == 0,
            "Mapping belongs in Infrastructure Fluent API configurations, not the domain model:"
            + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void Auditable_entities_should_expose_settable_timestamps()
    {
        // SaveChanges sets these by reflection-free assignment through the
        // interface; a private setter would break it at runtime, not compile time.
        var auditable = EntityTypes.Where(t => typeof(IAuditableEntity).IsAssignableFrom(t));

        foreach (var type in auditable)
        {
            var createdAt = type.GetProperty(nameof(IAuditableEntity.CreatedAt));
            var updatedAt = type.GetProperty(nameof(IAuditableEntity.UpdatedAt));

            Assert.True(createdAt?.CanWrite, $"{type.Name}.CreatedAt must be settable.");
            Assert.True(updatedAt?.CanWrite, $"{type.Name}.UpdatedAt must be settable.");
        }
    }
}
