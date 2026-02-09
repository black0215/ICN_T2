using System.Drawing;

namespace ICN_T2.Logic.Level5.Image
{
    public interface IColorFormat
    {
        string Name { get; }

        int Size { get; }

        byte[] Encode(Color color);

        Color Decode(byte[] data);
    }
}
