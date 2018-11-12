using RozMap.Extensions;
using System;
using System.IO;
using System.Linq;

namespace RozMap
{
    public interface ISourceWriter
    {
        /// <summary>
        /// Just like it says, writes a blank line into the code being generated
        /// </summary>
        void BlankLine();

        /// <summary>
        /// Writes one or more lines into the code that respects the current block depth
        /// and handles text alignment for you. Also respects the "BLOCK" and
        /// "END"
        /// </summary>
        /// <param name="text"></param>
        void Write(string text = null);

        /// <summary>
        /// Writes a line with a closing '}' character at the current block level
        /// and decrements the current block level
        /// </summary>
        /// <param name="extra"></param>
        void FinishBlock(string extra = null);


        /// <summary>
        /// Writes a single line with this content to the code
        /// at the current block level
        /// </summary>
        /// <param name="text"></param>
        void WriteLine(string text);

        void BeginClass(string className, string interfaceName);

        void EndClass();
        
        void BeginMethod(string methodName, Type returnType, params (Type parameterType, string parameterName)[] parameters);
        
        void EndMethod();

        // Closes all remaining blocks
        void CompleteCode();
        string GetSourceCode();
    }

    public class SourceWriter : ISourceWriter
    {
        private readonly StringWriter _writer = new StringWriter();
        private string _leadingSpaces = "";
        private int _level;

        public SourceWriter(string @namespace)
        {
            WriteLine($"namespace {@namespace}");
            StartBlock();
        }

        public int IndentionLevel
        {
            get => _level;
            set
            {
                _level = value;
                _leadingSpaces = "".PadRight(_level*4);
            }
        }

        public void BlankLine()
        {
            _writer.WriteLine();
        }

        public void BeginClass(string className, string interfaceName)
        {
            WriteLine($"public class {className} : {interfaceName}");
            StartBlock();
        }

        public void EndClass()
        {
            FinishBlock();
        }

        public void BeginMethod(string methodName, Type returnType, params (Type parameterType, string parameterName)[] parameters)
        {
            var line = $"public {returnType.FullName} {methodName}(";

            if(parameters.Any())
            {
                for(var i = 0; i < parameters.Length; i++)
                {
                    var (parameterType, parameterName) = parameters[i];
                    line += $"{parameterType.FullName} {parameterName}";
                    if(i < parameters.Length-1)
                        _writer.Write(", ");
                }
            }

            line += ")";

            WriteLine(line);

            StartBlock();
        }

        public void EndMethod()
        {
            FinishBlock();
        }

        public void Write(string text)
        {
            if (text.IsNullOrEmpty())
            {
                BlankLine();
                return;
            }

            foreach(var line in text.ReadLines())
            {
                var cleanLine = line.Replace('`', '"');

                if(cleanLine.IsNullOrEmpty())
                {
                    BlankLine();
                }
                else if(cleanLine.StartsWith("BLOCK:"))
                {
                    WriteLine(line.Substring(6));
                    StartBlock();
                }
                else if(cleanLine.StartsWith("END"))
                {
                    FinishBlock(line.Substring(3));
                }
                else
                {
                    WriteLine(line);
                }
            }
        }

        public void WriteLine(string text)
        {
            _writer.WriteLine(_leadingSpaces + text);
        }

        private void StartBlock()
        {
            WriteLine("{");
            IndentionLevel++;
        }

        public void FinishBlock(string extra = null)
        {
            if (IndentionLevel == 0)
            {
                throw new InvalidOperationException("Not currently in a code block");
            }

            IndentionLevel--;

            if (extra.IsNullOrEmpty())
                WriteLine("}");
            else
                WriteLine("}" + extra);


            BlankLine();
        }

        public void CompleteCode()
        {
            if(IndentionLevel < 1)
                return;

            do
            {
                FinishBlock();
            } while(IndentionLevel > 0);
        }

        public string GetSourceCode()
        {
            return _writer.ToString();
        }

        internal class BlockMarker : IDisposable
        {
            private readonly SourceWriter _parent;

            public BlockMarker(SourceWriter parent)
            {
                _parent = parent;
            }

            public void Dispose()
            {
                _parent.FinishBlock();
            }
        }
    }
}