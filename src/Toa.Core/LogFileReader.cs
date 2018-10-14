using System;
using System.IO;
using System.Text;

namespace WagahighChoices.Toa
{
    internal class LogFileReader : IDisposable
    {
        private readonly FileStream _stream;
        private readonly Decoder _decoder = Encoding.Unicode.GetDecoder();
        private readonly byte[] _buffer = new byte[256];
        private char[] _charBuffer = new char[128];
        private int _charBufCount;
        private bool _needToCheckBuffer; // 読み込む前にバッファーに改行が含まれていないかチェックするべきかどうか

        public LogFileReader(string path)
        {
            this._stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }

        public string Read()
        {
            if (this._needToCheckBuffer)
            {
                for (var i = 0; i < this._charBufCount; i++)
                {
                    if (this._charBuffer[i] == '\n')
                    {
                        var strSize = i > 0 && this._charBuffer[i - 1] == '\r'
                            ? i - 1 : i;
                        var result = new string(this._charBuffer, 0, strSize);

                        this._charBufCount -= i + 1;
                        Array.Copy(this._charBuffer, i + 1, this._charBuffer, 0, this._charBufCount);

                        return result;
                    }
                }

                this._needToCheckBuffer = false;
            }

            while (true)
            {
                var byteCount = this._stream.Read(this._buffer, 0, this._buffer.Length);
                if (byteCount == 0) return null;

                var requiredLength = this._charBufCount + (byteCount + 1) / 2;
                if (requiredLength > this._charBuffer.Length)
                {
                    var newCharBufLength = this._charBuffer.Length * 2;
                    while (newCharBufLength < requiredLength) newCharBufLength *= 2;

                    // 拡張
                    Array.Resize(ref this._charBuffer, newCharBufLength);
                }

                var charCount = this._decoder.GetChars(this._buffer, 0, byteCount, this._charBuffer, this._charBufCount);

                for (var i = 0; i < charCount; i++)
                {
                    var index = this._charBufCount + i;
                    if (this._charBuffer[index] == '\n')
                    {
                        var strSize = index > 0 && this._charBuffer[index - 1] == '\r'
                            ? index - 1 : index;
                        var result = new string(this._charBuffer, 0, strSize);

                        this._charBufCount = charCount - i - 1;
                        Array.Copy(this._charBuffer, index + 1, this._charBuffer, 0, this._charBufCount);
                        this._needToCheckBuffer = true;

                        return result;
                    }
                }

                this._charBufCount += charCount;
            }
        }

        public void SeekToLastLine()
        {
            this._decoder.Reset();

            var streamLength = this._stream.Length;

            if (streamLength == 0) return;

            var pos = streamLength - this._buffer.Length;
            if (pos < 0) pos = 0;
            else if (pos % 2 != 0) pos--;

            while (true)
            {
                this._stream.Position = pos;
                var count = this._stream.Read(this._buffer, 0, this._buffer.Length);

                if (count == 0) throw new EndOfStreamException("話が違うぞ");

                for (var i = count - (count % 2 == 0 ? 2 : 3); i >= 0; i -= 2)
                {
                    if (this._buffer[i] == '\n' && this._buffer[i + 1] == 0)
                    {
                        this._stream.Position = pos + i + 2;
                        return;
                    }
                }

                if (pos == 0)
                {
                    this._stream.Position = 0;
                    return;
                }

                pos -= this._buffer.Length;
                if (pos < 0) pos = 0;
            }
        }

        public void Dispose()
        {
            this._stream.Dispose();
        }
    }
}
