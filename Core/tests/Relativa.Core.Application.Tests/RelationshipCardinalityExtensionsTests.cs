using FluentAssertions;
using Relativa.Persistence.Entities;
using Xunit;

namespace Relativa.Core.Application.Tests;

public sealed class RelationshipCardinalityExtensionsTests
{
    [Theory]
    [InlineData(RelationshipCardinality.OneToOne, "one_to_one")]
    [InlineData(RelationshipCardinality.OneToMany, "one_to_many")]
    [InlineData(RelationshipCardinality.ManyToOne, "many_to_one")]
    [InlineData(RelationshipCardinality.ManyToMany, "many_to_many")]
    public void ToDatabaseValue_AndParse_RoundTripEveryCardinality(RelationshipCardinality value, string dbValue)
    {
        value.ToDatabaseValue().Should().Be(dbValue);
        RelationshipCardinalityExtensions.ParseDatabaseValue(dbValue).Should().Be(value);
    }

    [Fact]
    public void ParseDatabaseValue_UnknownString_DefaultsToManyToOne()
    {
        RelationshipCardinalityExtensions.ParseDatabaseValue("garbage")
            .Should().Be(RelationshipCardinality.ManyToOne, "an unrecognized stored value must fall back to the safe many-to-one default");
    }

    [Fact]
    public void ToDatabaseValue_UndefinedEnumValue_Throws()
    {
        var act = () => ((RelationshipCardinality)99).ToDatabaseValue();

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
