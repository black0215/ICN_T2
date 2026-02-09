using System;
using System.Runtime.InteropServices;

namespace Albatross.Level5.Compression
{
    /// <summary>
    /// Wrapper class for the native Level-5 Decompressor DLL.
    /// </summary>
    public static class Level5Decompressor
    {
        // Name of the DLL. This should match the output name of the C++ project.
        private const string DllName = "Level5Decompressor.dll";

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int Decompress(IntPtr input, int inputLength, out IntPtr output, out int outputLength);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void FreeData(IntPtr data);

        /// <summary>
        /// Decompresses Level-5 Type A data.
        /// </summary>
        /// <param name="data">The compressed byte array.</param>
        /// <returns>The decompressed byte array.</returns>
        /// <exception cref="Exception">Thrown when decompression fails.</exception>
        public static byte[] Decompress(byte[] data)
        {
            if (data == null || data.Length == 0)
                return new byte[0];

            IntPtr inputPtr = Marshal.AllocHGlobal(data.Length);
            IntPtr outputPtr = IntPtr.Zero;
            int outputLen = 0;

            try
            {
                Marshal.Copy(data, 0, inputPtr, data.Length);

                int result = Decompress(inputPtr, data.Length, out outputPtr, out outputLen);

                if (result != 0)
                {
                    throw new Exception($"Level-5 Decompression failed with error code: {result}");
                }

                if (outputLen > 0 && outputPtr != IntPtr.Zero)
                {
                    byte[] outputBytes = new byte[outputLen];
                    Marshal.Copy(outputPtr, outputBytes, 0, outputLen);
                    return outputBytes;
                }
                else
                {
                    return new byte[0];
                }
            }
            finally
            {
                if (inputPtr != IntPtr.Zero)
                    Marshal.FreeHGlobal(inputPtr);
                
                if (outputPtr != IntPtr.Zero)
                    FreeData(outputPtr);
            }
        }
    }
}
