#region PDFsharp - A .NET library for processing PDF
//
// Authors:
//   Stefan Lange
//
// Copyright (c) 2005-2017 empira Software GmbH, Cologne Area (Germany)
//
// http://www.pdfsharp.com
// http://sourceforge.net/projects/pdfsharp
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included
// in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
// THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
// DEALINGS IN THE SOFTWARE.
#endregion
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PdfSharp.Internal;
#if NET_ZIP
using System.IO.Compression;
#else
using PdfSharp.SharpZipLib.Zip.Compression;
using PdfSharp.SharpZipLib.Zip.Compression.Streams;
#endif

namespace PdfSharp.Pdf.Filters
{
    public class Adler32
    {
        private int a = 1;
        private int b = 0;
        private static readonly int Modulus = 65521;

        public int Checksum(byte[] data, int offset, int length)
        {
            for (int counter = 0; counter < length; ++counter)
            {
                a = (a + (data[offset + counter])) % Modulus;
                b = (b + a) % Modulus;
            }
            return ((b * 65536) + a);
        }
    }

    /// <summary>
    /// Implements the FlateDecode filter by wrapping SharpZipLib.
    /// </summary>
    public class FlateDecode : Filter
    {
        // Reference: 3.3.3  LZWDecode and FlateDecode Filters / Page 71

        /// <summary>
        /// Encodes the specified data.
        /// </summary>
        public override byte[] Encode(byte[] data)
        {
            return Encode(data, PdfFlateEncodeMode.Default);
        }



        /// <summary>
        /// Encodes the specified data.
        /// </summary>
        public byte[] Encode(byte[] data, PdfFlateEncodeMode mode)
        {
            MemoryStream ms = new MemoryStream();
#if NET_ZIP
            using (DeflateStream dstream = new DeflateStream(ms, CompressionLevel.Optimal))
            {
                dstream.Write(data, 0, data.Length);
            }
            var adler = new Adler32();
            var arr = new List<byte>(ms.ToArray());
            // Calculate adler checksum and put it at the end in Big-Endian.
            var check = BitConverter.GetBytes(adler.Checksum(arr.ToArray(), 0, (int)arr.Count)).Reverse().ToArray();
            arr.AddRange(check);
            // Add zlib headers
            // 78 DA - Best Compression 
            arr.Insert(0, (byte)0xDA);
            arr.Insert(0, (byte)0x78);
            return arr.ToArray();
#else
            int level = Deflater.DEFAULT_COMPRESSION;
            switch (mode)
            {
                case PdfFlateEncodeMode.BestCompression:
                    level = Deflater.BEST_COMPRESSION;
                    break;
                case PdfFlateEncodeMode.BestSpeed:
                    level = Deflater.BEST_SPEED;
                    break;
            }
            DeflaterOutputStream zip = new DeflaterOutputStream(ms, new Deflater(level, false));
            zip.Write(data, 0, data.Length);
            zip.Finish();
#endif
            return ms.ToArray();
        }

        /// <summary>
        /// Decodes the specified data.
        /// </summary>
        public override byte[] Decode(byte[] data, FilterParms parms)
        {
            MemoryStream msInput = new MemoryStream(data);
            MemoryStream msOutput = new MemoryStream();
#if NET_ZIP
            // See http://connect.microsoft.com/VisualStudio/feedback/ViewFeedback.aspx?FeedbackID=97064
            // It seems to work when skipping the first two bytes.
            byte header;   // 0x30 0x59
            header = (byte)msInput.ReadByte();
            //Debug.Assert(header == 48);
            header = (byte)msInput.ReadByte();
            //Debug.Assert(header == 89);
            DeflateStream zip = new DeflateStream(msInput, CompressionMode.Decompress, true);
            int cbRead;
            byte[] abResult = new byte[1024];
            do
            {
                cbRead = zip.Read(abResult, 0, abResult.Length);
                if (cbRead > 0)
                    msOutput.Write(abResult, 0, cbRead);
            }
            while (cbRead > 0);
            zip.Close();
            msOutput.Flush();
            if (msOutput.Length >= 0)
            {
                msOutput.Capacity = (int)msOutput.Length;
                return msOutput.GetBuffer();
            }
            return null;
#else
            InflaterInputStream iis = new InflaterInputStream(msInput, new Inflater(false));
            int cbRead;
            byte[] abResult = new byte[32768];
            do
            {
                cbRead = iis.Read(abResult, 0, abResult.Length);
                if (cbRead > 0)
                    msOutput.Write(abResult, 0, cbRead);
            }
            while (cbRead > 0);
#if UWP
            iis.Dispose();
#else
            iis.Close();
#endif
            msOutput.Flush();
            if (msOutput.Length >= 0)
            {
#if NETFX_CORE || UWP
                return msOutput.ToArray();
#else
                msOutput.Capacity = (int)msOutput.Length;
                return msOutput.GetBuffer();
#endif
            }
            return null;
#endif
        }
    }
}
