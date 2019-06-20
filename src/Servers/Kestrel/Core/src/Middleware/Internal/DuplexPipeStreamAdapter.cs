using System;
using System.IO;
using System.IO.Pipelines;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Server.Kestrel.Core.Internal
{
    /// <summary>
    /// A helper for wrapping a Stream decorator from an <see cref="IDuplexPipe"/>.
    /// </summary>
    /// <typeparam name="TStream"></typeparam>
    internal class DuplexPipeStreamAdapter<TStream> : DuplexPipeStream, IDuplexPipe where TStream : Stream
    {
        public DuplexPipeStreamAdapter(IDuplexPipe duplexPipe, Func<Stream, TStream> createStream) :
            this(duplexPipe, new StreamPipeReaderOptions(), new StreamPipeWriterOptions(), createStream)
        {

        }

        public DuplexPipeStreamAdapter(IDuplexPipe duplexPipe, StreamPipeReaderOptions readerOptions, StreamPipeWriterOptions writerOptions, Func<Stream, TStream> createStream) : base(duplexPipe.Input, duplexPipe.Output)
        {
            Stream = createStream(this);
            Input = PipeReader.Create(Stream, readerOptions);
            Output = PipeWriter.Create(Stream, writerOptions);
        }

        public TStream Stream { get; }

        public PipeReader Input { get; }

        public PipeWriter Output { get; }

        protected override void Dispose(bool disposing)
        {
            Input.Complete();
            Output.Complete();
            base.Dispose(disposing);
        }

        public override ValueTask DisposeAsync()
        {
            Input.Complete();
            Output.Complete();
            return base.DisposeAsync();
        }
    }
}
