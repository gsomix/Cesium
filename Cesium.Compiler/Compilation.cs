using System.Text;
using Cesium.CodeGen;
using Cesium.CodeGen.Contexts;
using Cesium.CodeGen.Extensions;
using Cesium.Core;
using Cesium.Parser;
using Cesium.Preprocessor;
using Mono.Cecil;
using Yoakke.Streams;
using Yoakke.SynKit.C.Syntax;

namespace Cesium.Compiler;

internal static class Compilation
{
    public static async Task<int> Compile(
        IEnumerable<string> inputFilePaths,
        string outputFilePath,
        CompilationOptions compilationOptions)
    {
        Console.WriteLine($"Generating assembly {outputFilePath}.");
        var assemblyContext = CreateAssembly(outputFilePath, compilationOptions);

        foreach (var inputFilePath in inputFilePaths)
        {
            Console.WriteLine($"Processing input file \"{inputFilePath}\".");
            await GenerateCode(assemblyContext, inputFilePath);
        }

        SaveAssembly(assemblyContext, compilationOptions.TargetRuntime.Kind, outputFilePath);

        return 0;
    }

    private static AssemblyContext CreateAssembly(string outputFilePath, CompilationOptions compilationOptions)
    {
        var assemblyName = Path.GetFileNameWithoutExtension(outputFilePath);
        return AssemblyContext.Create(
            new AssemblyNameDefinition(assemblyName, new Version()),
            compilationOptions);
    }

    private static Task<string> Preprocess(string compilationFileDirectory, TextReader reader)
    {
        var currentProcessPath = Path.GetDirectoryName(Environment.ProcessPath)
                                 ?? throw new Exception("Cannot determine path to the compiler executable.");

        var stdLibDirectory = Path.Combine(currentProcessPath, "stdlib");
        var includeContext = new FileSystemIncludeContext(stdLibDirectory, compilationFileDirectory);
        var preprocessorLexer = new CPreprocessorLexer(reader);
        var definesContext = new InMemoryDefinesContext();
        var preprocessor = new CPreprocessor(preprocessorLexer, includeContext, definesContext);
        return preprocessor.ProcessSource();
    }

    private static async Task GenerateCode(AssemblyContext context, string inputFilePath)
    {
        var compilationFileDirectory = Path.GetDirectoryName(inputFilePath)!;

        await using var input = new FileStream(inputFilePath, FileMode.Open);
        using var reader = new StreamReader(input, Encoding.UTF8);

        var content = await Preprocess(compilationFileDirectory, reader);
        var lexer = new CLexer(content);
        var parser = new CParser(lexer);
        var translationUnitParseError = parser.ParseTranslationUnit();
        if (translationUnitParseError.IsError)
        {
            switch (translationUnitParseError.Error.Got)
            {
                case CToken token:
                    throw new ParseException($"Error during parsing {inputFilePath}. Error at position {translationUnitParseError.Error.Position}. Got {token.LogicalText}.");
                case char ch:
                    throw new ParseException($"Error during parsing {inputFilePath}. Error at position {translationUnitParseError.Error.Position}. Got {ch}.");
                default:
                    throw new ParseException($"Error during parsing {inputFilePath}. Error at position {translationUnitParseError.Error.Position}.");
            }
        }

        var translationUnit = translationUnitParseError.Ok.Value;

        if (parser.TokenStream.Peek().Kind != CTokenType.End)
            throw new ParseException($"Excessive output after the end of a translation unit {inputFilePath} at {lexer.Position}.");

        context.EmitTranslationUnit(translationUnit.ToIntermediate());
    }

    private static void SaveAssembly(
        AssemblyContext context,
        SystemAssemblyKind targetFrameworkKind,
        string outputFilePath)
    {
        context.VerifyAndGetAssembly().Write(outputFilePath);

        // This part should go to Cesium.SDK eventually together with
        // runtimeconfig.json generation
        var compilerRuntime = Path.Combine(AppContext.BaseDirectory, "Cesium.Runtime.dll");
        var outputExecutablePath = Path.GetDirectoryName(outputFilePath) ?? Environment.CurrentDirectory;
        var applicationRuntime = Path.Combine(outputExecutablePath, "Cesium.Runtime.dll");
        File.Copy(compilerRuntime, applicationRuntime, true);

        if (context.Module.Kind == ModuleKind.Console && targetFrameworkKind == SystemAssemblyKind.SystemRuntime)
        {
            var runtimeConfigFilePath = Path.ChangeExtension(outputFilePath, "runtimeconfig.json");
            Console.WriteLine($"Generating a .NET 6 runtime config at {runtimeConfigFilePath}.");
            File.WriteAllText(runtimeConfigFilePath, RuntimeConfig.EmitNet6());
        }
    }
}
