using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NINA.Plugin.ExposureBudget.MakeIcon {

    /// <summary>
    /// Rasterise l'icone calculatrice de N.I.N.A. (ressource "CalculatorSVG" du
    /// dictionnaire SVG de NINA.WPF.Base) en PNG, pour l'integrer au plugin et
    /// l'afficher dans sa fiche (Options &gt; Plugins, a cote de l'auteur et de la version).
    ///
    /// Utilisation :
    ///   dotnet run --project tools\makeicon [chemin du PNG de sortie]
    ///
    /// A relancer si N.I.N.A. modifie son icone.
    /// </summary>
    internal static class Program {

        private const string CalculatorGeometryKey = "CalculatorSVG";

        private static readonly Uri SvgDictionaryUri =
            new Uri("pack://application:,,,/NINA.WPF.Base;component/Resources/StaticResources/SVGDictionary.xaml", UriKind.Absolute);

        /// <summary>Taille du PNG produit : suffisant pour un affichage net jusqu'a 150 px.</summary>
        private const int IconSize = 256;

        /// <summary>Marge autour de l'icone, en fraction de la taille.</summary>
        private const double MarginRatio = 0.08;

        [STAThread]
        private static int Main(string[] args) {
            try {
                // Mode analyse : mesure une image fournie (dimensions, transparence, contraste)
                // sans la modifier, pour decider comment l'integrer au plugin.
                if (args != null && args.Length >= 2 && args[0] == "--inspect") {
                    return Inspect(args[1]);
                }

                // Mode redimensionnement : prepare une image fournie pour la fiche du plugin
                // (reduction de haute qualite, cadrage carre centre, PNG conserve).
                if (args != null && args.Length >= 3 && args[0] == "--resize") {
                    var size = args.Length >= 4 && int.TryParse(args[3], out var parsed) ? parsed : 256;
                    return Resize(args[1], args[2], size);
                }

                // Les URI "pack://application" exigent une application WPF.
                var application = Application.Current ?? new Application();

                var outputPath = args != null && args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
                    ? args[0]
                    : Path.Combine("NINA.Plugin.ExposureBudget", "Resources", "Calculator.png");

                var geometry = LoadCalculatorGeometry();
                if (geometry == null) {
                    Console.Error.WriteLine($"Geometrie '{CalculatorGeometryKey}' introuvable dans {SvgDictionaryUri}");
                    return 1;
                }

                var bitmap = Render(geometry);

                var folder = Path.GetDirectoryName(Path.GetFullPath(outputPath));
                if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder)) {
                    Directory.CreateDirectory(folder);
                }

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using (var stream = File.Create(outputPath)) {
                    encoder.Save(stream);
                }

                Console.WriteLine($"Icone ecrite : {Path.GetFullPath(outputPath)} ({IconSize}x{IconSize})");
                DescribeGeometry(geometry);
                DescribeBitmap(bitmap);
                return 0;
            } catch (Exception ex) {
                Console.Error.WriteLine($"Echec de la generation de l'icone : {ex}");
                return 1;
            }
        }

        /// <summary>Auto-verification : la geometrie doit etre celle d'une calculatrice (portrait).</summary>
        private static void DescribeGeometry(Geometry geometry) {
            var bounds = geometry.Bounds;
            Console.WriteLine($"  geometrie : {bounds.Width} x {bounds.Height} (ratio largeur/hauteur {bounds.Width / bounds.Height:0.###})");
        }

        /// <summary>
        /// Auto-verification : controle que le dessin occupe bien l'image, centree, et que
        /// les trous (ecran et touches) sont transparents comme dans N.I.N.A.
        /// </summary>
        private static void DescribeBitmap(RenderTargetBitmap bitmap) {
            var stride = bitmap.PixelWidth * 4;
            var pixels = new byte[stride * bitmap.PixelHeight];
            bitmap.CopyPixels(pixels, stride, 0);

            int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1, opaque = 0;

            for (var y = 0; y < bitmap.PixelHeight; y++) {
                for (var x = 0; x < bitmap.PixelWidth; x++) {
                    var alpha = pixels[(y * stride) + (x * 4) + 3];
                    if (alpha <= 16) { continue; }

                    opaque++;
                    if (x < minX) { minX = x; }
                    if (y < minY) { minY = y; }
                    if (x > maxX) { maxX = x; }
                    if (y > maxY) { maxY = y; }
                }
            }

            if (maxX < 0) {
                Console.WriteLine("  image : VIDE (aucun pixel opaque) — probleme de rendu");
                return;
            }

            var dessinHauteur = maxY - minY + 1;
            var dessinLargeur = maxX - minX + 1;
            Console.WriteLine($"  image : dessin {dessinLargeur} x {dessinHauteur} px, marge gauche/droite {minX}/{bitmap.PixelWidth - 1 - maxX}, haut/bas {minY}/{bitmap.PixelHeight - 1 - maxY}");
            Console.WriteLine($"  image : {opaque * 100.0 / (bitmap.PixelWidth * bitmap.PixelHeight):0.#} % de pixels opaques (le reste = trous de l'ecran et des touches)");
        }

        /// <summary>
        /// Prepare une image fournie par l'utilisateur pour la fiche du plugin :
        /// reduction de haute qualite (par demiages successifs puis rendu final), cadrage
        /// carre centre sur fond transparent, enregistrement en PNG.
        /// </summary>
        private static int Resize(string input, string output, int targetSize) {
            var fullInput = Path.GetFullPath(input);
            if (!File.Exists(fullInput)) {
                Console.Error.WriteLine($"Image introuvable : {fullInput}");
                return 1;
            }

            var decoder = BitmapDecoder.Create(new Uri(fullInput), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            BitmapSource source = decoder.Frames[0];

            Console.WriteLine($"Source : {source.PixelWidth} x {source.PixelHeight}, {source.Format}");

            // Reduction par demiages successifs : chacune des etapes divise par deux, ce qui
            // evite les artefacts d'un redimensionnement brutal.
            while (source.PixelWidth > targetSize * 2 && source.PixelHeight > targetSize * 2) {
                source = new TransformedBitmap(source, new ScaleTransform(0.5, 0.5));
            }

            var bitmap = FitToSquare(source, targetSize);
            var fullOutput = Path.GetFullPath(output);
            var folder = Path.GetDirectoryName(fullOutput);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder)) {
                Directory.CreateDirectory(folder);
            }

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(fullOutput)) {
                encoder.Save(stream);
            }

            Console.WriteLine($"Ecrit  : {fullOutput} ({bitmap.PixelWidth} x {bitmap.PixelHeight}, {new FileInfo(fullOutput).Length / 1024.0:0} Ko)");
            DescribeBitmap(bitmap);
            return 0;
        }

        /// <summary>
        /// Dessine l'image centree dans un carre de la taille demandee (marges transparentes
        /// si l'image n'est pas carree), avec un echantillonnage de qualite.
        /// </summary>
        private static RenderTargetBitmap FitToSquare(BitmapSource source, int size) {
            var scale = Math.Min(size / (double)source.PixelWidth, size / (double)source.PixelHeight);
            var width = source.PixelWidth * scale;
            var height = source.PixelHeight * scale;

            var visual = new DrawingVisual();
            RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);

            using (var context = visual.RenderOpen()) {
                context.DrawImage(source, new Rect((size - width) / 2, (size - height) / 2, width, height));
            }

            var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }

        /// <summary>
        /// Analyse une image fournie : dimensions, format, presence de transparence,
        /// couleurs des coins et contraste, afin de choisir la mise en forme adaptee a la
        /// fiche du plugin (N.I.N.A. l'affiche sur fond sombre, en 150x150 et en 25x25).
        /// </summary>
        private static int Inspect(string path) {
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath)) {
                Console.Error.WriteLine($"Image introuvable : {fullPath}");
                return 1;
            }

            var decoder = BitmapDecoder.Create(new Uri(fullPath), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames[0];

            Console.WriteLine($"Image     : {fullPath} ({new FileInfo(fullPath).Length / 1024.0:0} Ko)");
            Console.WriteLine($"Dimensions: {frame.PixelWidth} x {frame.PixelHeight} px (ratio {frame.PixelWidth / (double)frame.PixelHeight:0.###}), DPI {frame.DpiX}x{frame.DpiY}");
            Console.WriteLine($"Format    : {frame.Format} ({frame.Format.BitsPerPixel} bits)");

            var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
            var width = converted.PixelWidth;
            var height = converted.PixelHeight;
            var stride = width * 4;
            var pixels = new byte[stride * height];
            converted.CopyPixels(pixels, stride, 0);

            int transparent = 0, opaque = 0, partial = 0;
            long luminanceSum = 0;
            int light = 0, dark = 0;

            for (var y = 0; y < height; y++) {
                for (var x = 0; x < width; x++) {
                    var index = (y * stride) + (x * 4);
                    var b = pixels[index];
                    var g = pixels[index + 1];
                    var r = pixels[index + 2];
                    var a = pixels[index + 3];

                    if (a == 0) { transparent++; continue; }
                    if (a == 255) { opaque++; } else { partial++; }

                    var luminance = (int)((0.2126 * r) + (0.7152 * g) + (0.0722 * b));
                    luminanceSum += luminance;
                    if (luminance >= 128) { light++; } else { dark++; }
                }
            }

            var visible = opaque + partial;
            Console.WriteLine($"Alpha     : {transparent * 100.0 / (width * height):0.#} % transparent, {opaque * 100.0 / (width * height):0.#} % opaque, {partial * 100.0 / (width * height):0.#} % semi-transparent");

            if (visible > 0) {
                Console.WriteLine($"Luminance : moyenne {luminanceSum / (double)visible:0} sur 255 ({light * 100.0 / visible:0} % de pixels clairs, {dark * 100.0 / visible:0} % sombres)");
            }

            Console.WriteLine("Coins     : " + string.Join("  ", new[] {
                $"HG {Describe(pixels, 0, 0, stride)}",
                $"HD {Describe(pixels, 0, width - 1, stride)}",
                $"BG {Describe(pixels, height - 1, 0, stride)}",
                $"BD {Describe(pixels, height - 1, width - 1, stride)}"
            }));

            Console.WriteLine($"Bord      : {DescribeBorder(pixels, width, height, stride)}");
            return 0;
        }

        private static string Describe(byte[] pixels, int x, int y, int stride) {
            var index = (y * stride) + (x * 4);
            return $"B{pixels[index]} V{pixels[index + 1]} R{pixels[index + 2]} A{pixels[index + 3]}";
        }

        /// <summary>Resume la bordure de l'image : couleur uniforme (fond) ou variee.</summary>
        private static string DescribeBorder(byte[] pixels, int width, int height, int stride) {
            var samples = 0;
            long b = 0, g = 0, r = 0, a = 0;
            int minA = 255, maxA = 0;

            void Sample(int x, int y) {
                var index = (y * stride) + (x * 4);
                b += pixels[index];
                g += pixels[index + 1];
                r += pixels[index + 2];
                a += pixels[index + 3];
                if (pixels[index + 3] < minA) { minA = pixels[index + 3]; }
                if (pixels[index + 3] > maxA) { maxA = pixels[index + 3]; }
                samples++;
            }

            for (var x = 0; x < width; x += Math.Max(1, width / 40)) {
                Sample(x, 0);
                Sample(x, height - 1);
            }

            for (var y = 0; y < height; y += Math.Max(1, height / 40)) {
                Sample(0, y);
                Sample(width - 1, y);
            }

            return $"moyenne B{b / samples} V{g / samples} R{r / samples} A{a / samples}, alpha entre {minA} et {maxA}";
        }

        private static Geometry LoadCalculatorGeometry() {
            var dictionary = new ResourceDictionary { Source = SvgDictionaryUri };
            return dictionary[CalculatorGeometryKey] as Geometry;
        }

        /// <summary>
        /// Dessine la geometrie centree dans une image carree, en blanc sur fond transparent
        /// (comme les autres icones de N.I.N.A. dans le theme sombre).
        /// </summary>
        private static RenderTargetBitmap Render(Geometry geometry) {
            var bounds = geometry.Bounds;
            var drawingSize = IconSize * (1 - (2 * MarginRatio));
            var scale = Math.Min(drawingSize / bounds.Width, drawingSize / bounds.Height);

            var transform = new TransformGroup();
            transform.Children.Add(new ScaleTransform(scale, scale));
            transform.Children.Add(new TranslateTransform(
                ((IconSize - (bounds.Width * scale)) / 2) - (bounds.X * scale),
                ((IconSize - (bounds.Height * scale)) / 2) - (bounds.Y * scale)));

            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen()) {
                context.PushTransform(transform);
                context.DrawGeometry(Brushes.White, null, geometry);
                context.Pop();
            }

            var bitmap = new RenderTargetBitmap(IconSize, IconSize, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }
    }
}
