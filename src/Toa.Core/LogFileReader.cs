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
        private int _charBufIndex;
        private bool _needToCheckBuffer; // 読み込む前にバッファーに改行が含まれていないかチェックするべきかどうか
        private long _lastFileLength;

        public LogFileReader(string path)
        {
            this._stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }

        public string Read()
        {
            if (this._needToCheckBuffer)
            {
                for (var i = 0; i < this._charBufIndex; i++)
                {
                    if (this._charBuffer[i] == '\n')
                    {
                        var strSize = i > 0 && this._charBuffer[i - 1] == '\r'
                           ? i - 1 : i;
                        var result = new string(this._charBuffer, 0, strSize);

                        this._charBufIndex = this._charBufIndex - i - 1;
                        Array.Copy(this._charBuffer, i + 1, this._charBuffer, 0, this._charBufIndex);

                        return result;
                    }
                }

                this._needToCheckBuffer = false;
            }

            var newLength = this._stream.Length; // FileStream.Length はメソッドにするべきでは？
            if (newLength <= this._lastFileLength) return null;
            this._lastFileLength = newLength;

            while (true)
            {
                var byteCount = this._stream.Read(this._buffer, 0, this._buffer.Length);
                if (byteCount == 0) return null;

                if (this._charBufIndex + (byteCount + 1) / 2 > this._charBuffer.Length)
                {
                    // 拡張
                    Array.Resize(ref this._charBuffer, this._charBuffer.Length * 2);
                }

                var charCount = this._decoder.GetChars(this._buffer, 0, byteCount, this._charBuffer, this._charBufIndex);

                for (var i = 0; i < charCount; i++)
                {
                    var index = this._charBufIndex + i;
                    if (this._charBuffer[index] == '\n')
                    {
                        var strSize = index > 0 && this._charBuffer[index - 1] == '\r'
                            ? index - 1 : index;
                        var result = new string(this._charBuffer, 0, strSize);

                        this._charBufIndex = charCount - i - 1;
                        Array.Copy(this._charBuffer, index + 1, this._charBuffer, 0, this._charBufIndex);
                        this._needToCheckBuffer = true;

                        return result;
                    }
                }

                this._charBufIndex += charCount;
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
