using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RozMap.CodeGen;
using RozMap.Extensions;

namespace RozMap
{
    internal class MapperBuilder
    {
        private readonly MapperConfiguration _parent;
        private readonly Dictionary<string, string> _mapperDependencies = new Dictionary<string, string>();

        public MapperBuilder(Type sourceType, Type destType, MapperConfiguration parent)
        {
            SourceType = sourceType;
            DestType = destType;
            _parent = parent;
            MapperClassName = MakeMapperClassName(sourceType, destType);
        }

        internal static string MakeMapperClassName(Type sourceType, Type destType)
        {
            return $"{sourceType.SanitizedName()}_{destType.SanitizedName()}_Mapper";
        }

        public string MapperClassName { get; }
        public string MapperTypeName => $"{MapperConfiguration.GeneratedNamespace}.{MapperClassName}";
        public Type SourceType { get; }
        public Type DestType { get; }

        public string[] DependencyTypeNames => _mapperDependencies.Values.ToArray();

        public void WriteMapperCode(ISourceWriter sourceWriter)
        {
            var sourceProperties = GetPropertyNamesForType(SourceType);
            var destProperties = GetPropertyNamesForType(DestType);
            var matchingProperties = GetMatchingProperties(sourceProperties, destProperties);

            var mapperClassName = $"{SourceType.SanitizedName()}_{DestType.SanitizedName()}_Mapper";

            sourceWriter.BeginClass(mapperClassName, "IPerformInstanceMapping");

            WriteMapInstanceMethod(sourceWriter, SourceType.FullName, DestType.FullName, matchingProperties);

            WriteMapperConstructor(sourceWriter, mapperClassName);

            sourceWriter.EndClass();
        }

        private void WriteMapperConstructor(ISourceWriter sourceWriter, string mapperClassName)
        {
            if(!_mapperDependencies.Any())
                return;

            var constructorParameters = _mapperDependencies.Select(kvp => (mapperTypeName:kvp.Value, parameterName:kvp.Key)).ToArray();
            sourceWriter.BeginConstructor(mapperClassName, constructorParameters);

            foreach(var (_, parameterName) in constructorParameters)
            {
                sourceWriter.WriteLine($"this.{parameterName} = {parameterName};");
            }

            sourceWriter.EndConstructor();

            // Write the dependency fields
            foreach(var (parameterTypeName, parameterName) in constructorParameters)
            {
                sourceWriter.WriteLine($"private readonly {parameterTypeName} {parameterName};");
            }
        }

        private void WriteMapInstanceMethod(ISourceWriter sourceWriter, string sourceTypeFullName, string destTypeFullName,
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
                WriteMappingForProperty(sourceWriter, property);
            }

            sourceWriter.WriteLine("return destInstance;");
            sourceWriter.EndMethod();
        }

        private void WriteMappingForProperty(ISourceWriter sourceWriter, PropertyInfo destProperty)
        {
            var propertyName = destProperty.Name;

            if(destProperty.IsPrimitive())
                sourceWriter.WriteLine($"destInstance.{propertyName} = typedInstance.{propertyName};");
            else
            {
                var sourceProperty = SourceType.GetProperty(propertyName);
                if(sourceProperty == null)
                    throw new Exception("Huh??");

                var dependencyFieldName = AddRequiredMapper(sourceWriter, sourceProperty.PropertyType, destProperty.PropertyType);
                sourceWriter.WriteLine(
                    $"destInstance.{propertyName} = ({destProperty.PropertyType.FullName})this.{dependencyFieldName}.MapInstance(typedInstance.{propertyName});");
            }
        }

        private string AddRequiredMapper(ISourceWriter sourceWriter, Type sourceType, Type destType)
        {
            var paramName = $"{sourceType.SanitizedName()}_to_{destType.SanitizedName()}_mapper";

            if(_mapperDependencies.ContainsKey(paramName))
                return paramName;

            var mapperFullTypeName = _parent.BuildDependentMapper(sourceType, destType);
            _mapperDependencies.Add(paramName, mapperFullTypeName);
            return paramName;
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
            return destPropertyNames.Intersect(sourcePropertyNames, new PropertyComparer()).ToArray();
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

    public class MapperConfiguration
    {
        private readonly Dictionary<string, MapperBuilder> _pendingConstruction = new Dictionary<string, MapperBuilder>();
        private readonly Dictionary<string, MapperBuilder> _completedBuilders = new Dictionary<string, MapperBuilder>();
        private readonly List<Type> _referenceTypes = new List<Type>();

        public static string GeneratedNamespace { get; } = "RozMap.Mappers.Generated";

        public IPerformInstanceMapping GetMapperFor(Type sourceType, Type destType)
        {
            if(!CanBeConstructed(destType))
                throw new NotSupportedException("Cannot map to a type without a parameterless constructor");

            _referenceTypes.Add(sourceType);
            _referenceTypes.Add(destType);

            var sourceWriter = new SourceWriter(GeneratedNamespace);

            var mapperBuilder = CreateBuilder(sourceType, destType);
            mapperBuilder.WriteMapperCode(sourceWriter);

            CompleteBuilder(mapperBuilder);

            foreach(var kvp in _pendingConstruction)
            {
                var builder = kvp.Value;
                builder.WriteMapperCode(sourceWriter);
                _referenceTypes.Add(builder.SourceType);
                _referenceTypes.Add(builder.DestType);
                _completedBuilders.Add(kvp.Key, builder);
            }

            _pendingConstruction.Clear();

            sourceWriter.CompleteCode(); // This should close the namespace

            var mapperAssembly = GenerateMappersAssembly(sourceWriter);

            return GetMapperFromAssembly(mapperAssembly, mapperBuilder.MapperTypeName);
        }

        internal string BuildDependentMapper(Type sourceType, Type destType)
        {
            var dependentMapperClassName = MapperBuilder.MakeMapperClassName(sourceType, destType);
            if(_completedBuilders.ContainsKey(dependentMapperClassName))
                return $"{GeneratedNamespace}.{dependentMapperClassName}";

            var mapperBuilder = CreateBuilder(sourceType, destType);

            return $"{GeneratedNamespace}.{mapperBuilder.MapperClassName}";
        }

        private MapperBuilder CreateBuilder(Type sourceType, Type destType)
        {
            var mapperBuilder = new MapperBuilder(sourceType, destType, this);
            if(_pendingConstruction.ContainsKey(mapperBuilder.MapperClassName))
                throw new InvalidOperationException("Circular dependency detected");

            _pendingConstruction.Add(mapperBuilder.MapperTypeName, mapperBuilder);
            return mapperBuilder;
        }

        private void CompleteBuilder(MapperBuilder builder)
        {
            _pendingConstruction.Remove(builder.MapperTypeName);
            _completedBuilders.Add(builder.MapperTypeName, builder);
        }

        private IPerformInstanceMapping GetMapperFromAssembly(Assembly mapperAssembly, string mapperTypeName)
        {
            var builder = _completedBuilders[mapperTypeName];

            var dependencyTypeNames = builder.DependencyTypeNames;
            var dependencies = dependencyTypeNames.Select(y => GetMapperFromAssembly(mapperAssembly, y)).Cast<object>().ToArray();

            var mapperType = GetTypeFromName(mapperAssembly, mapperTypeName);
            if(mapperType == null)
                throw new Exception("Unable to load the mapper from the generated assembly");

            object mapper;
            if(!dependencies.Any())
                mapper = Activator.CreateInstance(mapperType);
            else
                mapper = ConstructMapperWithDependencies(mapperType, dependencies);

            return (IPerformInstanceMapping)mapper;
        }

        private static IPerformInstanceMapping ConstructMapperWithDependencies(Type mapperType, object[] dependencies)
        {
            var constructor = mapperType.GetConstructors().Single();
            if(constructor.GetParameters().Length != dependencies.Length)
                throw new InvalidOperationException("The number of required constructor parameters does not match the number of provided dependencies");

            return (IPerformInstanceMapping)constructor.Invoke(dependencies);
        }

        private Type GetTypeFromName(Assembly assembly, string typeFullName)
        {
            return assembly.GetType(typeFullName);
        }

        private Assembly GenerateMappersAssembly(ISourceWriter sourceWriter)
        {
            var assemblyGenerator = new AssemblyGenerator(GeneratedNamespace);

            foreach(var type in _referenceTypes)
            {
                assemblyGenerator.ReferenceAssemblyContainingType(type);
            }

            assemblyGenerator.ReferenceAssemblyContainingType<IPerformInstanceMapping>();

            var sourceCode = sourceWriter.GetSourceCode();
            System.Diagnostics.Debug.WriteLine(sourceCode);

            var mapperAssembly = assemblyGenerator.Generate(sourceCode);
            return mapperAssembly;
        }

        private static bool CanBeConstructed(Type type)
        {
            var constructors = type.GetConstructors(BindingFlags.Public);
            return constructors.Length <= 0 || constructors.Any(c => c.GetParameters().Length < 1);
        }
    }

    internal class MapperDependency
    {
        public MapperDependency(Type sourceType, Type destType)
        {
            SourceType = sourceType;
            DestType = destType;
        }

        public Type SourceType { get; }
        public Type DestType { get; }

        public Type MapperType { get; set; }
        public bool IsResolved => MapperType != null;

        protected bool Equals(MapperDependency other)
        {
            return SourceType == other.SourceType && DestType == other.DestType;
        }

        public override bool Equals(object obj)
        {
            if(ReferenceEquals(null, obj))
                return false;
            if(ReferenceEquals(this, obj))
                return true;
            if(obj.GetType() != GetType())
                return false;
            return Equals((MapperDependency)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((SourceType != null ? SourceType.GetHashCode() : 0) * 397) ^ (DestType != null ? DestType.GetHashCode() : 0);
            }
        }

        public static bool operator ==(MapperDependency left, MapperDependency right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(MapperDependency left, MapperDependency right)
        {
            return !Equals(left, right);
        }
    }
}