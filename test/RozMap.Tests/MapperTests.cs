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

        [Fact]
        public void Should_Be_Able_To_Map_More_Complex_Class()
        {
            var config = new MapperConfiguration();
            var mapper = new Mapper(config);

            var sourceProp = new SimpleSource { TestProperty = 777 };
            var source = new ComplexSource { TestProperty = 888, SimpleProperty = sourceProp };

            var dest = mapper.Map<ComplexSource, ComplexDest>(source);

            dest.Should().NotBeNull();
            dest.TestProperty.Should().Be(888);
            dest.SimpleProperty.Should().BeEquivalentTo(new SimpleDest { TestProperty = 777 });
        }

        [Fact]
        public void Should_Be_Able_To_Map_Object_With_Mathcing_Non_Primitive_Members()
        {
            var config = new MapperConfiguration();
            var mapper = new Mapper(config);

            var sourceProp = new SimpleSource { TestProperty = 777 };
            var source = new NestedSource { TestProperty = 888, SimpleProperty = sourceProp };

            var dest = mapper.Map<NestedSource, NestedDest>(source);

            dest.Should().NotBeNull();
            dest.TestProperty.Should().Be(888);
            dest.SimpleProperty.Should().BeEquivalentTo(new SimpleDest { TestProperty = 777 });
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

    public class ComplexSource
    {
        public int TestProperty { get; set; }
        public SimpleSource SimpleProperty { get; set; }
    }

    public class ComplexDest
    {
        public int TestProperty { get; set; }
        public SimpleDest SimpleProperty { get; set; }
    }

    public class NestedSource
    {
        public int TestProperty { get; set; }
        public SimpleSource SimpleProperty { get; set; }
    }

    public class NestedDest
    {
        public int TestProperty { get; set; }
        public SimpleSource SimpleProperty { get; set; }
    }
}
