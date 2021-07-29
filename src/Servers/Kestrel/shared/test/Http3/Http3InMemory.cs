// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net.Http;
using System.Net.Http.QPack;
using System.Text;
using System.Threading.Channels;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Internal;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http3;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using static System.IO.Pipelines.DuplexPipe;
using Http3SettingType = Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http3.Http3SettingType;

namespace Microsoft.AspNetCore.Testing
{
    internal class Http3InMemory
    {
        protected static readonly int MaxRequestHeaderFieldSize = 16 * 1024;
        protected static readonly string _4kHeaderValue = new string('a', 4096);
        protected static readonly byte[] _helloWorldBytes = Encoding.ASCII.GetBytes("hello, world");
        protected static readonly byte[] _maxData = Encoding.ASCII.GetBytes(new string('a', 16 * 1024));

        public Http3InMemory(ServiceContext serviceContext, MockSystemClock mockSystemClock, ITimeoutHandler timeoutHandler, ILoggerFactory loggerFactory)
        {
            _serviceContext = serviceContext;
            _timeoutControl = new TimeoutControl(new TimeoutControlConnectionInvoker(this, timeoutHandler));
            _timeoutControl.Debugger = new TestDebugger();

            _mockSystemClock = mockSystemClock;

            _serverReceivedSettings = Channel.CreateUnbounded<KeyValuePair<Http3SettingType, long>>();
            Logger = loggerFactory.CreateLogger<Http3InMemory>();
        }

        private class TestDebugger : IDebugger
        {
            public bool IsAttached => false;
        }

        private class TimeoutControlConnectionInvoker : ITimeoutHandler
        {
            private readonly ITimeoutHandler _inner;
            private readonly Http3InMemory _http3;

            public TimeoutControlConnectionInvoker(Http3InMemory http3, ITimeoutHandler inner)
            {
                _http3 = http3;
                _inner = inner;
            }

            public void OnTimeout(TimeoutReason reason)
            {
                _inner.OnTimeout(reason);
                _http3._httpConnection.OnTimeout(reason);
            }
        }

        internal ServiceContext _serviceContext;
        private MockSystemClock _mockSystemClock;
        internal HttpConnection _httpConnection;
        internal readonly TimeoutControl _timeoutControl;
        internal readonly MemoryPool<byte> _memoryPool = PinnedBlockMemoryPoolFactory.Create();
        internal readonly ConcurrentQueue<TestStreamContext> _streamContextPool = new ConcurrentQueue<TestStreamContext>();
        protected Task _connectionTask;
        internal ILogger Logger { get; }

        internal readonly ConcurrentDictionary<long, Http3StreamBase> _runningStreams = new ConcurrentDictionary<long, Http3StreamBase>();
        internal readonly Channel<KeyValuePair<Http3SettingType, long>> _serverReceivedSettings;

        internal Func<TestStreamContext, Http3ControlStream> OnCreateServerControlStream { get; set; }
        private Http3ControlStream _inboundControlStream;
        private long _currentStreamId;
        internal Http3Connection Connection { get; private set; }

        internal Http3ControlStream OutboundControlStream { get; private set; }

        internal ChannelReader<KeyValuePair<Http3SettingType, long>> ServerReceivedSettingsReader => _serverReceivedSettings.Reader;

        internal TestMultiplexedConnectionContext MultiplexedConnectionContext { get; set; }

        internal long GetStreamId(long mask)
        {
            var id = (_currentStreamId << 2) | mask;

            _currentStreamId += 1;

            return id;
        }

        internal async ValueTask<Http3ControlStream> GetInboundControlStream()
        {
            if (_inboundControlStream == null)
            {
                var reader = MultiplexedConnectionContext.ToClientAcceptQueue.Reader;
#if IS_FUNCTIONAL_TESTS
                while (await reader.WaitToReadAsync().DefaultTimeout())
#else
                while (await reader.WaitToReadAsync())
#endif
                {
                    while (reader.TryRead(out var stream))
                    {
                        _inboundControlStream = stream;
                        var streamId = await stream.TryReadStreamIdAsync();

                        // -1 means stream was completed.
                        Debug.Assert(streamId == 0 || streamId == -1, "StreamId sent that was non-zero, which isn't handled by tests");

                        return _inboundControlStream;
                    }
                }
            }

            return _inboundControlStream;
        }

        internal void CloseConnectionGracefully()
        {
            MultiplexedConnectionContext.ConnectionClosingCts.Cancel();
        }

        internal Task WaitForConnectionStopAsync(long expectedLastStreamId, bool ignoreNonGoAwayFrames, Http3ErrorCode? expectedErrorCode = null)
        {
            return WaitForConnectionErrorAsync<Exception>(ignoreNonGoAwayFrames, expectedLastStreamId, expectedErrorCode: expectedErrorCode ?? 0, matchExpectedErrorMessage: null);
        }

        internal async Task WaitForConnectionErrorAsync<TException>(bool ignoreNonGoAwayFrames, long? expectedLastStreamId, Http3ErrorCode expectedErrorCode, Action<Type, string[]> matchExpectedErrorMessage = null, params string[] expectedErrorMessage)
            where TException : Exception
        {
            var frame = await _inboundControlStream.ReceiveFrameAsync();

            if (ignoreNonGoAwayFrames)
            {
                while (frame.Type != Http3FrameType.GoAway)
                {
                    frame = await _inboundControlStream.ReceiveFrameAsync();
                }
            }

            if (expectedLastStreamId != null)
            {
                VerifyGoAway(frame, expectedLastStreamId.GetValueOrDefault());
            }

            AssertConnectionError<TException>(expectedErrorCode, matchExpectedErrorMessage, expectedErrorMessage);

            // Verify HttpConnection.ProcessRequestsAsync has exited.
#if IS_FUNCTIONAL_TESTS
            await _connectionTask.DefaultTimeout();
#else
            await _connectionTask;
#endif

            // Verify server-to-client control stream has completed.
            await _inboundControlStream.ReceiveEndAsync();
        }

        internal void AssertConnectionError<TException>(Http3ErrorCode expectedErrorCode, Action<Type, string[]> matchExpectedErrorMessage = null, params string[] expectedErrorMessage) where TException : Exception
        {
            var currentError = (Http3ErrorCode)MultiplexedConnectionContext.Error;
            if (currentError != expectedErrorCode)
            {
                throw new InvalidOperationException($"Expected error code {expectedErrorCode}, got {currentError}.");
            }

            matchExpectedErrorMessage?.Invoke(typeof(TException), expectedErrorMessage);
        }

        internal void VerifyGoAway(Http3FrameWithPayload frame, long expectedLastStreamId)
        {
            AssertFrameType(frame.Type, Http3FrameType.GoAway);
            var payload = frame.Payload;
            if (!VariableLengthIntegerHelper.TryRead(payload.Span, out var streamId, out var _))
            {
                throw new InvalidOperationException("Failed to read GO_AWAY stream ID.");
            }
            if (streamId != expectedLastStreamId)
            {
                throw new InvalidOperationException($"Expected stream ID {expectedLastStreamId}, got {streamId}.");
            }
        }

        public void AdvanceClock(TimeSpan timeSpan)
        {
            var clock = _mockSystemClock;
            var endTime = clock.UtcNow + timeSpan;

            while (clock.UtcNow + Heartbeat.Interval < endTime)
            {
                clock.UtcNow += Heartbeat.Interval;
                _timeoutControl.Tick(clock.UtcNow);
            }

            clock.UtcNow = endTime;
            _timeoutControl.Tick(clock.UtcNow);
        }

        public void TriggerTick(DateTimeOffset now)
        {
            _mockSystemClock.UtcNow = now;
            Connection?.Tick(now);
        }

        public async Task InitializeConnectionAsync(RequestDelegate application)
        {
            MultiplexedConnectionContext = new TestMultiplexedConnectionContext(this);

            var httpConnectionContext = new HttpMultiplexedConnectionContext(
                connectionId: "TestConnectionId",
                HttpProtocols.Http3,
                altSvcHeader: null,
                connectionContext: MultiplexedConnectionContext,
                connectionFeatures: MultiplexedConnectionContext.Features,
                serviceContext: _serviceContext,
                memoryPool: _memoryPool,
                localEndPoint: null,
                remoteEndPoint: null);
            httpConnectionContext.TimeoutControl = _timeoutControl;

            _httpConnection = new HttpConnection(httpConnectionContext);
            _httpConnection.Initialize(Connection);

            // ProcessRequestAsync will create the Http3Connection
            _connectionTask = _httpConnection.ProcessRequestsAsync(new DummyApplication(application));

            Connection = (Http3Connection)_httpConnection._requestProcessor;
            Connection._streamLifetimeHandler = new LifetimeHandlerInterceptor(Connection, this);

            await GetInboundControlStream();
        }

        public static void AssertFrameType(Http3FrameType actual, Http3FrameType expected)
        {
            if (actual != expected)
            {
                throw new InvalidOperationException($"Expected {actual} frame. Got {expected}.");
            }
        }

        internal async ValueTask<Http3RequestStream> InitializeConnectionAndStreamsAsync(RequestDelegate application)
        {
            await InitializeConnectionAsync(application);

            OutboundControlStream = await CreateControlStream();

            return await CreateRequestStream();
        }

        private class LifetimeHandlerInterceptor : IHttp3StreamLifetimeHandler
        {
            private readonly IHttp3StreamLifetimeHandler _inner;
            private readonly Http3InMemory _http3TestBase;

            public LifetimeHandlerInterceptor(IHttp3StreamLifetimeHandler inner, Http3InMemory http3TestBase)
            {
                _inner = inner;
                _http3TestBase = http3TestBase;
            }

            public bool OnInboundControlStream(Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http3.Http3ControlStream stream)
            {
                return _inner.OnInboundControlStream(stream);
            }

            public void OnInboundControlStreamSetting(Http3SettingType type, long value)
            {
                _inner.OnInboundControlStreamSetting(type, value);

                var success = _http3TestBase._serverReceivedSettings.Writer.TryWrite(
                    new KeyValuePair<Http3SettingType, long>(type, value));
                Debug.Assert(success);
            }

            public bool OnInboundDecoderStream(Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http3.Http3ControlStream stream)
            {
                return _inner.OnInboundDecoderStream(stream);
            }

            public bool OnInboundEncoderStream(Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http3.Http3ControlStream stream)
            {
                return _inner.OnInboundEncoderStream(stream);
            }

            public void OnStreamCompleted(IHttp3Stream stream)
            {
                _inner.OnStreamCompleted(stream);

                if (_http3TestBase._runningStreams.TryRemove(stream.StreamId, out var testStream))
                {
                    testStream._onStreamCompletedTcs.TrySetResult();
                }
            }

            public void OnStreamConnectionError(Http3ConnectionErrorException ex)
            {
                _inner.OnStreamConnectionError(ex);
            }

            public void OnStreamCreated(IHttp3Stream stream)
            {
                _inner.OnStreamCreated(stream);

                if (_http3TestBase._runningStreams.TryGetValue(stream.StreamId, out var testStream))
                {
                    testStream._onStreamCreatedTcs.TrySetResult();
                }
            }

            public void OnStreamHeaderReceived(IHttp3Stream stream)
            {
                _inner.OnStreamHeaderReceived(stream);

                if (_http3TestBase._runningStreams.TryGetValue(stream.StreamId, out var testStream))
                {
                    testStream._onHeaderReceivedTcs.TrySetResult();
                }
            }
        }

        protected void ConnectionClosed()
        {

        }

        public static PipeOptions GetInputPipeOptions(ServiceContext serviceContext, MemoryPool<byte> memoryPool, PipeScheduler writerScheduler) => new PipeOptions
        (
          pool: memoryPool,
          readerScheduler: serviceContext.Scheduler,
          writerScheduler: writerScheduler,
          pauseWriterThreshold: serviceContext.ServerOptions.Limits.MaxRequestBufferSize ?? 0,
          resumeWriterThreshold: serviceContext.ServerOptions.Limits.MaxRequestBufferSize ?? 0,
          useSynchronizationContext: false,
          minimumSegmentSize: memoryPool.GetMinimumSegmentSize()
        );

        public static PipeOptions GetOutputPipeOptions(ServiceContext serviceContext, MemoryPool<byte> memoryPool, PipeScheduler readerScheduler) => new PipeOptions
        (
            pool: memoryPool,
            readerScheduler: readerScheduler,
            writerScheduler: serviceContext.Scheduler,
            pauseWriterThreshold: GetOutputResponseBufferSize(serviceContext),
            resumeWriterThreshold: GetOutputResponseBufferSize(serviceContext),
            useSynchronizationContext: false,
            minimumSegmentSize: memoryPool.GetMinimumSegmentSize()
        );

        private static long GetOutputResponseBufferSize(ServiceContext serviceContext)
        {
            var bufferSize = serviceContext.ServerOptions.Limits.MaxResponseBufferSize;
            if (bufferSize == 0)
            {
                // 0 = no buffering so we need to configure the pipe so the writer waits on the reader directly
                return 1;
            }

            // null means that we have no back pressure
            return bufferSize ?? 0;
        }

        internal ValueTask<Http3ControlStream> CreateControlStream()
        {
            return CreateControlStream(id: 0);
        }

        internal async ValueTask<Http3ControlStream> CreateControlStream(int? id)
        {
            var testStreamContext = new TestStreamContext(canRead: true, canWrite: false, this);
            testStreamContext.Initialize(GetStreamId(0x02));

            var stream = new Http3ControlStream(this, testStreamContext);
            _runningStreams[stream.StreamId] = stream;

            MultiplexedConnectionContext.ToServerAcceptQueue.Writer.TryWrite(stream.StreamContext);
            if (id != null)
            {
                await stream.WriteStreamIdAsync(id.GetValueOrDefault());
            }
            return stream;
        }

        internal ValueTask<Http3RequestStream> CreateRequestStream(Http3RequestHeaderHandler headerHandler = null)
        {
            if (!_streamContextPool.TryDequeue(out var testStreamContext))
            {
                testStreamContext = new TestStreamContext(canRead: true, canWrite: true, this);
            }
            testStreamContext.Initialize(GetStreamId(0x00));

            var stream = new Http3RequestStream(this, Connection, testStreamContext, headerHandler ?? new Http3RequestHeaderHandler());
            _runningStreams[stream.StreamId] = stream;

            MultiplexedConnectionContext.ToServerAcceptQueue.Writer.TryWrite(stream.StreamContext);
            return new ValueTask<Http3RequestStream>(stream);
        }
    }

    internal class Http3StreamBase : IProtocolErrorCodeFeature
    {
        internal TaskCompletionSource _onStreamCreatedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource _onStreamCompletedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource _onHeaderReceivedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        internal ConnectionContext StreamContext { get; }
        internal IProtocolErrorCodeFeature _protocolErrorCodeFeature;
        internal DuplexPipe.DuplexPipePair _pair;
        internal Http3InMemory _testBase;
        internal Http3Connection _connection;
        public long BytesReceived { get; private set; }
        public long Error
        {
            get => _protocolErrorCodeFeature.Error;
            set => _protocolErrorCodeFeature.Error = value;
        }

        public Task OnStreamCreatedTask => _onStreamCreatedTcs.Task;
        public Task OnStreamCompletedTask => _onStreamCompletedTcs.Task;
        public Task OnHeaderReceivedTask => _onHeaderReceivedTcs.Task;

        public Http3StreamBase(TestStreamContext testStreamContext)
        {
            StreamContext = testStreamContext;
            _protocolErrorCodeFeature = testStreamContext;
            _pair = testStreamContext._pair;
        }

        protected Task SendAsync(ReadOnlySpan<byte> span)
        {
            var writableBuffer = _pair.Application.Output;
            writableBuffer.Write(span);
            return FlushAsync(writableBuffer);
        }

        protected static Task FlushAsync(PipeWriter writableBuffer)
        {
            var task = writableBuffer.FlushAsync();
#if IS_FUNCTIONAL_TESTS
            return task.AsTask().DefaultTimeout();
#else
            return task.GetAsTask();
#endif
        }

        internal async Task ReceiveEndAsync()
        {
            var result = await ReadApplicationInputAsync();
            if (!result.IsCompleted)
            {
                throw new InvalidOperationException("End not received.");
            }
        }

#if IS_FUNCTIONAL_TESTS
        protected Task<ReadResult> ReadApplicationInputAsync()
        {
            return _pair.Application.Input.ReadAsync().AsTask().DefaultTimeout();
        }
#else
        protected ValueTask<ReadResult> ReadApplicationInputAsync()
        {
            return _pair.Application.Input.ReadAsync();
        }
#endif

        internal async ValueTask<Http3FrameWithPayload> ReceiveFrameAsync(bool expectEnd = false, bool allowEnd = false, Http3FrameWithPayload frame = null)
        {
            frame ??= new Http3FrameWithPayload();

            while (true)
            {
                var result = await ReadApplicationInputAsync();
                var buffer = result.Buffer;
                var consumed = buffer.Start;
                var examined = buffer.Start;
                var copyBuffer = buffer;

                try
                {
                    if (buffer.Length == 0)
                    {
                        if (result.IsCompleted && allowEnd)
                        {
                            return null;
                        }

                        throw new InvalidOperationException("No data received.");
                    }

                    if (Http3FrameReader.TryReadFrame(ref buffer, frame, out var framePayload))
                    {
                        consumed = examined = framePayload.End;
                        frame.Payload = framePayload.ToArray();

                        if (expectEnd)
                        {
                            if (!result.IsCompleted || buffer.Length > 0)
                            {
                                throw new Exception("Reader didn't complete with frame");
                            }
                        }

                        return frame;
                    }
                    else
                    {
                        examined = buffer.End;
                    }

                    if (result.IsCompleted)
                    {
                        throw new IOException("The reader completed without returning a frame.");
                    }
                }
                finally
                {
                    BytesReceived += copyBuffer.Slice(copyBuffer.Start, consumed).Length;
                    _pair.Application.Input.AdvanceTo(consumed, examined);
                }
            }
        }

        internal async Task SendFrameAsync(Http3FrameType frameType, Memory<byte> data, bool endStream = false)
        {
            var outputWriter = _pair.Application.Output;
            Http3FrameWriter.WriteHeader(frameType, data.Length, outputWriter);

            if (!endStream)
            {
                await SendAsync(data.Span);
            }
            else
            {
                // Write and end stream at the same time.
                // Avoid race condition of frame read separately from end of stream.
                await EndStreamAsync(data.Span);
            }
        }

        internal Task EndStreamAsync(ReadOnlySpan<byte> span = default)
        {
            var writableBuffer = _pair.Application.Output;
            if (span.Length > 0)
            {
                writableBuffer.Write(span);
            }
            return writableBuffer.CompleteAsync().AsTask();
        }

        internal async Task WaitForStreamErrorAsync(Http3ErrorCode protocolError, Action<string> matchExpectedErrorMessage = null, string expectedErrorMessage = null)
        {
            var result = await ReadApplicationInputAsync();
            if (!result.IsCompleted)
            {
                throw new InvalidOperationException("Stream not ended.");
            }
            if ((Http3ErrorCode)Error != protocolError)
            {
                throw new InvalidOperationException($"Expected error code {protocolError}, got {(Http3ErrorCode)Error}.");
            }

            matchExpectedErrorMessage?.Invoke(expectedErrorMessage);
        }
    }

    internal class Http3RequestHeaderHandler
    {
        public readonly byte[] HeaderEncodingBuffer = new byte[64 * 1024];
        public readonly QPackDecoder QpackDecoder = new QPackDecoder(8192);
        public readonly Dictionary<string, string> DecodedHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    internal class Http3RequestStream : Http3StreamBase, IHttpHeadersHandler
    {
        private readonly TestStreamContext _testStreamContext;
        private readonly Http3RequestHeaderHandler _headerHandler;
        private readonly long _streamId;

        public bool CanRead => true;
        public bool CanWrite => true;

        public long StreamId => _streamId;

        public bool Disposed => _testStreamContext.Disposed;
        public Task OnDisposedTask => _testStreamContext.OnDisposedTask;

        public Http3RequestStream(Http3InMemory testBase, Http3Connection connection, TestStreamContext testStreamContext, Http3RequestHeaderHandler headerHandler)
            : base(testStreamContext)
        {
            _testBase = testBase;
            _connection = connection;
            _streamId = testStreamContext.StreamId;
            _testStreamContext = testStreamContext;
            this._headerHandler = headerHandler;
        }

        public Task SendHeadersAsync(IEnumerable<KeyValuePair<string, string>> headers, bool endStream = false)
        {
            return SendHeadersAsync(GetHeadersEnumerator(headers), endStream);
        }

        public async Task SendHeadersAsync(Http3HeadersEnumerator headers, bool endStream = false)
        {
            var headersTotalSize = 0;

            var buffer = _headerHandler.HeaderEncodingBuffer.AsMemory();
            var done = QPackHeaderWriter.BeginEncode(headers, buffer.Span, ref headersTotalSize, out var length);
            if (!done)
            {
                throw new InvalidOperationException("Headers not sent.");
            }

            await SendFrameAsync(Http3FrameType.Headers, buffer.Slice(0, length), endStream);
        }

        internal Http3HeadersEnumerator GetHeadersEnumerator(IEnumerable<KeyValuePair<string, string>> headers)
        {
            var dictionary = headers
                .GroupBy(g => g.Key)
                .ToDictionary(g => g.Key, g => new StringValues(g.Select(values => values.Value).ToArray()));

            var headersEnumerator = new Http3HeadersEnumerator();
            headersEnumerator.Initialize(dictionary);
            return headersEnumerator;
        }

        internal async Task SendHeadersPartialAsync()
        {
            // Send HEADERS frame header without content.
            var outputWriter = _pair.Application.Output;
            Http3FrameWriter.WriteHeader(Http3FrameType.Data, frameLength: 10, outputWriter);
            await SendAsync(Span<byte>.Empty);
        }

        internal async Task SendDataAsync(Memory<byte> data, bool endStream = false)
        {
            await SendFrameAsync(Http3FrameType.Data, data, endStream);
        }

        internal async ValueTask<Dictionary<string, string>> ExpectHeadersAsync(bool expectEnd = false)
        {
            var http3WithPayload = await ReceiveFrameAsync(expectEnd);
            Http3InMemory.AssertFrameType(http3WithPayload.Type, Http3FrameType.Headers);

            _headerHandler.DecodedHeaders.Clear();
            _headerHandler.QpackDecoder.Decode(http3WithPayload.PayloadSequence, this);
            _headerHandler.QpackDecoder.Reset();
            return _headerHandler.DecodedHeaders.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, _headerHandler.DecodedHeaders.Comparer);
        }

        internal async ValueTask<Memory<byte>> ExpectDataAsync()
        {
            var http3WithPayload = await ReceiveFrameAsync();
            return http3WithPayload.Payload;
        }

        internal async Task ExpectReceiveEndOfStream()
        {
            var result = await ReadApplicationInputAsync();
            if (!result.IsCompleted)
            {
                throw new InvalidOperationException("End of stream not received.");
            }
        }

        public void OnHeader(ReadOnlySpan<byte> name, ReadOnlySpan<byte> value)
        {
            _headerHandler.DecodedHeaders[name.GetAsciiStringNonNullCharacters()] = value.GetAsciiOrUTF8StringNonNullCharacters();
        }

        public void OnHeadersComplete(bool endHeaders)
        {
        }

        public void OnStaticIndexedHeader(int index)
        {
            var knownHeader = H3StaticTable.GetHeaderFieldAt(index);
            _headerHandler.DecodedHeaders[((Span<byte>)knownHeader.Name).GetAsciiStringNonNullCharacters()] = HttpUtilities.GetAsciiOrUTF8StringNonNullCharacters((ReadOnlySpan<byte>)knownHeader.Value);
        }

        public void OnStaticIndexedHeader(int index, ReadOnlySpan<byte> value)
        {
            _headerHandler.DecodedHeaders[((Span<byte>)H3StaticTable.GetHeaderFieldAt(index).Name).GetAsciiStringNonNullCharacters()] = value.GetAsciiOrUTF8StringNonNullCharacters();
        }

        public void Complete()
        {
            _testStreamContext.Complete();
        }
    }

    internal class Http3FrameWithPayload : Http3RawFrame
    {
        public Http3FrameWithPayload() : base()
        {
        }

        // This does not contain extended headers
        public Memory<byte> Payload { get; set; }

        public ReadOnlySequence<byte> PayloadSequence => new ReadOnlySequence<byte>(Payload);
    }

    public enum StreamInitiator
    {
        Client,
        Server
    }

    internal class Http3ControlStream : Http3StreamBase
    {
        private readonly long _streamId;

        public bool CanRead => true;
        public bool CanWrite => false;

        public long StreamId => _streamId;

        public Http3ControlStream(Http3InMemory testBase, TestStreamContext testStreamContext)
            : base(testStreamContext)
        {
            _testBase = testBase;
            _streamId = testStreamContext.StreamId;
        }

        internal async ValueTask<Dictionary<long, long>> ExpectSettingsAsync()
        {
            var http3WithPayload = await ReceiveFrameAsync();
            Http3InMemory.AssertFrameType(http3WithPayload.Type, Http3FrameType.Settings);

            var payload = http3WithPayload.PayloadSequence;

            var settings = new Dictionary<long, long>();
            while (true)
            {
                var id = VariableLengthIntegerHelper.GetInteger(payload, out var consumed, out _);
                if (id == -1)
                {
                    break;
                }

                payload = payload.Slice(consumed);

                var value = VariableLengthIntegerHelper.GetInteger(payload, out consumed, out _);
                if (value == -1)
                {
                    break;
                }

                payload = payload.Slice(consumed);
                settings.Add(id, value);
            }

            return settings;
        }

        public async Task WriteStreamIdAsync(int id)
        {
            var writableBuffer = _pair.Application.Output;

            void WriteSpan(PipeWriter pw)
            {
                var buffer = pw.GetSpan(sizeHint: 8);
                var lengthWritten = VariableLengthIntegerHelper.WriteInteger(buffer, id);
                pw.Advance(lengthWritten);
            }

            WriteSpan(writableBuffer);

            await FlushAsync(writableBuffer);
        }

        internal async Task SendGoAwayAsync(long streamId, bool endStream = false)
        {
            var data = new byte[VariableLengthIntegerHelper.GetByteCount(streamId)];
            VariableLengthIntegerHelper.WriteInteger(data, streamId);

            await SendFrameAsync(Http3FrameType.GoAway, data, endStream);
        }

        internal async Task SendSettingsAsync(List<Http3PeerSetting> settings, bool endStream = false)
        {
            var settingsLength = CalculateSettingsSize(settings);
            var buffer = new byte[settingsLength];
            WriteSettings(settings, buffer);

            await SendFrameAsync(Http3FrameType.Settings, buffer, endStream);
        }

        internal static int CalculateSettingsSize(List<Http3PeerSetting> settings)
        {
            var length = 0;
            foreach (var setting in settings)
            {
                length += VariableLengthIntegerHelper.GetByteCount((long)setting.Parameter);
                length += VariableLengthIntegerHelper.GetByteCount(setting.Value);
            }
            return length;
        }

        internal static void WriteSettings(List<Http3PeerSetting> settings, Span<byte> destination)
        {
            foreach (var setting in settings)
            {
                var parameterLength = VariableLengthIntegerHelper.WriteInteger(destination, (long)setting.Parameter);
                destination = destination.Slice(parameterLength);

                var valueLength = VariableLengthIntegerHelper.WriteInteger(destination, (long)setting.Value);
                destination = destination.Slice(valueLength);
            }
        }

        public async ValueTask<long> TryReadStreamIdAsync()
        {
            while (true)
            {
                var result = await ReadApplicationInputAsync();
                var readableBuffer = result.Buffer;
                var consumed = readableBuffer.Start;
                var examined = readableBuffer.End;

                try
                {
                    if (!readableBuffer.IsEmpty)
                    {
                        var id = VariableLengthIntegerHelper.GetInteger(readableBuffer, out consumed, out examined);
                        if (id != -1)
                        {
                            return id;
                        }
                    }

                    if (result.IsCompleted)
                    {
                        return -1;
                    }
                }
                finally
                {
                    _pair.Application.Input.AdvanceTo(consumed, examined);
                }
            }
        }
    }

    internal class TestMultiplexedConnectionContext : MultiplexedConnectionContext, IConnectionLifetimeNotificationFeature, IConnectionLifetimeFeature, IConnectionHeartbeatFeature, IProtocolErrorCodeFeature
    {
        public readonly Channel<ConnectionContext> ToServerAcceptQueue = Channel.CreateUnbounded<ConnectionContext>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });

        public readonly Channel<Http3ControlStream> ToClientAcceptQueue = Channel.CreateUnbounded<Http3ControlStream>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });

        private readonly Http3InMemory _testBase;
        private long _error;

        public TestMultiplexedConnectionContext(Http3InMemory testBase)
        {
            _testBase = testBase;
            Features = new FeatureCollection();
            Features.Set<IConnectionLifetimeNotificationFeature>(this);
            Features.Set<IConnectionHeartbeatFeature>(this);
            Features.Set<IProtocolErrorCodeFeature>(this);
            ConnectionClosedRequested = ConnectionClosingCts.Token;
        }

        public override string ConnectionId { get; set; }

        public override IFeatureCollection Features { get; }

        public override IDictionary<object, object> Items { get; set; }

        public CancellationToken ConnectionClosedRequested { get; set; }

        public CancellationTokenSource ConnectionClosingCts { get; set; } = new CancellationTokenSource();

        public long Error
        {
            get => _error;
            set => _error = value;
        }

        public override void Abort()
        {
            Abort(new ConnectionAbortedException());
        }

        public override void Abort(ConnectionAbortedException abortReason)
        {
            ToServerAcceptQueue.Writer.TryComplete();
            ToClientAcceptQueue.Writer.TryComplete();
        }

        public override async ValueTask<ConnectionContext> AcceptAsync(CancellationToken cancellationToken = default)
        {
            while (await ToServerAcceptQueue.Reader.WaitToReadAsync())
            {
                while (ToServerAcceptQueue.Reader.TryRead(out var connection))
                {
                    return connection;
                }
            }

            return null;
        }

        public override ValueTask<ConnectionContext> ConnectAsync(IFeatureCollection features = null, CancellationToken cancellationToken = default)
        {
            var testStreamContext = new TestStreamContext(canRead: true, canWrite: false, _testBase);
            testStreamContext.Initialize(_testBase.GetStreamId(0x03));

            var stream = _testBase.OnCreateServerControlStream?.Invoke(testStreamContext) ?? new Http3ControlStream(_testBase, testStreamContext);
            ToClientAcceptQueue.Writer.WriteAsync(stream);
            return new ValueTask<ConnectionContext>(stream.StreamContext);
        }

        public void OnHeartbeat(Action<object> action, object state)
        {
        }

        public void RequestClose()
        {
            throw new NotImplementedException();
        }
    }

    internal class TestStreamContext : ConnectionContext, IStreamDirectionFeature, IStreamIdFeature, IProtocolErrorCodeFeature, IPersistentStateFeature
    {
        private readonly Http3InMemory _testBase;

        internal DuplexPipePair _pair;
        private Pipe _inputPipe;
        private Pipe _outputPipe;
        private CompletionPipeReader _transportPipeReader;
        private CompletionPipeWriter _transportPipeWriter;

        private bool _isAborted;
        private bool _isComplete;

        // Persistent state collection is not reset with a stream by design.
        private IDictionary<object, object> _persistentState;

        private TaskCompletionSource _disposedTcs;

        public TestStreamContext(bool canRead, bool canWrite, Http3InMemory testBase)
        {
            Features = new FeatureCollection();
            CanRead = canRead;
            CanWrite = canWrite;
            _testBase = testBase;
        }

        public void Initialize(long streamId)
        {
            if (!_isComplete)
            {
                // Create new pipes when test stream context is reused rather than reseting them.
                // This is required because the client tests read from these directly from these pipes.
                // When a request is finished they'll check to see whether there is anymore content
                // in the Application.Output pipe. If it has been reset then that code will error.
                var inputOptions = Http3InMemory.GetInputPipeOptions(_testBase._serviceContext, _testBase._memoryPool, PipeScheduler.ThreadPool);
                var outputOptions = Http3InMemory.GetOutputPipeOptions(_testBase._serviceContext, _testBase._memoryPool, PipeScheduler.ThreadPool);

                _inputPipe = new Pipe(inputOptions);
                _outputPipe = new Pipe(outputOptions);

                _transportPipeReader = new CompletionPipeReader(_inputPipe.Reader);
                _transportPipeWriter = new CompletionPipeWriter(_outputPipe.Writer);

                _pair = new DuplexPipePair(
                    new DuplexPipe(_transportPipeReader, _transportPipeWriter),
                    new DuplexPipe(_outputPipe.Reader, _inputPipe.Writer));
            }
            else
            {
                _pair.Application.Input.Complete();
                _pair.Application.Output.Complete();

                _transportPipeReader.Reset();
                _transportPipeWriter.Reset();

                _inputPipe.Reset();
                _outputPipe.Reset();
            }

            Features.Set<IStreamDirectionFeature>(this);
            Features.Set<IStreamIdFeature>(this);
            Features.Set<IProtocolErrorCodeFeature>(this);
            Features.Set<IPersistentStateFeature>(this);

            StreamId = streamId;
            _testBase.Logger.LogInformation($"Initializing stream {streamId}");
            ConnectionId = "TEST:" + streamId.ToString();

            _disposedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Disposed = false;
        }

        public bool Disposed { get; private set; }

        public Task OnDisposedTask => _disposedTcs.Task;

        public override string ConnectionId { get; set; }

        public long StreamId { get; private set; }

        public override IFeatureCollection Features { get; }

        public override IDictionary<object, object> Items { get; set; }

        public override IDuplexPipe Transport
        {
            get
            {
                return _pair.Transport;
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public bool CanRead { get; }

        public bool CanWrite { get; }

        public long Error { get; set; }

        public override void Abort(ConnectionAbortedException abortReason)
        {
            _isAborted = true;
            _pair.Application.Output.Complete(abortReason);
        }

        public override ValueTask DisposeAsync()
        {
            _testBase.Logger.LogInformation($"Disposing stream {StreamId}");

            Disposed = true;
            _disposedTcs.TrySetResult();

            if (!_isAborted &&
                _transportPipeReader.IsCompletedSuccessfully &&
                _transportPipeWriter.IsCompletedSuccessfully)
            {
                _testBase._streamContextPool.Enqueue(this);
            }

            return ValueTask.CompletedTask;
        }

        internal void Complete()
        {
            _isComplete = true;
        }

        IDictionary<object, object> IPersistentStateFeature.State
        {
            get
            {
                // Lazily allocate persistent state
                return _persistentState ?? (_persistentState = new ConnectionItems());
            }
        }
    }
}
