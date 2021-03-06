// ==++== 
//
//   Copyright (c) Microsoft Corporation.  All rights reserved.
//
// ==--== 
/*============================================================
** 
** Class:  StreamWriter 
**
** <OWNER>[....]</OWNER> 
**
**
** Purpose: For writing text to streams in a particular
** encoding. 
**
** 
===========================================================*/ 
using System;
using System.Text; 
using System.Threading;
using System.Globalization;
using System.Runtime.Versioning;
using System.Runtime.Serialization; 
using System.Diagnostics.Contracts;
 
namespace System.IO { 
    // This class implements a TextWriter for writing characters to a Stream.
    // This is designed for character output in a particular Encoding, 
    // whereas the Stream class is designed for byte input and output.
    //
    [Serializable]
    [System.Runtime.InteropServices.ComVisible(true)] 
    public class StreamWriter : TextWriter
    { 
        // For UTF-8, the values of 1K for the default buffer size and 4K for the 
        // file stream buffer size are reasonable & give very reasonable
        // performance for in terms of construction time for the StreamWriter and 
        // write perf.  Note that for UTF-8, we end up allocating a 4K byte buffer,
        // which means we take advantage of adaptive buffering code.
        // The performance using UnicodeEncoding is acceptable.
        private const int DefaultBufferSize = 1024;   // char[] 
        private const int DefaultFileStreamBufferSize = 4096;
        private const int MinBufferSize = 128; 
 
        // Bit bucket - Null has no backing store. Non closable.
        public new static readonly StreamWriter Null = new StreamWriter(Stream.Null, new UTF8Encoding(false, true), MinBufferSize, false); 

        internal Stream stream;
        private Encoding encoding;
        private Encoder encoder; 
        internal byte[] byteBuffer;
        internal char[] charBuffer; 
        internal int charPos; 
        internal int charLen;
        internal bool autoFlush; 
        private bool haveWrittenPreamble;
        private bool closable;  // For Console.Out - should Finalize call Dispose?

#if MDA_SUPPORTED 
        [NonSerialized]
        // For StreamWriterBufferedDataLost MDA 
        private MdaHelper mdaHelper; 
#endif
        // The high level goal is to be tolerant of encoding errors when we read and very strict 
        // when we write. Hence, default StreamWriter encoding will throw on encoding error.
        // Note: when StreamWriter throws on invalid encoding chars (for ex, high surrogate character
        // D800-DBFF without a following low surrogate character DC00-DFFF), it will cause the
        // internal StreamWriter's state to be irrecoverable as it would have buffered the 
        // illegal chars and any subsequent call to Flush() would hit the encoding error again.
        // Even Close() will hit the exception as it would try to flush the unwritten data. 
        // May be we can add a DiscardBufferedData() method to get out of such situation (like 
        // StreamRerader though for different reason). Eitherway, the buffered data will be lost!
        private static Encoding _UTF8NoBOM; 

        internal static Encoding UTF8NoBOM {
            get {
                if (_UTF8NoBOM == null) { 
                    // No need for double lock - we just want to avoid extra
                    // allocations in the common case. 
                    UTF8Encoding noBOM = new UTF8Encoding(false, true); 
                    Thread.MemoryBarrier();
                    _UTF8NoBOM = noBOM; 
                }
                return _UTF8NoBOM;
            }
        } 

 
        internal StreamWriter(): base(null) { // Ask for CurrentCulture all the time 
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated
        public StreamWriter(Stream stream)
            : this(stream, UTF8NoBOM, DefaultBufferSize) {
        } 

        [System.Security.SecuritySafeCritical]  // auto-generated 
        public StreamWriter(Stream stream, Encoding encoding) 
            : this(stream, encoding, DefaultBufferSize) {
        } 

        // Creates a new StreamWriter for the given stream.  The
        // character encoding is set by encoding and the buffer size,
        // in number of 16-bit characters, is set by bufferSize. 
        //
        [System.Security.SecuritySafeCritical]  // auto-generated 
        public StreamWriter(Stream stream, Encoding encoding, int bufferSize): base(null) { // Ask for CurrentCulture all the time 
            if (stream==null || encoding==null)
                throw new ArgumentNullException((stream==null ? "stream" : "encoding")); 
            if (!stream.CanWrite)
                throw new ArgumentException(Environment.GetResourceString("Argument_StreamNotWritable"));
            if (bufferSize <= 0) throw new ArgumentOutOfRangeException("bufferSize", Environment.GetResourceString("ArgumentOutOfRange_NeedPosNum"));
            Contract.EndContractBlock(); 

            Init(stream, encoding, bufferSize); 
        } 

        // For non closable streams such as Console.Out 
        internal StreamWriter(Stream stream, Encoding encoding, int bufferSize, bool closeable)
            : this(stream, encoding, bufferSize) {
            closable = closeable;
        } 

        [System.Security.SecuritySafeCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.Machine)] 
        [ResourceConsumption(ResourceScope.Machine)]
        public StreamWriter(String path) 
            : this(path, false, UTF8NoBOM, DefaultBufferSize) {
        }

        [System.Security.SecuritySafeCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)] 
        public StreamWriter(String path, bool append) 
            : this(path, append, UTF8NoBOM, DefaultBufferSize) {
        } 

        [System.Security.SecuritySafeCritical]  // auto-generated
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)] 
        public StreamWriter(String path, bool append, Encoding encoding)
            : this(path, append, encoding, DefaultBufferSize) { 
        } 

        [System.Security.SecuritySafeCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        public StreamWriter(String path, bool append, Encoding encoding, int bufferSize): base(null) { // Ask for CurrentCulture all the time
            if (path==null) 
                throw new ArgumentNullException("path");
            if (encoding == null) 
                throw new ArgumentNullException("encoding"); 
            if (path.Length == 0)
                throw new ArgumentException(Environment.GetResourceString("Argument_EmptyPath")); 
            if (bufferSize <= 0) throw new ArgumentOutOfRangeException("bufferSize", Environment.GetResourceString("ArgumentOutOfRange_NeedPosNum"));
            Contract.EndContractBlock();

            Stream stream = CreateFile(path, append); 
            Init(stream, encoding, bufferSize);
        } 
 
        private void Init(Stream stream, Encoding encoding, int bufferSize)
        { 
            this.stream = stream;
            this.encoding = encoding;
            this.encoder = encoding.GetEncoder();
            if (bufferSize < MinBufferSize) bufferSize = MinBufferSize; 
            charBuffer = new char[bufferSize];
            byteBuffer = new byte[encoding.GetMaxByteCount(bufferSize)]; 
            charLen = bufferSize; 
            // If we're appending to a Stream that already has data, don't write
            // the preamble. 
            if (stream.CanSeek && stream.Position > 0)
                haveWrittenPreamble = true;
            closable = true;
#if MDA_SUPPORTED 
            if (Mda.StreamWriterBufferedDataLost.Enabled) {
                String callstack = null; 
                if (Mda.StreamWriterBufferedDataLost.CaptureAllocatedCallStack) 
                    callstack = Environment.GetStackTrace(null, false);
                mdaHelper = new MdaHelper(this, callstack); 
            }
#endif
        }
 
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)] 
        private static Stream CreateFile(String path, bool append) { 
            FileMode mode = append? FileMode.Append: FileMode.Create;
            FileStream f = new FileStream(path, mode, FileAccess.Write, FileShare.Read, DefaultFileStreamBufferSize, FileOptions.SequentialScan); 
            return f;
        }

        public override void Close() { 
            Dispose(true);
            GC.SuppressFinalize(this); 
        } 

        protected override void Dispose(bool disposing) { 
            try {
                // We need to flush any buffered data if we are being closed/disposed.
                // Also, we never close the handles for stdout & friends.  So we can safely
                // write any buffered data to those streams even during finalization, which 
                // is generally the right thing to do.
                if (stream != null) { 
                    // Note: flush on the underlying stream can throw (ex., low disk space) 
                    if (disposing || (!Closable && stream is __ConsoleStream)) {
                        Flush(true, true); 
#if MDA_SUPPORTED
                        // Disable buffered data loss mda
                        if (mdaHelper != null)
                            GC.SuppressFinalize(mdaHelper); 
#endif
                    } 
                } 
            }
            finally { 
                // Dispose of our resources if this StreamWriter is closable.
                // Note: Console.Out and other such non closable streamwriters should be left alone
                if (Closable && stream != null) {
                    try { 
                        // Attempt to close the stream even if there was an IO error from Flushing.
                        // Note that Stream.Close() can potentially throw here (may or may not be 
                        // due to the same Flush error). In this case, we still need to ensure 
                        // cleaning up internal resources, hence the finally block.
                        if (disposing) 
                            stream.Close();
                    }
                    finally {
                        stream = null; 
                        byteBuffer = null;
                        charBuffer = null; 
                        encoding = null; 
                        encoder = null;
                        charLen = 0; 
                        base.Dispose(disposing);
                    }
                }
            } 
        }
 
        public override void Flush() { 
            Flush(true, true);
        } 

        private void Flush(bool flushStream, bool flushEncoder) {
            // flushEncoder should be true at the end of the file and if
            // the user explicitly calls Flush (though not if AutoFlush is true). 
            // This is required to flush any dangling characters from our UTF-7
            // and UTF-8 encoders. 
            if (stream == null) 
                __Error.WriterClosed();
 
            // Perf boost for Flush on non-dirty writers.
            if (charPos==0 && !flushStream && !flushEncoder)
                return;
 
            if (!haveWrittenPreamble) {
                haveWrittenPreamble = true; 
                byte[] preamble = encoding.GetPreamble(); 
                if (preamble.Length > 0)
                    stream.Write(preamble, 0, preamble.Length); 
            }

            int count = encoder.GetBytes(charBuffer, 0, charPos, byteBuffer, 0, flushEncoder);
            charPos = 0; 
            if (count > 0)
                stream.Write(byteBuffer, 0, count); 
            // By definition, calling Flush should flush the stream, but this is 
            // only necessary if we passed in true for flushStream.  The Web
            // Services guys have some perf tests where flushing needlessly hurts. 
            if (flushStream)
                stream.Flush();
        }
 
        public virtual bool AutoFlush {
            get { return autoFlush; } 
            set { 
                autoFlush = value;
                if (value) Flush(true, false); 
            }
        }

        public virtual Stream BaseStream { 
            get { return stream; }
        } 
 
        internal bool Closable {
            get { return closable; } 
            //set { closable = value; }
        }

        internal bool HaveWrittenPreamble { 
            set { haveWrittenPreamble= value; }
        } 
 
        public override Encoding Encoding {
            get { return encoding; } 
        }

        public override void Write(char value) {
            if (charPos == charLen) Flush(false, false); 
            charBuffer[charPos] = value;
            charPos++; 
            if (autoFlush) Flush(true, false); 
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated
        public override void Write(char[] buffer)
        {
            // This may be faster than the one with the index & count since it 
            // has to do less argument checking.
            if (buffer==null) 
                return; 
            int index = 0;
            int count = buffer.Length; 
            while (count > 0) {
                if (charPos == charLen) Flush(false, false);
                int n = charLen - charPos;
                if (n > count) n = count; 
                Contract.Assert(n > 0, "StreamWriter::Write(char[]) isn't making progress!  This is most likely a ---- in user code.");
                Buffer.InternalBlockCopy(buffer, index*2, charBuffer, charPos*2, n*2); 
                charPos += n; 
                index += n;
                count -= n; 
            }
            if (autoFlush) Flush(true, false);
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated
        public override void Write(char[] buffer, int index, int count) { 
            if (buffer==null) 
                throw new ArgumentNullException("buffer", Environment.GetResourceString("ArgumentNull_Buffer"));
            if (index < 0) 
                throw new ArgumentOutOfRangeException("index", Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
            if (count < 0)
                throw new ArgumentOutOfRangeException("count", Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegNum"));
            if (buffer.Length - index < count) 
                throw new ArgumentException(Environment.GetResourceString("Argument_InvalidOffLen"));
            Contract.EndContractBlock(); 
 
            while (count > 0) {
                if (charPos == charLen) Flush(false, false); 
                int n = charLen - charPos;
                if (n > count) n = count;
                Contract.Assert(n > 0, "StreamWriter::Write(char[], int, int) isn't making progress!  This is most likely a race condition in user code.");
                Buffer.InternalBlockCopy(buffer, index*2, charBuffer, charPos*2, n*2); 
                charPos += n;
                index += n; 
                count -= n; 
            }
            if (autoFlush) Flush(true, false); 
        }

        [System.Security.SecuritySafeCritical]  // auto-generated
        public override void Write(String value) { 
            if (value != null) {
                int count = value.Length; 
                int index = 0; 
                while (count > 0) {
                    if (charPos == charLen) Flush(false, false); 
                    int n = charLen - charPos;
                    if (n > count) n = count;
                    Contract.Assert(n > 0, "StreamWriter::Write(String) isn't making progress!  This is most likely a race condition in user code.");
                    value.CopyTo(index, charBuffer, charPos, n); 
                    charPos += n;
                    index += n; 
                    count -= n; 
                }
                if (autoFlush) Flush(true, false); 
            }
        }

#if false 
        // This method is more efficient for long strings outputted to streams
        // than the one on TextWriter, and won't cause any problems in terms of 
        // hiding methods on TextWriter as long as languages respect the 
        // hide-by-name-and-sig metadata flag.
        public override void WriteLine(String value) { 
            if (value != null) {
                int count = value.Length;
                int index = 0;
                while (count > 0) { 
                    if (charPos == charLen) Flush(false);
                    int n = charLen - charPos; 
                    if (n > count) n = count; 
                    Contract.Assert(n > 0, "StreamWriter::WriteLine(String) isn't making progress!  This is most likely a race condition in user code.");
                    value.CopyTo(index, charBuffer, charPos, n); 
                    charPos += n;
                    index += n;
                    count -= n;
                } 
            }
            if (charPos >= charLen - 2) Flush(false); 
            Buffer.InternalBlockCopy(CoreNewLine, 0, charBuffer, charPos*2, CoreNewLine.Length * 2); 
            charPos += CoreNewLine.Length;
            if (autoFlush) Flush(true, false); 
        }
#endif
    }
#if MDA_SUPPORTED 
    // StreamWriterBufferedDataLost MDA
    // Instead of adding a finalizer to StreamWriter for detecting buffered data loss 
    // (ie, when the user forgets to call Close/Flush on the StreamWriter), we will 
    // have a separate object with normal finalization semantics that maintains a
    // back pointer to this StreamWriter and alerts about any data loss 
    sealed class MdaHelper
    {
        private StreamWriter streamWriter;
        private String allocatedCallstack;    // captures the callstack when this streamwriter was allocated 

        internal MdaHelper(StreamWriter sw, String cs) 
        { 
            streamWriter = sw;
            allocatedCallstack = cs; 
        }

        // Finalizer
        ~MdaHelper() 
        {
            // Make sure people closed this StreamWriter, exclude StreamWriter::Null. 
            if (streamWriter.charPos != 0 && streamWriter.stream != null && streamWriter.stream != Stream.Null) { 
                String fileName = (streamWriter.stream is FileStream) ? ((FileStream)streamWriter.stream).NameInternal : "<unknown>";
                String callStack = allocatedCallstack; 

                if (callStack == null)
                    callStack = Environment.GetResourceString("IO_StreamWriterBufferedDataLostCaptureAllocatedFromCallstackNotEnabled");
 
                String message = Environment.GetResourceString("IO_StreamWriterBufferedDataLost", streamWriter.stream.GetType().FullName, fileName, callStack);
 
                Mda.StreamWriterBufferedDataLost.ReportError(message); 
            }
        } 
    }
#endif  // MDA_SUPPORTED
}

