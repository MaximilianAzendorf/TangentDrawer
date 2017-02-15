#define NO_DEBUG_INTERFACE

using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static System.Math;
using Microsoft.CodeDom.Providers.DotNetCompilerPlatform;
using System.Text.RegularExpressions;
using System.IO;
using Fclp;
using System.Drawing;
using System.Drawing.Imaging;

namespace TangentDrawer
{
    class Program
    {
        public class Args
        {
            private static readonly Regex SizeRegex = new Regex(@"^\s*(?<w>[0-9]+)x(?<h>[0-9]+)\s*$");
            private static readonly Regex IntervalRegex = new Regex(@"^\s*\[(?<a>\-?[0-9]+(\.[0-9]+)?),\s*(?<b>\-?[0-9]+(\.[0-9]+)?)\]\s*$");

            public string InputFunction { get; set; }
            public string BrightnessFunction { get; set; }
            public string OutputFile { get; set; }
            public string OutputSize { get; set; }
            public string FrameSize { get; set; }
            public string XInterval { get; set; }
            public string YInterval { get; set; }
            public string TInterval { get; set; }
            public int MaxIterations { get; set; }
            public bool Parametric { get; set; }
            public bool OmitSteepTangents { get; set; }
            public double DerivativeOffset { get; set; }
            public List<string> ExtensionFiles { get; set; }

            public int Width, Height;
            public int FrameWidth, FrameHeight;
            public float XMin, XMax, YMin, YMax, TMin, TMax;

            public void ParseArgs()
            {
                var sizeMatch = SizeRegex.Match(OutputSize);
                var fsizeMatch = SizeRegex.Match(FrameSize ?? "");
                var xIntMatch = IntervalRegex.Match(XInterval);
                var yIntMatch = IntervalRegex.Match(YInterval);
                var tIntMatch = IntervalRegex.Match(TInterval);

                if (!sizeMatch.Success)
                {
                    throw new FormatException($"the output size argument \"{OutputSize}\" has an unknown format.");
                }
                if (FrameSize != null && !sizeMatch.Success)
                {
                    throw new FormatException($"the frame size argument \"{FrameSize}\" has an unknown format.");
                }

                Width = int.Parse(sizeMatch.Groups["w"].Value, System.Globalization.CultureInfo.InvariantCulture);
                Height = int.Parse(sizeMatch.Groups["h"].Value, System.Globalization.CultureInfo.InvariantCulture);
                FrameWidth = FrameSize == null ? Width : int.Parse(fsizeMatch.Groups["w"].Value, System.Globalization.CultureInfo.InvariantCulture);
                FrameHeight = FrameSize == null ? Height : int.Parse(fsizeMatch.Groups["h"].Value, System.Globalization.CultureInfo.InvariantCulture);
                XMin = float.Parse(xIntMatch.Groups["a"].Value, System.Globalization.CultureInfo.InvariantCulture);
                XMax = float.Parse(xIntMatch.Groups["b"].Value, System.Globalization.CultureInfo.InvariantCulture);
                YMin = float.Parse(yIntMatch.Groups["a"].Value, System.Globalization.CultureInfo.InvariantCulture);
                YMax = float.Parse(yIntMatch.Groups["b"].Value, System.Globalization.CultureInfo.InvariantCulture);
                TMin = float.Parse(tIntMatch.Groups["a"].Value, System.Globalization.CultureInfo.InvariantCulture);
                TMax = float.Parse(tIntMatch.Groups["b"].Value, System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        static int Main(string[] _args)
        {
            #region debug_arg_interface
#if DEBUG && !NO_DEBUG_INTERFACE
            Console.WriteLine("==SHITTY DEBUG INTERFACE==");
            string input = Console.ReadLine();
            string[] extensions = { };

            List<string> _argsl = new List<string>();
            _argsl.Add("-f");
            _argsl.Add($"\"{input}\"");
            _argsl.Add("-o");
            _argsl.Add("output.png");
            _argsl.Add("-n");
            _argsl.Add("100000");
            if ((extensions?.Length ?? 0) > 0)
            {
                _argsl.Add("-e");
                _argsl.AddRange(extensions.Select(s => $"\"{s}\""));
            }
            _args = _argsl.ToArray();
            Console.Clear();
#endif
            #endregion

            var fclp = new FluentCommandLineParser<Args>();
            fclp.SetupHelp("?", "h", "help")
                .Callback(text => Console.WriteLine(text));
            fclp.Setup(arg => arg.InputFunction)
                .As('f', "function")
                .Required()
                .WithDescription("The term containing the variable \'x\' (or \'t\' if --parametric is set) whose tangents will be rendered. If --parametric is set, the format \"[term for x]|[term for y]\" is expected.");
            fclp.Setup(arg => arg.BrightnessFunction)
                .As('b', "brightness")
                .SetDefault(null)
                .WithDescription("A term containing the variable \'x\' that defines the relative opacity of a tangent");
            fclp.Setup(arg => arg.OutputFile)
                .As('o', "output")
                .Required()
                .WithDescription("The output file path.");
            fclp.Setup(arg => arg.OutputSize)
                .As('s', "output-size")
                .SetDefault("500x500")
                .WithDescription("The dimensions of the output file. The expected format is \"[Width]x[Height]\". The default ist \"500x500\".");
            fclp.Setup(arg => arg.FrameSize)
                .As('r', "frame-size")
                .SetDefault(null)
                .WithDescription("The dimensions of the render target. The x- and y intervals will be mapped to this \"frame\" which is placed centered at the output file. If this argument is not present, the frame will be the whole output file.");
            fclp.Setup(arg => arg.XInterval)
                .As('x', "x-interval")
                .SetDefault("[-1,1]")
                .WithDescription("The interval for x in which the function will be rendered. If --parametric is not set, this is also the interval out of which the random sample points for the function will be drawn. The expected format is \"[[From],[To]]\", the default is \"[-1,1]\".");
            fclp.Setup(arg => arg.YInterval)
                .As('y', "y-interval")
                .SetDefault("[-1,1]")
                .WithDescription("The interval for y in which the function will be rendered. The expected format is \"[[From],[To]]\", the default is \"[-1,1]\".");
            fclp.Setup(arg => arg.TInterval)
                .As('t', "t-interval")
                .SetDefault("[0,1]")
                .WithDescription("The interval for t out of which the random sample values for t for the parametric function will be drawn. If --parametric is not set, this argument has no effect. The expected format is \"[[From],[To]]\", the default is \"[0,1]\".");
            fclp.Setup(arg => arg.ExtensionFiles)
                .As('e', "extensions")
                .SetDefault(new List<string>())
                .WithDescription("A comma separated list of C# code files, these will be placed in the generated code (according to the file \"codeenv.txt\") and will also be compiled, so things which are defined there will be usable in the -f and -b arguments.");
            fclp.Setup(arg => arg.MaxIterations)
                .As('n', "max-iterations")
                .SetDefault(-1)
                .WithDescription("The maximum number of iterations/tangents that will be calculated. The default is -1 (so the rendering will continue until you press Ctrl+C)");
            fclp.Setup(arg => arg.Parametric)
                .As('p', "parametric")
                .SetDefault(false)
                .WithDescription("Set this flag if you want to render a parametric curve instead of a function.");
            fclp.Setup(arg => arg.DerivativeOffset)
                .As('d', "derivative-offset")
                .SetDefault(0.00001)
                .WithDescription("The difference betwenn the two sample points which will be used to calculate the tangent slope. The default is \"0.00001\".");
            fclp.Setup(arg => arg.OmitSteepTangents)
                .As('m', "omit-steep-tangents")
                .SetDefault(false)
                .WithDescription("Set this flag if you want to omit tangents that have a excessively big slope (> 20).");

            var argRes = fclp.Parse(_args);
            if(argRes.HasErrors)
            {
                Console.WriteLine(argRes.ErrorText);
                return -1;
            }
            if(argRes.HelpCalled)
            {
                return 0;
            }
            Args args = fclp.Object;
            args.ParseArgs();
            
            Console.WriteLine("TANGENT RENDERER\n");
            Console.WriteLine("[Options]");
            Console.WriteLine("Input function\t= {0}", args.InputFunction ?? "[none]");
            Console.WriteLine("Br. function\t= {0}", args.BrightnessFunction ?? "[none]");
            Console.WriteLine("Parametric\t= {0}", args.Parametric);
            Console.WriteLine("Output file\t= {0}", args.OutputFile ?? "[none]");
            Console.WriteLine("Extension files\t= {0}", args.ExtensionFiles.Any() ? String.Join(",",args.ExtensionFiles) : "[none]");
            Console.WriteLine("Output size\t= {0}x{1}", args.Width, args.Height);
            Console.WriteLine("Frame size\t= {0}x{1}", args.FrameWidth, args.FrameHeight);
            Console.WriteLine("Max iterations\t= {0}", args.MaxIterations);
            Console.WriteLine("D-Offset\t= {0}", args.DerivativeOffset);
            Console.WriteLine("x-Domain\t= [{0},{1}]", args.XMin, args.XMax);
            Console.WriteLine("y-Domain\t= [{0},{1}]", args.YMin, args.YMax);
            Console.WriteLine("t-Domain\t= [{0},{1}]", args.TMin, args.TMax);
            Console.WriteLine();

            FunctionCompiler fcomp = new FunctionCompiler();
            Func<float, Tuple<float, float>> f = fcomp.CompileFunction(args.InputFunction, "X.P", args.ExtensionFiles, args.Parametric);
            Func<float, Tuple<float, float>> b = args.BrightnessFunction == null 
                ? (x => new Tuple<float, float>(x, 1))
                : fcomp.CompileFunction(args.BrightnessFunction, "X.P", args.ExtensionFiles, true);

            if(f == null)
            {
                Console.WriteLine($"Syntax Error in \"{args.InputFunction}\"");
                return -1;
            }

            Renderer rnd = new Renderer(args, f, b);
            rnd.Start();

            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                rnd.Stop();
            };

            int line = Console.CursorTop;
            while (rnd.IsRunning())
            {
                System.Threading.Thread.Sleep(40);
                Console.CursorTop = line;
                Console.CursorLeft = 0;
                Console.WriteLine($"#Lines: {rnd.LineCount}");
            }

            Bitmap bmp = rnd.StopAndFinalize();
            bmp.Save(args.OutputFile);

            return 0;
        }
    }
}
