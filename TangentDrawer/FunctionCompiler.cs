using Microsoft.CodeDom.Providers.DotNetCompilerPlatform;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TangentDrawer
{
    public class FunctionCompiler
    {
        const string XCODE_PLACEHOLDER = "##Cx";
        const string YCODE_PLACEHOLDER = "##Cy";
        const string PREAMBLE_PLACEHOLDER = "##P";
        const string EXTENSIONS_PLACEHOLDER = "##E";

        class CustomReplacements
        {
            public static string toUpperStart(string input) => char.ToUpper(input[0]) + input.Substring(1);
        }

        static Regex parametricPattern = new Regex(@"^(?<x>[^\|]+)\|(?<y>.+)$");
        static Regex customReplacementPattern = new Regex(@"#(?<func>[a-zA-Z]+)#(?<arg>.+?)#");
        static Dictionary<string, string> replacements = new Dictionary<string, string>()
        {
            [@"²"] = "^2",
            [@"³"] = "^3",
            [@"(?<![.0-9])([0-9]+)(?![.0-9])"] = "{1}.0",
            [@"([0-9]+(?:\.[0-9]+)?)\s?([a-zA-Z][0-9a-zA-Z]*)"] = "{1}*{2}",
            [@"((?:[a-zA-Z][0-9a-zA-Z]*)?\((?:[^()]|(?<c>\()|(?<-c>\)))+(?(c)(?!))\)|[a-zA-Z][0-9a-zA-Z]*|[0-9]+(?:\.[0-9]+)?)\^(-?(?:[a-zA-Z][0-9a-zA-Z]*)?\((?:[^()]|(?<c>\()|(?<-c>\)))+(?(c)(?!))\)|-?[a-zA-Z][0-9a-zA-Z]*|-?[0-9]+(?:\.[0-9]+)?)"] = "Pow({1}, {2})",
            [@"(?<=^|[^0-9a-zA-Z])([a-z][a-zA-Z]+)"] = "#toUpperStart#{1}#"
        };

        // Author: Aaron Hudon
        // Source: http://stackoverflow.com/questions/31639602/using-c-sharp-6-features-with-codedomprovider-rosyln
        //
        static Lazy<CSharpCodeProvider> providerLazy { get; } = new Lazy<CSharpCodeProvider>(() => {
            var csc = new CSharpCodeProvider();
            var settings = csc
                .GetType()
                .GetField("_compilerSettings", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(csc);

            var path = settings
                .GetType()
                .GetField("_compilerFullPath", BindingFlags.Instance | BindingFlags.NonPublic);

            path.SetValue(settings, ((string)path.GetValue(settings)).Replace(@"bin\roslyn\", @"roslyn\"));

            return csc;
        });
        static CSharpCodeProvider provider => providerLazy.Value;

        static string codeEnv = File.ReadAllText("codeenv.txt");

        static string execRepl(string replName, string input)
        {
            Type t = typeof(CustomReplacements);
            MethodInfo m = t.GetMethod(replName);
            return (string)m.Invoke(null, new object[] { input });
        }

        static string ApplyReplacements(string function)
        {
            bool cont = true;
            while (cont)
            {
                cont = false;
                foreach (var kvp in replacements)
                {
                    Regex r = new Regex(kvp.Key);
                    string newFunction = r.Replace(function, m => String.Format(kvp.Value, m.Groups.Cast<Group>().Select(g => g.Value).ToArray()));
                    newFunction = customReplacementPattern.Replace(newFunction, m => execRepl(m.Groups["func"].Value, m.Groups["arg"].Value));
                    if (newFunction != function)
                    {
                        function = newFunction;
                        cont = true;
                    }
                }
            }

            return function;
        }

        public Func<float, Tuple<float, float>> CompileFunction(string function, string @class = "X.P", IEnumerable<string> extensionFiles = null, bool parametric = false)
        {
            extensionFiles = extensionFiles ?? Enumerable.Empty<string>();

            Console.WriteLine($"[Function Compiler, Input=\"{function}\"]");
            function = ApplyReplacements(function);
            Console.WriteLine($"Transformed to C#-Expression \"{function}\".");

            string xfunction = "x";
            string yfunction = function;
            string preamble = "";

            if (parametric)
            {
                Match pmatch = parametricPattern.Match(function);
                if (pmatch.Success)
                {
                    xfunction = pmatch.Groups["x"].Value;
                    yfunction = pmatch.Groups["y"].Value;
                }
                preamble = "float t = x;";
            }

            string code = codeEnv.Replace(XCODE_PLACEHOLDER, xfunction)
                                 .Replace(YCODE_PLACEHOLDER, yfunction)
                                 .Replace(PREAMBLE_PLACEHOLDER, preamble)
                                 .Replace(EXTENSIONS_PLACEHOLDER, string.Join("\n", extensionFiles.Select(f => File.ReadAllText(f))));

            CompilerParameters parameters = new CompilerParameters();
            parameters.GenerateExecutable = false;
            parameters.GenerateInMemory = true;
            CompilerResults results = provider.CompileAssemblyFromSource(parameters, code);
            if (results.Errors.HasErrors)
            {
                Console.WriteLine("[Syntax Error]");
                return null;
            }
            Assembly assembly = results.CompiledAssembly;
            Type program = assembly.GetType(@class);
            MethodInfo method = program.GetMethod("f");

            Console.WriteLine($"Compiled to assembly \"{assembly.FullName}\"");
            return (x => (Tuple<float, float>)method.Invoke(null, new object[] { x }));
        }

        public Func<float, Tuple<float, float>> LoadFunction(string function, string filename, string @class = "X.P", bool parametric = false)
        {
            Assembly assembly = Assembly.LoadFile(filename);
            Type program = assembly.GetType(@class);
            MethodInfo method = program.GetMethod(function);

            Console.WriteLine($"Loaded assembly \"{filename}\"");
            return (x => (Tuple<float, float>)method.Invoke(null, new object[] { x }));
        }
    }
}
