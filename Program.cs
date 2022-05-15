using PixelStacker.Logic.Collections.ColorMapper;
using PixelStacker.Logic.Engine;
using PixelStacker.Logic.IO.Config;
using PixelStacker.Logic.IO.Formatters;
using PixelStacker.Logic.Model;
using SkiaSharp;
using System;
using System.Threading.Tasks;
using System.IO;
using System.Linq;



// This Program is entierly based on https://github.com/Pangamma/PixelStacker



namespace ImageToSchematic
{
    class Program
    {
        static void Main(string[] args)
        {
            String inputFilePath = null;
            String outputFilePath = null;
            bool batch = false;
            bool noOutput = false;
            bool enableQuantiser = false;
            bool makeMultilayer = false;
            bool makeTopView = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Equals("--input"))
                    inputFilePath = args[i + 1];
                if (args[i].Equals("--output"))
                    outputFilePath = args[i + 1];
                if (args[i].Equals("--batch"))
                    batch = true;
                if (args[i].Equals("--nolog"))
                    noOutput = true;
                if (args[i].Equals("--quantise"))
                    enableQuantiser = true;
                if (args[i].Equals("--multilayer"))
                    makeMultilayer = true;
                if (args[i].Equals("--topview"))
                    makeTopView = true;
            }

            if (inputFilePath == null || outputFilePath == null)
            {
                Console.WriteLine("\nSyntax error.\n\nAvaliable arguments:");
                Console.WriteLine("    --output   Set output file path");
                Console.WriteLine("    --input    Set input file path");
                Console.WriteLine("    --batch    Processes all files in the input folder\n" +
                                  "               (Input and output arguments should be folders)");
                Console.WriteLine("    --nolog    Disable ALL program output\n\n");
                return;
            }

            Options options = GetOptions(!makeTopView, makeMultilayer, enableQuantiser);

            if (batch)
            {
                ConvertBatch(inputFilePath, outputFilePath, options, noOutput);
            }
            else
            {
                ConvertImage(inputFilePath, outputFilePath, options, noOutput);
            }
        }

        static SKBitmap LoadImageFromFile(string filePath)
        {
            using (SKBitmap img = SKBitmap.Decode(filePath))
            {
                return img.Copy();
            }
        }

        static RenderedCanvas RenderCanvas(SKBitmap image, Options options)
        {
            RenderCanvasEngine RenderEngine = new RenderCanvasEngine();
            IColorMapper ColorMapper = new KdTreeMapper();
            MaterialPalette Palette = MaterialPalette.FromResx();
            ColorMapper.SetSeedData(Palette.ToValidCombinationList(options), Palette, options.Preprocessor.IsSideView);

            RenderedCanvas canvas = new RenderedCanvas();
            Task syncTask = new Task (async () =>
            {
                SKBitmap imgPreprocessed = await RenderEngine.PreprocessImageAsync(null, image, options.Preprocessor);
                canvas = await RenderEngine.RenderCanvasAsync(null, imgPreprocessed, ColorMapper, Palette);
            });
            syncTask.RunSynchronously();

            return canvas;
        }

        static void SaveSchematicToFile(string filePath, RenderedCanvas canvas, Options options)
        {
            IExportFormatter formatter = new SchematicFormatter();

            Task syncTask = new Task (async () =>
            {
                await formatter.ExportAsync(filePath, new PixelStackerProjectData(canvas, options), null);
            });

            syncTask.RunSynchronously();
        }

        static Options GetOptions(bool sideView = true, bool multilayer = false, bool quantise = false)
        {
            Options options = new Options(null)
            {
                IsSideView = sideView,
                IsMultiLayer = multilayer,
            };
            options.Preprocessor.IsSideView = sideView;
            options.Preprocessor.QuantizerSettings.IsEnabled = quantise;
            options.MaterialOptions.IsMultiLayer = multilayer;
            return options;
        }

        static void ConvertImage(string inputPath, string outputPath, Options options, bool noOutput)
        {
            if (!noOutput)
                Console.WriteLine("\nInput file: {0}\nOutput file: {1}\n", inputPath, outputPath);

            if (!noOutput) Console.WriteLine("Loading Image...");
            SKBitmap loadedImage = LoadImageFromFile(inputPath);
            if (!noOutput) Console.WriteLine("Converting Image...");
            RenderedCanvas canvas = RenderCanvas(loadedImage, options);
            if (!noOutput) Console.WriteLine("Saving Schematic...");
            SaveSchematicToFile(outputPath, canvas, options);
            if (!noOutput) Console.WriteLine("Finished.\n");
        }

        static void ConvertBatch(string inputPath, string outputPath, Options options, bool noOutput)
        {   
            if (!noOutput)
                Console.WriteLine("\nInput directory: {0}\nOutput directory: {1}\n", inputPath, outputPath);

            if (!Directory.Exists(inputPath))
            {
                if (!noOutput)
                    Console.WriteLine("Input directory does not exist. Aborting.");
                return;
            }
            if (!Directory.Exists(outputPath))
            {
                if (!noOutput)
                    Console.WriteLine("Output directory does not exist. Creating directory...");
                try
                {
                    Directory.CreateDirectory(outputPath);
                }
                catch (Exception)
                {   
                    if (!noOutput)
                        Console.WriteLine("Failed to create Output directory.");
                    return;
                }
                if (!noOutput)
                    Console.WriteLine("Directory created.");
            }

            string[] imageExtensions = { "bmp", "gif", "ico", "jpeg", "jpg", "ktx", "png", "webp", "wbmp" };

            foreach (var inFile in Directory.GetFiles(inputPath))
            {
                string fileName = inFile.Split("\\").Last();
                string extension = fileName.Split(".").Last();

                if (!imageExtensions.Contains(extension.ToLower()))
                    continue;

                int extPos = fileName.LastIndexOf(extension);
                string outFile = outputPath + "\\" + fileName.Substring(0, extPos) + "schematic";
                ConvertImage(inFile, outFile, options, true);

                if (!noOutput)
                    Console.WriteLine("{0} => {1}", inFile, outFile);
            }
        }
    }
}
