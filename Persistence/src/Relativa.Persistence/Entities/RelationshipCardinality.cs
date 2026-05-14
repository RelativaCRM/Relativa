using System;

namespace Relativa.Persistence.Entities;

/// <summary>
/// Stored in <c>entity_relationship_type.relationship_cardinality</c> as snake_case strings.
/// <see cref="ManyToOne"/> is 0 so new CLR instances default correctly.
/// </summary>
public enum RelationshipCardinality
{
    ManyToOne = 0,
    OneToMany = 1,
    OneToOne = 2,
    ManyToMany = 3,
}

public static class RelationshipCardinalityExtensions
{
    public static string ToDatabaseValue(this RelationshipCardinality value) =>
        value switch
        {
            RelationshipCardinality.OneToOne => "one_to_one",
            RelationshipCardinality.OneToMany => "one_to_many",
            RelationshipCardinality.ManyToOne => "many_to_one",
            RelationshipCardinality.ManyToMany => "many_to_many",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
        };

    public static RelationshipCardinality ParseDatabaseValue(string value) =>
        value switch
        {
            "one_to_one" => RelationshipCardinality.OneToOne,
            "one_to_many" => RelationshipCardinality.OneToMany,
            "many_to_one" => RelationshipCardinality.ManyToOne,
            "many_to_many" => RelationshipCardinality.ManyToMany,
            _ => RelationshipCardinality.ManyToOne,
        };
}
