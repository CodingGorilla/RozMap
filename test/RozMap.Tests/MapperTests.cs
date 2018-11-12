using System;
using FluentAssertions;
using Xunit;

namespace RozMap.Tests
{
    public class MapperTests
    {
        [Fact]
        public void Should_Be_Able_To_Map_Simple_Class()
        {
            var config = new MapperConfiguration();
            var mapper = new Mapper(config);
            
            var source = new SimpleSource { TestProperty = 777 };
            var dest = mapper.Map<SimpleSource, SimpleDest>(source);

            dest.Should().NotBeNull();
            dest.TestProperty.Should().Be(source.TestProperty);
        }
    }

    public class SimpleSource
    {
        public int TestProperty { get; set; }
    }

    public class SimpleDest
    {
        public int TestProperty { get; set; }
    }
}
