// ==++== 
//
//   Copyright (c) Microsoft Corporation.  All rights reserved.
//
// ==--== 
/*============================================================
** 
** Class:  Stream 
**
** <OWNER>[....]</OWNER> 
**
**
** Purpose: Abstract base class for all Streams.  Provides
** default implementations of asynchronous reads & writes, in 
** terms of the synchronous reads & writes (and vice versa).
** 
** 
===========================================================*/
using System; 
using System.Threading;
using System.Runtime;
using System.Runtime.InteropServices;
#if NEW_EXPERIMENTAL_ASYNC_IO 
using System.Runtime.Remoting.Messaging;
#endif 
using System.Security; 
using System.Security.Permissions;
using System.Diagnostics.Contracts; 

namespace System.IO {
    [Serializable]
    [ComVisible(true)] 
    [ContractClass(typeof(StreamContract))]
#if FEATURE_REMOTING 
    public abstract class Stream : MarshalByRefObject, IDisposable { 
#if false
    } 
#endif // false
#else // FEATURE_REMOTING
    public abstract class Stream : IDisposable {
#endif // FEATURE_REMOTING 

        public static readonly Stream Null = new NullStream(); 
 
        private const int _DefaultBufferSize = 4096;
 
#if NEW_EXPERIMENTAL_ASYNC_IO
        // To implement Async IO operations on streams that don't support async IO
        private delegate int ReadDelegate([In, Out]byte[] bytes, int index, int offset);
        private delegate void WriteDelegate(byte[] bytes, int index, int offset); 

        [NonSerialized] 
        private ReadDelegate _readDelegate; 
        [NonSerialized]
        private WriteDelegate _writeDelegate; 
        // Use a semaphore here with a max count of 1.  Note Mutex in Win32
        // is very different from a semaphore, requiring thread affinity.
        [NonSerialized]
        private AutoResetEvent _asyncActiveEvent; 

        [NonSerialized] 
        // Keeps track of pending IO and not close the event until after 
        // all operations are completed. This avoids NullRefExc for _asyncActiveEvent
        private int _asyncActiveCount = 1; 
#endif
        public abstract bool CanRead {
            [Pure]
            get; 
        }
 
        // If CanSeek is false, Position, Seek, Length, and SetLength should throw. 
        public abstract bool CanSeek {
            [Pure] 
            get;
        }

        [ComVisible(false)] 
        public virtual bool CanTimeout {
            [Pure] 
            get { 
                return false;
            } 
        }

        public abstract bool CanWrite {
            [Pure] 
            get;
        } 
 
        public abstract long Length {
            get; 
        }

        public abstract long Position {
            get; 
            set;
        } 
 
        [ComVisible(false)]
        public virtual int ReadTimeout { 
            get {
                Contract.Ensures(Contract.Result<int>() >= 0);
                throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_TimeoutsNotSupported"));
            } 
            set {
                throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_TimeoutsNotSupported")); 
            } 
        }
 
        [ComVisible(false)]
        public virtual int WriteTimeout {
            get {
                Contract.Ensures(Contract.Result<int>() >= 0); 
                throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_TimeoutsNotSupported"));
            } 
            set { 
                throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_TimeoutsNotSupported"));
            } 
        }

        // Reads the bytes from the current stream and writes the bytes to
        // the destination stream until all bytes are read, starting at 
        // the current position.
        public void CopyTo(Stream destination) 
        { 
            if (destination == null)
                throw new ArgumentNullException("destination"); 
            if (!CanRead && !CanWrite)
                throw new ObjectDisposedException(null, Environment.GetResourceString("ObjectDisposed_StreamClosed"));
            if (!destination.CanRead && !destination.CanWrite)
                throw new ObjectDisposedException("destination", Environment.GetResourceString("ObjectDisposed_StreamClosed")); 
            if (!CanRead)
                throw new NotSupportedException(Environment.GetResourceString("NotSupported_UnreadableStream")); 
            if (!destination.CanWrite) 
                throw new NotSupportedException(Environment.GetResourceString("NotSupported_UnwritableStream"));
            Contract.EndContractBlock(); 

            InternalCopyTo(destination, _DefaultBufferSize);
        }
 
        public void CopyTo(Stream destination, int bufferSize)
        { 
            if (destination == null) 
                throw new ArgumentNullException("destination");
            if (bufferSize <= 0) 
                throw new ArgumentOutOfRangeException("bufferSize",
                        Environment.GetResourceString("ArgumentOutOfRange_NeedPosNum"));
            if (!CanRead && !CanWrite)
                throw new ObjectDisposedException(null, Environment.GetResourceString("ObjectDisposed_StreamClosed")); 
            if (!destination.CanRead && !destination.CanWrite)
                throw new ObjectDisposedException("destination", Environment.GetResourceString("ObjectDisposed_StreamClosed")); 
            if (!CanRead) 
                throw new NotSupportedException(Environment.GetResourceString("NotSupported_UnreadableStream"));
            if (!destination.CanWrite) 
                throw new NotSupportedException(Environment.GetResourceString("NotSupported_UnwritableStream"));
            Contract.EndContractBlock();

            InternalCopyTo(destination, bufferSize); 
        }
 
        private void InternalCopyTo(Stream destination, int bufferSize) 
        {
            Contract.Requires(destination != null); 
            Contract.Requires(CanRead);
            Contract.Requires(destination.CanWrite);
            Contract.Requires(bufferSize > 0);
 
            byte[] buffer = new byte[bufferSize];
            int read; 
            while ((read = Read(buffer, 0, buffer.Length)) != 0) 
                destination.Write(buffer, 0, read);
        } 


        // Stream used to require that all cleanup logic went into Close(),
        // which was thought up before we invented IDisposable.  However, we 
        // need to follow the IDisposable pattern so that users can write
        // sensible subclasses without needing to inspect all their base 
        // classes, and without worrying about version brittleness, from a 
        // base class switching to the Dispose pattern.  We're moving
        // Stream to the Dispose(bool) pattern - that's where all subclasses 
        // should put their cleanup starting in V2.
        public virtual void Close()
        {
            /* These are correct, but we'd have to fix PipeStream & NetworkStream very carefully. 
            Contract.Ensures(CanRead == false);
            Contract.Ensures(CanWrite == false); 
            Contract.Ensures(CanSeek == false); 
            */
 
            Dispose(true);
            GC.SuppressFinalize(this);
        }
 
        public void Dispose()
        { 
            /* These are correct, but we'd have to fix PipeStream & NetworkStream very carefully. 
            Contract.Ensures(CanRead == false);
            Contract.Ensures(CanWrite == false); 
            Contract.Ensures(CanSeek == false);
            */

            Close(); 
        }
 
 
        protected virtual void Dispose(bool disposing)
        { 
            // Note: Never change this to call other virtual methods on Stream
            // like Write, since the state on subclasses has already been
            // torn down.  This is the last code to run on cleanup for a stream.
#if NEW_EXPERIMENTAL_ASYNC_IO 
            if ((disposing) && (_asyncActiveEvent != null))
                _CloseAsyncActiveEvent(Interlocked.Decrement(ref _asyncActiveCount)); 
#endif 
        }
 
        private void _CloseAsyncActiveEvent(int asyncActiveCount)
        {
#if NEW_EXPERIMENTAL_ASYNC_IO
            Contract.Assert(_asyncActiveCount >= 0, "ref counting mismatch, possible ---- in the code"); 

            // If no pending async IO, close the event 
            if ((_asyncActiveEvent != null) && (asyncActiveCount == 0)) { 
                _asyncActiveEvent.Close();
                _asyncActiveEvent = null; 
            }
#endif
        }
 
        public abstract void Flush();
 
 
        [Obsolete("CreateWaitHandle will be removed eventually.  Please use \"new ManualResetEvent(false)\" instead.")]
        protected virtual WaitHandle CreateWaitHandle() 
        {
            Contract.Ensures(Contract.Result<WaitHandle>() != null);
            return new ManualResetEvent(false);
        } 

        [HostProtection(ExternalThreading=true)] 
        public virtual IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, Object state) 
        {
            Contract.Ensures(Contract.Result<IAsyncResult>() != null); 
            if (!CanRead) __Error.ReadNotSupported();

#if !NEW_EXPERIMENTAL_ASYNC_IO
            IAsyncResult asyncResult = BlockingBeginRead(buffer, offset, count, callback, state); 
#else
            // Increment the count to account for this async operation 
            Contract.Assert(_asyncActiveCount >= 1, "ref counting mismatch, possible ---- in the code"); 
            Interlocked.Increment(ref _asyncActiveCount);
 
            ReadDelegate d = new ReadDelegate(Read);

            // To avoid a race with a stream's position pointer & generating ----
            // conditions with internal buffer indexes in our own streams that 
            // don't natively support async IO operations when there are multiple
            // async requests outstanding, we will block the application's main 
            // thread if it does a second IO request until the first one completes. 
            if (_asyncActiveEvent == null) {
                lock(this) { 
                    if (_asyncActiveEvent == null)
                        _asyncActiveEvent = new AutoResetEvent(true);
                }
            } 
            bool r = _asyncActiveEvent.WaitOne();
            Contract.Assert(r, "AutoResetEvent didn't get a signal when we called WaitOne!"); 
 
            Contract.Assert(_readDelegate == null && _writeDelegate == null, "Expected no other readers or writers!");
 
            // Set delegate before we call BeginInvoke, to avoid a race
            _readDelegate = d;
            IAsyncResult asyncResult = d.BeginInvoke(buffer, offset, count, callback, state);
#endif 

            return asyncResult; 
        } 

        public virtual int EndRead(IAsyncResult asyncResult) 
        {
            if (asyncResult == null)
                throw new ArgumentNullException("asyncResult");
            Contract.Ensures(Contract.Result<int>() >= 0); 
            Contract.EndContractBlock();
 
#if !NEW_EXPERIMENTAL_ASYNC_IO 
            return BlockingEndRead(asyncResult);
#else 
            // Ideally we want to throw InvalidOperationException but for ECMA conformance we need to throw ArgExc instead.
            if (_readDelegate == null)
                throw new ArgumentException(Environment.GetResourceString("InvalidOperation_WrongAsyncResultOrEndReadCalledMultiple"));
 
            int numRead = -1;
            try { 
                numRead = _readDelegate.EndInvoke(asyncResult); 
            }
            finally { 
                _readDelegate = null;
                _asyncActiveEvent.Set();

                // Decrement the count to account for this async operation 
                // and close the handle if no other IO is pending
                _CloseAsyncActiveEvent(Interlocked.Decrement(ref _asyncActiveCount)); 
            } 

            return numRead; 
#endif
        }

        [HostProtection(ExternalThreading=true)] 
        public virtual IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, Object state)
        { 
            Contract.Ensures(Contract.Result<IAsyncResult>() != null); 
            if (!CanWrite) __Error.WriteNotSupported();
 
#if !NEW_EXPERIMENTAL_ASYNC_IO
            IAsyncResult asyncResult = BlockingBeginWrite(buffer, offset, count, callback, state);
#else
            // Increment the count to account for this async operation 
            Contract.Assert(_asyncActiveCount >= 1, "ref counting mismatch, possible ---- in the code");
            Interlocked.Increment(ref _asyncActiveCount); 
 
            WriteDelegate d = new WriteDelegate(Write);
 
            // To avoid a race with a stream's position pointer & generating ----
            // conditions with internal buffer indexes in our own streams that
            // don't natively support async IO operations when there are multiple
            // async requests outstanding, we will block the application's main 
            // thread if it does a second IO request until the first one completes.
            if (_asyncActiveEvent == null) { 
                lock(this) { 
                    if (_asyncActiveEvent == null)
                        _asyncActiveEvent = new AutoResetEvent(true); 
                }
            }
            bool r = _asyncActiveEvent.WaitOne();
            Contract.Assert(r, "AutoResetEvent didn't get a signal when we called WaitOne!"); 

            Contract.Assert(_readDelegate == null && _writeDelegate == null, "Expected no other readers or writers!"); 
 
            // Set delegate before we call BeginInvoke, to avoid a race
            _writeDelegate = d; 
            IAsyncResult asyncResult = d.BeginInvoke(buffer, offset, count, callback, state);
#endif
            return asyncResult;
 
        }
 
        public virtual void EndWrite(IAsyncResult asyncResult) 
        {
            if (asyncResult==null) 
                throw new ArgumentNullException("asyncResult");
            Contract.EndContractBlock();

#if !NEW_EXPERIMENTAL_ASYNC_IO 
            BlockingEndWrite(asyncResult);
#else 
            // Ideally we want to throw InvalidOperationException but for ECMA conformance we need to throw ArgExc instead. 
            if (_writeDelegate == null)
                throw new ArgumentException(Environment.GetResourceString("InvalidOperation_WrongAsyncResultOrEndWriteCalledMultiple")); 

            try {
                _writeDelegate.EndInvoke(asyncResult);
            } 
            finally {
                _writeDelegate = null; 
                _asyncActiveEvent.Set(); 

                // Decrement the count to account for this async operation 
                // and close the handle if no other IO is pending
                _CloseAsyncActiveEvent(Interlocked.Decrement(ref _asyncActiveCount));
            }
#endif 
        }
 
        public abstract long Seek(long offset, SeekOrigin origin); 

        public abstract void SetLength(long value); 

        public abstract int Read([In, Out] byte[] buffer, int offset, int count);

        // Reads one byte from the stream by calling Read(byte[], int, int). 
        // Will return an unsigned byte cast to an int or -1 on end of stream.
        // This implementation does not perform well because it allocates a new 
        // byte[] each time you call it, and should be overridden by any 
        // subclass that maintains an internal buffer.  Then, it can help perf
        // significantly for people who are reading one byte at a time. 
#if !FEATURE_CORECLR
        [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
#endif
        public virtual int ReadByte() 
        {
            Contract.Ensures(Contract.Result<int>() >= -1); 
            Contract.Ensures(Contract.Result<int>() < 256); 

            byte[] oneByteArray = new byte[1]; 
            int r = Read(oneByteArray, 0, 1);
            if (r==0)
                return -1;
            return oneByteArray[0]; 
        }
 
        public abstract void Write(byte[] buffer, int offset, int count); 

        // Writes one byte from the stream by calling Write(byte[], int, int). 
        // This implementation does not perform well because it allocates a new
        // byte[] each time you call it, and should be overridden by any
        // subclass that maintains an internal buffer.  Then, it can help perf
        // significantly for people who are writing one byte at a time. 
        public virtual void WriteByte(byte value)
        { 
            byte[] oneByteArray = new byte[1]; 
            oneByteArray[0] = value;
            Write(oneByteArray, 0, 1); 
        }

        [HostProtection(Synchronization=true)]
        public static Stream Synchronized(Stream stream) 
        {
            if (stream==null) 
                throw new ArgumentNullException("stream"); 
            Contract.Ensures(Contract.Result<Stream>() != null);
            Contract.EndContractBlock(); 
            if (stream is SyncStream)
                return stream;

            return new SyncStream(stream); 
        }
 
        [ContractInvariantMethod] 
        protected virtual void ObjectInvariant()
        { 
        }

        internal IAsyncResult BlockingBeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, Object state)
        { 
            Contract.Ensures(Contract.Result<IAsyncResult>() != null);
 
            // To avoid a race with a stream's position pointer & generating ---- 
            // conditions with internal buffer indexes in our own streams that
            // don't natively support async IO operations when there are multiple 
            // async requests outstanding, we will block the application's main
            // thread and do the IO synchronously.
            // This can't perform well - use a different approach.
            SynchronousAsyncResult asyncResult = new SynchronousAsyncResult(state, false); 
            try {
                int numRead = Read(buffer, offset, count); 
                asyncResult._numRead = numRead; 
            }
            catch (IOException e) { 
                asyncResult._exception = e;
            }
            finally {
                asyncResult._isCompleted = true; 
                asyncResult._waitHandle.Set();
            } 
 
            if (callback != null) {
                callback(asyncResult); 
            }

            return asyncResult;
        } 

        internal static int BlockingEndRead(IAsyncResult asyncResult) 
        { 
            Contract.Ensures(Contract.Result<int>() >= 0);
 
            SynchronousAsyncResult ar = asyncResult as SynchronousAsyncResult;
            if (ar == null || ar.IsWrite)
                __Error.WrongAsyncResult();
            if (ar._EndXxxCalled) 
                __Error.EndReadCalledTwice();
            ar._EndXxxCalled = true; 
 
            if (ar._exception != null)
                throw ar._exception; 

            return ar._numRead;
        }
 
        internal IAsyncResult BlockingBeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, Object state)
        { 
            Contract.Ensures(Contract.Result<IAsyncResult>() != null); 

            // To avoid a race with a stream's position pointer & generating ---- 
            // conditions with internal buffer indexes in our own streams that
            // don't natively support async IO operations when there are multiple
            // async requests outstanding, we will block the application's main
            // thread and do the IO synchronously. 
            // This can't perform well - use a different approach.
            SynchronousAsyncResult asyncResult = new SynchronousAsyncResult(state, true); 
            try { 
                Write(buffer, offset, count);
            } 
            catch (IOException e) {
                asyncResult._exception = e;
            }
            finally { 
                asyncResult._isCompleted = true;
                asyncResult._waitHandle.Set(); 
            } 

            if (callback != null) { 
                callback(asyncResult);
            }

            return asyncResult; 
        }
 
        internal static void BlockingEndWrite(IAsyncResult asyncResult) 
        {
            SynchronousAsyncResult ar = asyncResult as SynchronousAsyncResult; 
            if (ar == null || !ar.IsWrite)
                __Error.WrongAsyncResult();
            if (ar._EndXxxCalled)
                __Error.EndWriteCalledTwice(); 
            ar._EndXxxCalled = true;
 
            if (ar._exception != null) 
                throw ar._exception;
        } 

        [Serializable]
        private sealed class NullStream : Stream
        { 
            internal NullStream() {}
 
            public override bool CanRead { 
                [Pure]
                get { return true; } 
            }

            public override bool CanWrite {
                [Pure] 
                get { return true; }
            } 
 
            public override bool CanSeek {
                [Pure] 
                get { return true; }
            }

            public override long Length { 
                get { return 0; }
            } 
 
            public override long Position {
                get { return 0; } 
                set {}
            }

            protected override void Dispose(bool disposing) 
            {
                // Do nothing - we don't want NullStream singleton (static) to be closable 
            } 

            public override void Flush() 
            {
            }

            [HostProtection(ExternalThreading = true)] 
            public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, Object state)
            { 
                if (!CanRead) __Error.ReadNotSupported(); 

                return BlockingBeginRead(buffer, offset, count, callback, state); 
            }

            public override int EndRead(IAsyncResult asyncResult)
            { 
                if (asyncResult == null)
                    throw new ArgumentNullException("asyncResult"); 
                Contract.EndContractBlock(); 

                return BlockingEndRead(asyncResult); 
            }

            [HostProtection(ExternalThreading = true)]
            public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, Object state) 
            {
                if (!CanWrite) __Error.WriteNotSupported(); 
 
                return BlockingBeginWrite(buffer, offset, count, callback, state);
            } 

            public override void EndWrite(IAsyncResult asyncResult)
            {
                if (asyncResult == null) 
                    throw new ArgumentNullException("asyncResult");
                Contract.EndContractBlock(); 
 
                BlockingEndWrite(asyncResult);
            } 

            public override int Read([In, Out] byte[] buffer, int offset, int count)
            {
                return 0; 
            }
 
            public override int ReadByte() 
            {
                return -1; 
            }

            public override void Write(byte[] buffer, int offset, int count)
            { 
            }
 
            public override void WriteByte(byte value) 
            {
            } 

            public override long Seek(long offset, SeekOrigin origin)
            {
                return 0; 
            }
 
            public override void SetLength(long length) 
            {
            } 
        }

        // Used as the IAsyncResult object when using asynchronous IO methods
        // on the base Stream class.  Note I'm not using async delegates, so 
        // this is necessary.
        private sealed class SynchronousAsyncResult : IAsyncResult 
        { 
            internal ManualResetEvent _waitHandle;
            internal Object _stateObject; 
            internal int _numRead;
            internal bool _isCompleted;
            internal bool _isWrite;
            internal bool _EndXxxCalled; 
            internal Exception _exception;
 
            internal SynchronousAsyncResult(Object asyncStateObject, bool isWrite) 
            {
                _stateObject = asyncStateObject; 
                _isWrite = isWrite;
                _waitHandle = new ManualResetEvent(false);
            }
 
            public bool IsCompleted {
                get { return _isCompleted; } 
            } 

            public WaitHandle AsyncWaitHandle { 
                get { return _waitHandle; }
            }

            public Object AsyncState { 
                get { return _stateObject; }
            } 
 
            public bool CompletedSynchronously {
                get { return true; } 
            }

            internal bool IsWrite {
                get { return _isWrite; } 
            }
        } 
 
        // SyncStream is a wrapper around a stream that takes
        // a lock for every operation making it thread safe. 

        [Serializable]
        internal sealed class SyncStream : Stream, IDisposable
        { 
            private Stream _stream;
 
            internal SyncStream(Stream stream) 
            {
                if (stream == null) 
                    throw new ArgumentNullException("stream");
                Contract.EndContractBlock();
                _stream = stream;
            } 

            public override bool CanRead { 
                [Pure] 
                get { return _stream.CanRead; }
            } 

            public override bool CanWrite {
                [Pure]
                get { return _stream.CanWrite; } 
            }
 
            public override bool CanSeek { 
                [Pure]
                get { return _stream.CanSeek; } 
            }

            [ComVisible(false)]
            public override bool CanTimeout { 
                [Pure]
                get { 
                    return _stream.CanTimeout; 
                }
            } 

            public override long Length {
                get {
                    lock(_stream) { 
                        return _stream.Length;
                    } 
                } 
            }
 
            public override long Position {
                get {
                    lock(_stream) {
                        return _stream.Position; 
                    }
                } 
                set { 
                    lock(_stream) {
                        _stream.Position = value; 
                    }
                }
            }
 
            [ComVisible(false)]
            public override int ReadTimeout { 
                get { 
                    return _stream.ReadTimeout;
                } 
                set {
                    _stream.ReadTimeout = value;
                }
            } 

            [ComVisible(false)] 
            public override int WriteTimeout { 
                get {
                    return _stream.WriteTimeout; 
                }
                set {
                    _stream.WriteTimeout = value;
                } 
            }
 
            // In the off chance that some wrapped stream has different 
            // semantics for Close vs. Dispose, let's preserve that.
            public override void Close() 
            {
                lock(_stream) {
                    try {
                        _stream.Close(); 
                    }
                    finally { 
                        base.Dispose(true); 
                    }
                } 
            }

            protected override void Dispose(bool disposing)
            { 
                lock(_stream) {
                    try { 
                        // Explicitly pick up a potentially methodimpl'ed Dispose 
                        if (disposing)
                            ((IDisposable)_stream).Dispose(); 
                    }
                    finally {
                        base.Dispose(disposing);
                    } 
                }
            } 
 
            public override void Flush()
            { 
                lock(_stream)
                    _stream.Flush();
            }
 
            public override int Read([In, Out]byte[] bytes, int offset, int count)
            { 
                lock(_stream) 
                    return _stream.Read(bytes, offset, count);
            } 

            public override int ReadByte()
            {
                lock(_stream) 
                    return _stream.ReadByte();
            } 
 
            [HostProtection(ExternalThreading=true)]
            public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, Object state) 
            {
                lock(_stream)
                    return _stream.BeginRead(buffer, offset, count, callback, state);
            } 

            public override int EndRead(IAsyncResult asyncResult) 
            { 
                lock(_stream)
                    return _stream.EndRead(asyncResult); 
            }

            public override long Seek(long offset, SeekOrigin origin)
            { 
                lock(_stream)
                    return _stream.Seek(offset, origin); 
            } 

            public override void SetLength(long length) 
            {
                lock(_stream)
                    _stream.SetLength(length);
            } 

            public override void Write(byte[] bytes, int offset, int count) 
            { 
                lock(_stream)
                    _stream.Write(bytes, offset, count); 
            }

            public override void WriteByte(byte b)
            { 
                lock(_stream)
                    _stream.WriteByte(b); 
            } 

            [HostProtection(ExternalThreading=true)] 
            public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, Object state)
            {
                lock(_stream)
                    return _stream.BeginWrite(buffer, offset, count, callback, state); 
            }
 
            public override void EndWrite(IAsyncResult asyncResult) 
            {
                lock(_stream) 
                    _stream.EndWrite(asyncResult);
            }
        }
    } 

    [ContractClassFor(typeof(Stream))] 
    internal abstract class StreamContract : Stream 
    {
        public override long Seek(long offset, SeekOrigin origin) 
        {
            Contract.Ensures(Contract.Result<long>() >= 0);
            throw new NotImplementedException();
        } 

        public override void SetLength(long value) 
        { 
            throw new NotImplementedException();
        } 

        public override int Read(byte[] buffer, int offset, int count)
        {
            Contract.Ensures(Contract.Result<int>() >= 0); 
            Contract.Ensures(Contract.Result<int>() <= count);
            throw new NotImplementedException(); 
        } 

        public override void Write(byte[] buffer, int offset, int count) 
        {
            throw new NotImplementedException();
        }
 
        public override long Position {
            get { 
                Contract.Ensures(Contract.Result<long>() >= 0); 
                throw new NotImplementedException();
            } 
            set {
                throw new NotImplementedException();
            }
        } 

        public override void Flush() 
        { 
            throw new NotImplementedException();
        } 

        public override bool CanRead {
            get { throw new NotImplementedException(); }
        } 

        public override bool CanWrite { 
            get { throw new NotImplementedException(); } 
        }
 
        public override bool CanSeek {
            get { throw new NotImplementedException(); }
        }
 
        public override long Length
        { 
            get { 
                Contract.Ensures(Contract.Result<long>() >= 0);
                throw new NotImplementedException(); 
            }
        }
    }
} 

