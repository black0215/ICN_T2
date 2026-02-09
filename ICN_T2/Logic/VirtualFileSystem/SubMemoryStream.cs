using System;
using System.IO;
using System.Drawing;

namespace ICN_T2.Logic.VirtualFileSystem
{
    /// <summary>
    /// 원본 스트림의 일부분을 읽거나, 수정된 바이트 배열을 담는 스트림
    /// </summary>
    public class SubMemoryStream : Stream
    {
        private readonly Stream? _baseStream;
        private readonly long _offset;
        private readonly long _length;
        private long _position;

        /// <summary>
        /// 파일이 수정되었을 경우 데이터를 담는 버퍼.
        /// null이면 원본 스트림(_baseStream)을 참조함.
        /// </summary>
        public byte[]? ByteContent { get; set; }

        /// <summary>
        /// [호환성] 원본 스트림 내 오프셋 (읽기 전용)
        /// </summary>
        public long Offset => _offset;

        /// <summary>
        /// [호환성] UI 표시용 색상
        /// </summary>
        public Color Color { get; set; } = Color.Black;

        /// <summary>
        /// 데이터의 실제 크기 (수정되었다면 ByteContent 길이, 아니면 원본 길이)
        /// </summary>
        public long Size => ByteContent != null ? ByteContent.Length : _length;

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => Size;

        public override long Position
        {
            get => _position;
            set
            {
                if (value < 0 || value > Length) throw new ArgumentOutOfRangeException(nameof(value));
                _position = value;
            }
        }

        public SubMemoryStream(Stream baseStream, long offset, long length)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset), "Offset must be non-negative.");
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length), "Length must be non-negative.");

            _baseStream = baseStream;
            _offset = offset;
            _length = length;
            _position = 0;
            ByteContent = null;
        }

        public SubMemoryStream(byte[] data)
        {
            _baseStream = null;
            _offset = 0;
            _length = data.Length;
            _position = 0;
            ByteContent = data;
        }

        // ===================================================
        // [호환성] 인수 없는 Read() - 원본과 동일
        // ===================================================
        /// <summary>
        /// 원본 스트림에서 데이터를 읽어 ByteContent에 캐싱합니다.
        /// </summary>
        public void Read()
        {
            if (ByteContent != null) return; // 이미 로드됨

            if (_baseStream == null)
            {
                ByteContent = Array.Empty<byte>();
                return;
            }

            ByteContent = new byte[_length];
            lock (_baseStream) // 멀티스레드 안전
            {
                long originalPos = _baseStream.Position;
                _baseStream.Seek(_offset, SeekOrigin.Begin);
                _baseStream.ReadExactly(ByteContent, 0, ByteContent.Length);
                _baseStream.Position = originalPos;
            }
        }

        // ===================================================
        // [호환성] 인수 없는 Seek() - 원본과 동일
        // ===================================================
        /// <summary>
        /// BaseStream의 위치를 이 서브스트림의 시작점으로 이동합니다.
        /// </summary>
        public void Seek()
        {
            if (_baseStream != null)
            {
                _baseStream.Seek(_offset, SeekOrigin.Begin);
            }
            _position = 0;
        }

        // ===================================================
        // Stream 오버라이드
        // ===================================================
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position >= Length) return 0;

            int toRead = (int)Math.Min(count, Length - _position);

            if (ByteContent != null)
            {
                Array.Copy(ByteContent, _position, buffer, offset, toRead);
            }
            else if (_baseStream != null)
            {
                lock (_baseStream)
                {
                    long originalPos = _baseStream.Position;
                    _baseStream.Position = _offset + _position;
                    toRead = _baseStream.Read(buffer, offset, toRead);
                    _baseStream.Position = originalPos;
                }
            }
            else
            {
                return 0;
            }

            _position += toRead;
            return toRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin: Position = offset; break;
                case SeekOrigin.Current: Position += offset; break;
                case SeekOrigin.End: Position = Length + offset; break;
            }
            return Position;
        }

        public override void Flush() { }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException("Use ByteContent property to modify data.");

        // ===================================================
        // [호환성] ReadWithoutCaching - 원본과 동일
        // ===================================================
        /// <summary>
        /// 데이터를 읽되 ByteContent에 캐싱하지 않습니다.
        /// 읽기 전용 작업에서 "수정됨" 플래그를 방지합니다.
        /// </summary>
        public byte[] ReadWithoutCaching()
        {
            if (ByteContent != null) return ByteContent;
            if (_baseStream == null) return Array.Empty<byte>();

            byte[] buffer = new byte[_length];

            lock (_baseStream)
            {
                long originalPos = _baseStream.Position;
                try
                {
                    _baseStream.Seek(_offset, SeekOrigin.Begin);
                    _baseStream.ReadExactly(buffer, 0, buffer.Length);
                }
                finally
                {
                    _baseStream.Position = originalPos;
                }
            }

            return buffer;
        }

        // ===================================================
        // [호환성] CopyTo - 원본과 동일한 최적화 로직
        // ===================================================
        public new void CopyTo(Stream destination)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));

            // ByteContent가 있으면 바로 쓰기
            if (ByteContent != null)
            {
                destination.Write(ByteContent, 0, ByteContent.Length);
                return;
            }

            // BaseStream에서 청크 단위로 복사
            if (_baseStream == null)
                return;

            lock (_baseStream)
            {
                byte[] buffer = new byte[4096];
                long remaining = _length;

                _baseStream.Seek(_offset, SeekOrigin.Begin);

                while (remaining > 0)
                {
                    int toRead = (int)Math.Min(remaining, buffer.Length);
                    int bytesRead = _baseStream.Read(buffer, 0, toRead);
                    if (bytesRead == 0) break;

                    destination.Write(buffer, 0, bytesRead);
                    remaining -= bytesRead;
                }
            }
        }
    }
}