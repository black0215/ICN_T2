using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace ICN_T2.YokaiWatch.Definitions
{
    public class XQ32
    {
        public byte[] Data { get; set; }
        public string FilePath { get; set; }

        // HP는 0x78 오프셋에 위치 (int16이지만 4바이트 텀이 있으므로 주변 데이터 조심)
        private const int HP_OFFSET = 0x78;

        public XQ32(byte[] data, string filePath = "")
        {
            Data = data;
            FilePath = filePath;
        }

        public int GetHP()
        {
            if (Data.Length < HP_OFFSET + 2) return 0;
            return BitConverter.ToUInt16(Data, HP_OFFSET);
        }

        public void SetHP(int hp)
        {
            if (Data.Length < HP_OFFSET + 2) return;

            byte[] hpBytes = BitConverter.GetBytes((ushort)hp);
            Data[HP_OFFSET] = hpBytes[0];
            Data[HP_OFFSET + 1] = hpBytes[1];
        }

        public void Save(string path)
        {
            File.WriteAllBytes(path, Data);
        }
    }
}
