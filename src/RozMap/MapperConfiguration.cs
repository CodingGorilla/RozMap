using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RozMap.CodeGen;
using RozMap.Extensions;

namespace RozMap
{
    public class MapperConfiguration
    {
        public IPerformInstanceMapping GetMapperFor(Type sourceType, Type destType)
        {
            var constructors = destType.GetConstructors(BindingFlags.Public);
            if(constructors.Length > 0 && !constructors.Any(c => c.GetParameters().Length < 1))
                throw new NotSupportedException("Cannot map to a type without a parameterless constructor");

            var sourceProperties = GetPropertyNamesForType(sourceType);
            var destProperties = GetPropertyNamesForType(destType);
            var matchingProperties = GetMatchingProperties(sourceProperties, destProperties);

            var mapperClassName = $"{sourceType.SanitizedName()}_{destType.SanitizedName()}_Mapper";

            var sourceWriter = new SourceWriter("RozMap.Mappers.Generated");
            sourceWriter.BeginClass(mapperClassName, "IPerformInstanceMapping");

            WriteMapInstanceMethod(sourceWriter, sourceType.FullName, destType.FullName, matchingProperties);

            sourceWriter.CompleteCode();

            var mapperAssembly = GenerateMappersAssembly(sourceWriter, new[] { sourceType, destType });
            
            return GetMapperFromAssembly(mapperAssembly, mapperClassName);
        }

        private static IPerformInstanceMapping GetMapperFromAssembly(Assembly mapperAssembly, string mapperClassName)
        {
            var mapperType = mapperAssembly.GetType($"RozMap.Mappers.Generated.{mapperClassName}");
            if(mapperType == null)
                throw new Exception("Unable to load the mapper from the generated assembly");

            var mapper = Activator.CreateInstance(mapperType);
            return (IPerformInstanceMapping)mapper;
        }

        private static Assembly GenerateMappersAssembly(ISourceWriter sourceWriter, IEnumerable<Type> referenceTypes)
        {
            var assemblyGenerator = new AssemblyGenerator("RozMap.Mappers.Generated");

            foreach(var type in referenceTypes)
            {
                assemblyGenerator.ReferenceAssemblyContainingType(type);
            }

            assemblyGenerator.ReferenceAssemblyContainingType<IPerformInstanceMapping>();

            var sourceCode = sourceWriter.GetSourceCode();
            System.Diagnostics.Debug.WriteLine(sourceCode);

            var mapperAssembly = assemblyGenerator.Generate(sourceCode);
            return mapperAssembly;
        }

        private static void WriteMapInstanceMethod(ISourceWriter sourceWriter, string sourceTypeFullName, string destTypeFullName,
                                                   IEnumerable<PropertyInfo> propertiesToMap)
        {
            sourceWriter.BeginMethod("MapInstance", typeof(object), (typeof(object), "sourceInstance"));

            sourceWriter.Write($"var typedInstance = sourceInstance as {sourceTypeFullName};");
            sourceWriter.Write("BLOCK:if(typedInstance == null)");
            sourceWriter.Write("throw new System.InvalidCastException(\"The specified instance could not be cast to the proper type\");");
            sourceWriter.Write("END");

            sourceWriter.WriteLine($"var destInstance = new {destTypeFullName}();");

            foreach(var property in propertiesToMap)
            {
                sourceWriter.WriteLine($"destInstance.{property.Name} = typedInstance.{property.Name};");
            }

            sourceWriter.WriteLine("return destInstance;");
        }

        private static IEnumerable<PropertyInfo> GetPropertyNamesForType(Type type)
        {
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                 .Where(pi => pi.CanRead && pi.CanWrite)
                                 .ToArray();
            return properties;
        }

        private static IEnumerable<PropertyInfo> GetMatchingProperties(IEnumerable<PropertyInfo> sourcePropertyNames,
                                                                       IEnumerable<PropertyInfo> destPropertyNames)
        {
            return sourcePropertyNames.Intersect(destPropertyNames, new PropertyComparer()).ToArray();
        }

        private class PropertyComparer : IEqualityComparer<PropertyInfo>
        {
            public bool Equals(PropertyInfo x, PropertyInfo y)
            {
                if(x == null || y == null)
                    return false;

                return x.Name.Equals(y.Name, StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode(PropertyInfo obj)
            {
                return obj.Name.GetHashCode();
            }
        }
    }
}