using System;

namespace RozMap.CodeGen
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
        void BeginConstructor(string className, params (string parameterFullTypeName, string parameterName)[] parameters);
        void EndConstructor();
    }
}