using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using ICN_T2.Logic.Level5.Compression;

namespace ICN_T2.Logic.Level5.Image
{
    public static class IMGC
    {
        public static Bitmap ToBitmap(byte[] fileContent)
        {
            if (fileContent == null || fileContent.Length == 0) return null;

            byte[] dataToProcess = fileContent;

            // Check for IMGC magic (0x49, 0x4D, 0x47, 0x43)
            bool isIMGC = fileContent.Length >= 4 &&
                          fileContent[0] == 0x49 && fileContent[1] == 0x4D &&
                          fileContent[2] == 0x47 && fileContent[3] == 0x43;

            if (!isIMGC)
            {
                try
                {
                    // Attempt decompression
                    byte[] decompressed = Compressor.Decompress(fileContent);

                    // Verify magic on decompressed data
                    if (decompressed.Length >= 4 &&
                        decompressed[0] == 0x49 && decompressed[1] == 0x4D &&
                        decompressed[2] == 0x47 && decompressed[3] == 0x43)
                    {
                        dataToProcess = decompressed;
                    }
                    else
                    {
                        // Not an IMGC file even after decompression
                        return null;
                    }
                }
                catch
                {
                    // Decompression failed
                    return null;
                }
            }

            try
            {
                using (var ms = new MemoryStream(dataToProcess))
                using (var reader = new BinaryReader(ms))
                {
                    // Ensure enough data for header
                    if (ms.Length < Marshal.SizeOf(typeof(IMGCSupport.Header))) return null;

                    var header = ReadStruct<IMGCSupport.Header>(reader);
                    System.Diagnostics.Debug.WriteLine($"[IMGC] Header: Fmt={header.ImageFormat}, CombFmt={header.CombineFormat}, BitDepth={header.BitDepth}, BPT={header.BytesPerTile}, W={header.Width}, H={header.Height}, TileOff={header.TileOffset}, TileSz1={header.TileSize1}, TileSz2={header.TileSize2}, ImgSz={header.ImageSize}");

                    // Validate Tile Data Offset/Size
                    if (header.TileOffset < 0 || header.TileOffset >= ms.Length) return null;
                    if (header.TileOffset + header.TileSize1 > ms.Length) return null;

                    reader.BaseStream.Seek(header.TileOffset, SeekOrigin.Begin);
                    byte[] tileData = reader.ReadBytes(header.TileSize1);
                    if (tileData.Length != header.TileSize1) return null;

                    tileData = SafeDecompress(tileData);
                    if (tileData == null || tileData.Length == 0) return null;

                    // [FIX] 레거시와 동일: imageData 오프셋 = TileOffset + TileSize2 (TileSize1이 아님!)
                    // TileSize1은 압축된 크기, TileSize2는 섹션 크기 (패딩 포함)
                    long imageDataOffset = header.TileOffset + header.TileSize2;
                    if (imageDataOffset + header.ImageSize > ms.Length) return null;

                    reader.BaseStream.Seek(imageDataOffset, SeekOrigin.Begin);
                    byte[] imageData = reader.ReadBytes(header.ImageSize);
                    if (imageData.Length != header.ImageSize) return null;

                    imageData = SafeDecompress(imageData);
                    if (imageData == null || imageData.Length == 0) return null;

                    return DecodeImage(tileData, imageData, IMGCSupport.ImageFormats[header.ImageFormat], header.Width, header.Height, header.BitDepth);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IMGC] Error loading image: {ex.Message}");
                // Log detailed info for debugging
                try
                {
                    using (var ms = new MemoryStream(dataToProcess))
                    using (var reader = new BinaryReader(ms))
                    {
                        if (ms.Length >= Marshal.SizeOf(typeof(IMGCSupport.Header)))
                        {
                            var header = ReadStruct<IMGCSupport.Header>(reader);
                            System.Diagnostics.Debug.WriteLine($"[IMGC Debug] Header: Magic={header.Magic:X}, Format={header.ImageFormat}, TileSz1={header.TileSize1}, TileSz2={header.TileSize2}, ImgSz={header.ImageSize}, W={header.Width}, H={header.Height}");
                            System.Diagnostics.Debug.WriteLine($"[IMGC Debug] StreamLen={ms.Length}, TileOff={header.TileOffset}");
                        }
                    }
                }
                catch { }
                return null;
            }
        }

        private static byte[] SafeDecompress(byte[] data)
        {
            if (data == null || data.Length < 4) return data;

            // Check compression header
            int size = (data[0] >> 3) | (data[1] << 5) | (data[2] << 13) | (data[3] << 21);
            int method = data[0] & 7;

            // Heuristics:
            // 1. Method 0 = No Compression (Raw)
            // 2. Size > 1MB = Likely raw data misidentified as header
            // 3. Method > 5 = Invalid method
            // 4. Size == 0 = Invalid
            if (method == 0 || method > 5 || size > 1024 * 1024 || size == 0) return data;

            try
            {
                byte[] result = Compressor.Decompress(data);
                // Validate decompressed result is reasonable
                if (result == null || result.Length == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[IMGC Warning] Decompression returned empty. Returning null.");
                    return null;
                }
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IMGC Warning] Decompression failed: {ex.Message}. Returning null.");
                return null;
            }
        }

        private static T ReadStruct<T>(BinaryReader reader) where T : struct
        {
            int size = Marshal.SizeOf(typeof(T));
            if (reader.BaseStream.Position + size > reader.BaseStream.Length) throw new EndOfStreamException("Not enough data for struct");
            byte[] bytes = reader.ReadBytes(size);
            GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                T result = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T))!;
                return result;
            }
            finally
            {
                handle.Free();
            }
        }

        private static Bitmap DecodeImage(byte[] tile, byte[] imageData, IColorFormat imgFormat, int width, int height, int bitDepth)
        {
            if (tile == null || tile.Length == 0 || imageData == null || imageData.Length == 0) return null;
            System.Diagnostics.Debug.WriteLine($"[IMGC.Decode] format={imgFormat.Name}, size={imgFormat.Size}, tile={tile.Length}B, img={imageData.Length}B, {width}x{height}, bpp={bitDepth}");

            using (var tileReader = new BinaryReader(new MemoryStream(tile)))
            using (var texReader = new BinaryReader(new MemoryStream(imageData)))
            {
                if (tileReader.BaseStream.Length < 2) return null;

                int tableLength = (int)tileReader.BaseStream.Length;

                var tmp = tileReader.ReadUInt16(); // Read first 2 bytes
                tileReader.BaseStream.Position = 0;
                var entryLength = 2;
                if (tmp == 0x453)
                {
                    if (tileReader.BaseStream.Length < 8) return null;
                    tileReader.ReadBytes(8);
                    entryLength = 4;
                }
                System.Diagnostics.Debug.WriteLine($"[IMGC.Decode] tileTableFirst=0x{tmp:X4}, entryLen={entryLength}, tableLen={tableLength}");

                var ms = new MemoryStream();
                for (int i = (int)tileReader.BaseStream.Position; i < tableLength; i += entryLength)
                {
                    if (tileReader.BaseStream.Position + 4 > tileReader.BaseStream.Length && entryLength == 4) break;
                    if (tileReader.BaseStream.Position + 2 > tileReader.BaseStream.Length && entryLength == 2) break;

                    uint entry = (entryLength == 2) ? tileReader.ReadUInt16() : tileReader.ReadUInt32();
                    if (entry == 0xFFFF || entry == 0xFFFFFFFF)
                    {
                        for (int j = 0; j < 64 * bitDepth / 8; j++)
                        {
                            ms.WriteByte(0);
                        }
                    }
                    else
                    {
                        int blockSize = 64 * bitDepth / 8;
                        long startPos = entry * blockSize;

                        if (startPos + blockSize <= texReader.BaseStream.Length)
                        {
                            texReader.BaseStream.Position = startPos;
                            // Read chunk safely
                            byte[] chunk = texReader.ReadBytes(blockSize);
                            ms.Write(chunk, 0, chunk.Length);
                            // Fill remaining if partial read (shouldn't happen with check)
                            for (int k = chunk.Length; k < blockSize; k++) ms.WriteByte(0);
                        }
                        else
                        {
                            // Out of bounds, write zeros
                            for (int j = 0; j < blockSize; j++) ms.WriteByte(0);
                        }
                    }
                }

                byte[] assembled = ms.ToArray();
                System.Diagnostics.Debug.WriteLine($"[IMGC.Decode] assembled={assembled.Length}B");

                byte[] pic;
                switch (imgFormat.Name)
                {
                    case "ETC1A4":
                        pic = Compression.Algorithms.ETC1Decoder.DecompressETC1A4(assembled, width, height);
                        System.Diagnostics.Debug.WriteLine($"[IMGC.Decode] ETC1A4 decoded: {pic?.Length ?? 0}B");
                        break;
                    case "ETC1":
                        pic = Compression.Algorithms.ETC1Decoder.DecompressETC1(assembled, width, height);
                        System.Diagnostics.Debug.WriteLine($"[IMGC.Decode] ETC1 decoded: {pic?.Length ?? 0}B");
                        break;
                    default:
                        pic = assembled;
                        break;
                }

                // [RESTORED] Albatross original logic — IMGCSwizzle already aligns internally
                IMGCSwizzle imgcSwizzle = new IMGCSwizzle(width, height);
                var points = imgcSwizzle.GetPointSequence();

                int pixelCount = width * height;
                Color[] resultArray = new Color[pixelCount];

                for (int i = 0; i < pixelCount; i++)
                {
                    int dataIndex = i * imgFormat.Size;
                    byte[] group = new byte[imgFormat.Size];
                    Array.Copy(pic, dataIndex, group, 0, imgFormat.Size);
                    resultArray[i] = imgFormat.Decode(group);
                }

                var bmp = new Bitmap(width, height);
                var data = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

                int pointIndex = 0;
                foreach (var pair in points)
                {
                    if (pointIndex >= resultArray.Length) break;

                    int x = pair.X, y = pair.Y;

                    // Only draw pixels within the actual image bounds
                    if (0 <= x && x < width && 0 <= y && y < height)
                    {
                        var color = resultArray[pointIndex];
                        int pixelOffset = data.Stride * y / 4 + x;

                        // Safe pointer write
                        int pixelValue = color.ToArgb();
                        Marshal.WriteInt32(data.Scan0 + pixelOffset * 4, pixelValue);
                    }
                    pointIndex++;
                }

                bmp.UnlockBits(data);

                return bmp;
            }
        }

        /// <summary>
        /// Encodes a Bitmap into IMGC (.xi) format
        /// </summary>
        /// <param name="source">Source bitmap to encode</param>
        /// <param name="originalXi">Optional original .xi file to use as header template</param>
        /// <returns>Encoded .xi file bytes</returns>
        public static byte[]? FromBitmap(Bitmap source, byte[]? originalXi = null)
        {
            try
            {
                // 1. Get dimensions (align to 8)
                int width = (source.Width + 7) & ~7;
                int height = (source.Height + 7) & ~7;

                Bitmap resized;
                if (source.Width != width || source.Height != height)
                {
                    resized = new Bitmap(width, height);
                    using (Graphics g = Graphics.FromImage(resized))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.DrawImage(source, 0, 0, source.Width, source.Height);
                    }
                }
                else
                {
                    resized = (Bitmap)source.Clone();
                }

                byte bitDepth = 4; // RGBA8 = 4 bytes per pixel
                IColorFormat colorFormat = new RGBA8();

                // 2. Encode pixels in swizzled order
                IMGCSwizzle swizzle = new IMGCSwizzle(width, height);
                var points = swizzle.GetPointSequence().ToList();

                var pixelData = new List<byte>();
                foreach (var point in points)
                {
                    if (point.X < width && point.Y < height)
                    {
                        Color pixel = resized.GetPixel(point.X, point.Y);
                        byte[] encoded = colorFormat.Encode(pixel);
                        pixelData.AddRange(encoded);
                    }
                    else
                    {
                        // Padding pixels (for alignment)
                        pixelData.AddRange(new byte[bitDepth]);
                    }
                }

                resized.Dispose();

                // 3. Build tile table (sequential, no deduplication)
                int tileCount = (width / 8) * (height / 8); // 64x64 = 8x8 tiles = 64 tiles
                var tileTable = new List<byte>();

                // Use ushort entries (2 bytes each)
                for (ushort i = 0; i < tileCount; i++)
                {
                    tileTable.Add((byte)(i & 0xFF));
                    tileTable.Add((byte)((i >> 8) & 0xFF));
                }

                // 4. Compress tile table and pixel data
                var lz10 = new Compression.Algorithms.LZ10();
                byte[] compressedTileData = lz10.Compress(tileTable.ToArray());
                byte[] compressedImageData = lz10.Compress(pixelData.ToArray());

                // 5. Build header
                IMGCSupport.Header header;
                if (originalXi != null && originalXi.Length >= Marshal.SizeOf(typeof(IMGCSupport.Header)))
                {
                    // Use original header as template
                    using (var ms = new MemoryStream(originalXi))
                    using (var reader = new BinaryReader(ms))
                    {
                        header = ReadStruct<IMGCSupport.Header>(reader);
                    }
                }
                else
                {
                    // Create new header
                    header = new IMGCSupport.Header
                    {
                        Magic = 0x47434D49, // "IMGC"
                        UnkBlock1 = new byte[0x06],
                        ImageFormat = 0, // RGBA8
                        Unk2 = 0,
                        CombineFormat = 0,
                        BitDepth = bitDepth,
                        BytesPerTile = 0x40,
                        UnkBlock3 = new byte[0x08],
                        UnkBlock4 = new byte[0x14],
                        UnkBlock5 = new byte[0x08]
                    };
                }

                // Update size fields
                header.Width = (short)width;
                header.Height = (short)height;
                header.TileSize1 = compressedTileData.Length;
                header.TileSize2 = (compressedTileData.Length + 3) & ~3; // 4-byte aligned
                header.ImageSize = compressedImageData.Length;
                header.TileOffset = Marshal.SizeOf(typeof(IMGCSupport.Header));

                // 6. Assemble file
                using (var ms = new MemoryStream())
                using (var writer = new BinaryWriter(ms))
                {
                    // Write header
                    WriteStruct(writer, header);

                    // Write compressed tile data
                    writer.Write(compressedTileData);

                    // Pad to TileSize2 (4-byte alignment)
                    int padding = header.TileSize2 - compressedTileData.Length;
                    for (int i = 0; i < padding; i++)
                    {
                        writer.Write((byte)0);
                    }

                    // Write compressed image data
                    writer.Write(compressedImageData);

                    return ms.ToArray();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IMGC.FromBitmap] Error: {ex.Message}");
                return null;
            }
        }

        private static void WriteStruct<T>(BinaryWriter writer, T structure) where T : struct
        {
            int size = Marshal.SizeOf(typeof(T));
            byte[] bytes = new byte[size];
            GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                Marshal.StructureToPtr(structure, handle.AddrOfPinnedObject(), false);
                writer.Write(bytes);
            }
            finally
            {
                handle.Free();
            }
        }
    }
}
