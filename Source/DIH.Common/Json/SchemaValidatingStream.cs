using Newtonsoft.Json;
using Newtonsoft.Json.Schema;
using System.Text;

namespace DIH.Common.Json
{
    /// <summary>
    /// A custom stream that replicates and validates JSON data from an input stream against a given schema,
    /// while allowing access to the validated data and any schema errors encountered.
    /// </summary>
    public class SchemaValidatingStream : Stream, IDisposable
    {
        private const int BUFFER_FLUSH_THRESHOLD = 131072; // Always write to stream and flush buffers after 128 KB
        private readonly Stream _inputStream;
        private readonly JSchemaValidatingReader _validatingReader;
        private readonly List<string> errors = new();

        // For stream book-keeping
        private byte[] _overflowBytes = Array.Empty<byte>();
        private readonly StringBuilder _sb = new();
        private readonly StringWriter _strWriter;
        private readonly JsonTextWriter _jsonTextWriter;
        private readonly Encoding _outputEncoding;

        /// <summary>
        /// Initializes a new instance of the <see cref="SchemaValidatingStream"/> class.
        /// It replicates JSON data from the provided input stream, validating it against the provided schema.
        /// Any schema validation errors encountered can be accessed through <see cref="SchemaErrors"/>.
        /// </summary>
        /// <param name="inputJsonStream">The input stream containing the JSON data to validate.</param>
        /// <param name="schema">The JSON schema used for validation.</param>
        /// <param name="inputEncoding">The encoding of the input stream. Defaults to UTF-8.</param>
        /// <param name="outputEncoding">The encoding for output. Defaults to UTF-8.</param>
        public SchemaValidatingStream(Stream inputJsonStream, string schema, Encoding? inputEncoding = null, Encoding? outputEncoding = null)
        {
            _inputStream = inputJsonStream;
            StreamReader sr = new(inputJsonStream, inputEncoding ?? Encoding.UTF8);
            JsonTextReader jsonTextReader = new(sr);
            _validatingReader = new JSchemaValidatingReader(jsonTextReader);
            _validatingReader.Schema = JSchema.Parse(schema);
            _validatingReader.ValidationEventHandler += _validatingReader_ValidationEventHandler;
            _strWriter = new StringWriter(_sb);
            _jsonTextWriter = new JsonTextWriter(_strWriter);
            _outputEncoding = outputEncoding == null ? Encoding.UTF8 : outputEncoding;
        }

        /// <summary>
        /// Reads a sequence of bytes from the current stream and advances the position within the stream by the number of bytes read.
        /// While reading, it validates the JSON data against the schema and keeps track of any validation errors.
        /// </summary>
        public override int Read(byte[] buffer, int offset, int count)
        {
            // Handle overflow bytes from last Read()
            int bytesWrittenToBuffer = WriteOverflowToBuffer(buffer, offset, count);

            // Max amount of local mem to use before flushing to caller's buffer
            int bufferWriteThreshold = Math.Min(count, BUFFER_FLUSH_THRESHOLD);

            try
            {
                // Fill string builder with json untill we have at least count characters
                while (bytesWrittenToBuffer < count && _validatingReader.Read())
                {
                    // Write json token to string builder
                    _jsonTextWriter.WriteToken(_validatingReader.TokenType, _validatingReader.Value);

                    if (_sb.Length >= bufferWriteThreshold)
                    {
                        // move string builder content to buffer
                        bytesWrittenToBuffer = WriteToBuffer(buffer, offset, count, bytesWrittenToBuffer);
                    }
                }
            }
            catch (JsonReaderException ex)
            {
                throw new InvalidDataException($"Failed to parse JSON", ex);
            }

            bytesWrittenToBuffer = WriteToBuffer(buffer, offset, count, bytesWrittenToBuffer);

            // Return the number of bytes we wrote to buffer
            return bytesWrittenToBuffer;
        }

        private int WriteOverflowToBuffer(byte[] buffer, int offset, int count)
        {
            int overflowReturnCount = Math.Min(count, _overflowBytes.Length);
            Array.Copy(_overflowBytes, 0, buffer, offset, overflowReturnCount);
            _overflowBytes = _overflowBytes.Skip(overflowReturnCount).ToArray();
            int bytesWrittenToBuffer = overflowReturnCount;
            return bytesWrittenToBuffer;
        }

        private int WriteToBuffer(byte[] buffer, int offset, int count, int bytesWrittenToBuffer)
        {
            byte[] bytes = _outputEncoding.GetBytes(_sb.ToString());
            int bytesToReturn = Math.Min(bytes.Length, count - bytesWrittenToBuffer);
            Array.Copy(bytes, 0, buffer, offset + bytesWrittenToBuffer, bytesToReturn);
            bytesWrittenToBuffer += bytesToReturn;
            if (bytesWrittenToBuffer >= count)
            {
                if (bytesWrittenToBuffer > count) throw new Exception("yooooooo");
                _overflowBytes = _overflowBytes.Concat(bytes.Skip(bytesToReturn)).ToArray();
            }
            _sb.Clear();
            return bytesWrittenToBuffer;
        }

        /// <summary>
        /// Gets the list of schema validation errors encountered during the reading of the stream.
        /// 
        /// If this has no strings, no errors was found.
        /// </summary>
        public IEnumerable<string> SchemaErrors
        {
            get
            {
                return this.errors;
            }
        }

        /// <summary>
        /// Releases all resources used by the <see cref="SchemaValidatingStream"/>.
        /// </summary>
        public new void Dispose()
        {
            _validatingReader.Close();
            _inputStream.Dispose();
            base.Dispose();
        }

        private void _validatingReader_ValidationEventHandler(object sender, SchemaValidationEventArgs e) => errors.Add(e.Message);

        // Stream required, but un-implemented (we only support read)

        public override bool CanRead => _inputStream.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length { get => throw new NotImplementedException(); }

        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override void Flush() => throw new NotImplementedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotImplementedException();

        public override void SetLength(long value) => throw new NotImplementedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();
    }
}

