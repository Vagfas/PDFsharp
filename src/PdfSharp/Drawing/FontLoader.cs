
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using PdfSharp.Fonts.OpenType;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.Filters;

namespace PdfSharp.Drawing
{
    public static class FontLoader
    {
        public static Dictionary<string, byte[]> FontCache = new Dictionary<string, byte[]>();
        public static byte[][] FontDeflate; // Deflate komprimerad data
        public static ConcurrentDictionary<ulong, XFontSource> SourceCache = new ConcurrentDictionary<ulong, XFontSource>();
        public static ConcurrentDictionary<ulong, XGlyphTypeface> GlyphCache = new ConcurrentDictionary<ulong, XGlyphTypeface>();
        private static Func<string, byte[]> LaddaFunc;
        private static Func<string, float, bool, bool, string> ResolveFunc;

        enum Fonter
        {
            Calibri,
            CalibriBold,
            CalibriBoldItalic,
            CalibriItalic,
            OCRB10Pitch
        }

        public static void Init(Func<string, byte[]> laddaFunc, Func<string, float, bool, bool, string> resolveFunc)
        {
            LaddaFunc = laddaFunc;
            ResolveFunc = resolveFunc;

            FontDeflate = new byte[][] { 
                    DeflateCacha((int)Fonter.Calibri, "Calibri", 9, false, false),
                    DeflateCacha((int)Fonter.CalibriBold, "Calibri", 9, true, false),
                    DeflateCacha((int)Fonter.CalibriBoldItalic, "Calibri", 9, true, true),
                    DeflateCacha((int)Fonter.CalibriItalic, "Calibri", 9, false, true),
                DeflateCacha((int)Fonter.OCRB10Pitch, "OCR-B 10 BT", 9, false, false)
                };
        }

        public static byte[] DeflateData(string fullFaceName)
        {
            if(fullFaceName == "Calibri Bold") 
                return FontDeflate[(int)Fonter.CalibriBold];
            return FontDeflate[(int)Fonter.Calibri];
        }

        public static byte[] DeflateCacha(int idx, string familyName, float _emSize, bool bold, bool italic)
        {
            var bytes = LaddaTTFFont(familyName, _emSize, bold, italic);
            return Filtering.FlateDecode.Encode(bytes, PdfFlateEncodeMode.BestCompression);
        }

        public static PdfFont LaddaPDFFont(XFont font)
        {
            PdfFont pdfFont;
            if (font.Unicode)
                pdfFont = new PdfType0Font(null, font, font.IsVertical);
            else
                pdfFont = new PdfTrueTypeFont(null, font);
            return pdfFont;
        }
         
        public static XGlyphTypeface LaddaGlyphTypeface(ulong sum)
        {
            if (GlyphCache.ContainsKey(sum))
                return GlyphCache[sum];
            GlyphCache[sum] = new XGlyphTypeface(SourceCache[sum]);
            return GlyphCache[sum];
        }

        public static XFontSource LaddaFontSource(byte[] bytes)
        {
            ulong sum = 0;
            for (var i = 0; i < 2048; i++)
                sum += bytes[i];
            if (SourceCache.ContainsKey(sum))
                return SourceCache[sum];
            var src = new XFontSource(bytes, sum);
            src.Fontface = new OpenTypeFontface(src);
            SourceCache[sum] = src;
            return SourceCache[sum];
        }

        public static byte[] LaddaTTFFont(string familyName, float _emSize, bool bold, bool italic)
        {
            var font = ResolveFunc(familyName, _emSize, bold, italic);
            if (FontCache.ContainsKey(font))
                return FontCache[font];

            var bytes = LaddaFunc(font);
            FontCache[font] = bytes;

            return bytes;
        }
    }
}