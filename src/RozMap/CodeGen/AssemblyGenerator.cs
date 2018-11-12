using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RozMap.Extensions;

namespace RozMap.CodeGen
{
    public class AssemblyGenerator
    {
        private readonly string _assemblyName;
        private readonly IList<MetadataReference> _references =new List<MetadataReference>();
        private readonly IList<Assembly> _assemblies = new List<Assembly>();

        public AssemblyGenerator(string assemblyName)
        {
            _assemblyName = assemblyName;
        }

        public string[] HintPaths { get; set; }

        public void ReferenceAssembly(Assembly assembly)
        {
            if(assembly == null)
                return;

            if(_assemblies.Contains(assembly))
                return;

            _assemblies.Add(assembly);

            try
            {
                var referencePath = CreateAssemblyReference(assembly);

                if(referencePath == null)
                {
                    Console.WriteLine($"Could not make an assembly reference to {assembly.FullName}");
                    return;
                }

                var alreadyReferenced = _references.Any(x => x.Display == referencePath);
                if(alreadyReferenced)
                    return;

                var reference = MetadataReference.CreateFromFile(referencePath);

                _references.Add(reference);

                foreach(var assemblyName in assembly.GetReferencedAssemblies())
                {
                    var referencedAssembly = Assembly.Load(assemblyName);
                    ReferenceAssembly(referencedAssembly);
                }
            }
            catch(Exception e)
            {
                Console.WriteLine($"Could not make an assembly reference to {assembly.FullName}\n\n{e}");
            }
        }

        public void ReferenceAssemblyContainingType<T>()
        {
            ReferenceAssemblyContainingType(typeof(T));
        }
        public void ReferenceAssemblyContainingType(Type t)
        {
            ReferenceAssembly(t.GetTypeInfo().Assembly);
        }

        public Assembly Generate(string code)
        {
            var assemblyName = _assemblyName ?? Path.GetRandomFileName();
            var syntaxTree = CSharpSyntaxTree.ParseText(code);

            var references = _references.ToArray();
            var compilation = CSharpCompilation.Create(assemblyName, new[] { syntaxTree }, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));


            using(var stream = new MemoryStream())
            {
                var result = compilation.Emit(stream);
                if(!result.Success)
                {
                    var failures = result.Diagnostics.Where(diagnostic =>
                                                                diagnostic.IsWarningAsError ||
                                                                diagnostic.Severity == DiagnosticSeverity.Error);


                    var message = failures.Select(x => $"{x.Id}: {x.GetMessage()}").Join("\n");


                    throw new InvalidOperationException("Compilation failures!\n\n" + message + "\n\nCode:\n\n" + code);
                }

                stream.Seek(0, SeekOrigin.Begin);

                return Assembly.Load(stream.ToArray());
            }
        }
        private string CreateAssemblyReference(Assembly assembly)
        {
            if(assembly.IsDynamic)
                return null;

            return string.IsNullOrEmpty(assembly.Location)
                ? GetPath(assembly)
                : assembly.Location;
        }

        private string GetPath(Assembly assembly)
        {
            return HintPaths?
                   .Select(FindFile(assembly))
                   .FirstOrDefault(file => file.IsNotNullOrEmpty());
        }

        private static Func<string, string> FindFile(Assembly assembly)
        {
            return hintPath =>
                   {
                       var name = assembly.GetName().Name;
                       Console.WriteLine($"Find {name}.dll in {hintPath}");
                       var files = Directory.GetFiles(hintPath, name + ".dll", SearchOption.AllDirectories);
                       var firstOrDefault = files.FirstOrDefault();
                       if(firstOrDefault != null)
                       {
                           Console.WriteLine($"Found {name}.dll in {firstOrDefault}");
                       }

                       return firstOrDefault;
                   };
        }

    }
}