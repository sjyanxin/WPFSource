//------------------------------------------------------------------------------ 
//
// <copyright file="CompoundFileDeflateTransform.cs" company="Microsoft">
//    Copyright (C) Microsoft Corporation.  All rights reserved.
// </copyright> 
//
// Description: 
//  Implementation of a helper class that provides a fully functional Stream on unmanaged ZLib in a fashion 
//  consistent with Office and RMA (see Creating Rights-Managed HTML Files at
//  http://msdn.microsoft.com/library/default.asp?url=/library/en-us/rma/introduction.asp). 
//
// History:
//  10/05/2005: BruceMac: First created.
//  02/14/2006: BruceMac: Rename file to reflect class name and apply security mitigations 
//              identified during security code review.
//  03/09/2006: BruceMac: Make AllocOrRealloc SecurityCritical because it allocates 
//              pinned memory based on caller arguments. 
//-----------------------------------------------------------------------------
// Allow use of presharp warning numbers [6518] unknown to the compiler 
#pragma warning disable 1634, 1691

using System;
using System.IO; 
using System.Diagnostics;
 
using System.IO.Packaging; 
using System.Windows;
using System.Runtime.InteropServices;           // for Marshal class 
using MS.Internal.IO.Packaging;                 // for PackagingUtilities
using System.Security;                          // for SecurityCritical and SecurityTreatAsSafe
using MS.Internal.WindowsBase;
 
namespace MS.Internal.IO.Packaging.CompoundFile
{ 
    //----------------------------------------------------- 
    //
    //  Internal Members 
    //
    //-----------------------------------------------------
    /// <summary>
    /// Provides Office-compatible ZLib compression using interop to ZLib library 
    /// </summary>
    /// <remarks>This class makes use of GCHandles in order to share data in a non-trivial fashion with the 
    /// unmanaged ZLib library.  Because of this, it demands UnmanagedCodePermission of it's caller. 
    /// IDeflateTransform is a batch-oriented interface.  All data will be transformed from source
    /// to destination.</remarks> 
    internal class CompoundFileDeflateTransform : IDeflateTransform
    {
        //------------------------------------------------------
        // 
        //  IDeflateTransform Interface
        // 
        //----------------------------------------------------- 
        /// <summary>
        /// Decompress delegate - invoke ZLib in a manner consistent with RMA/Office 
        /// </summary>
        /// <param name="source">stream to read from</param>
        /// <param name="sink">stream to write to</param>
        ///<SecurityNote> 
        ///     Critical: calls AllocOrRealloc which is allocates pinned memory based on arguments
        ///     TreatAsSafe: Callers cannot use this to allocate memory of arbitrary size. 
        ///          AllocOrRealloc is used in two occasions: 
        ///          1. Compress - here we provide size based on our default block size (of 4k)
        ///                  and growth will not exceed double this size (we are compressing, but sometimes 
        ///                  compression doesn't succeed in reducing sizes).
        ///          2. Decompress - here the block size is based on values obtained from the
        ///                  stream itself (see ReadBlockHeader) but we are careful to throw on malicious
        ///                  input.  Any size > 1MB is considered malicious and rejected. 
        ///</SecurityNote>
        [SecurityCritical, SecurityTreatAsSafe] 
        public void Decompress(Stream source, Stream sink) 
        {
            if (source == null) 
                throw new ArgumentNullException("source");

            if (sink == null)
                throw new ArgumentNullException("sink"); 

            Invariant.Assert(source.CanRead); 
            Invariant.Assert(sink.CanWrite, "Logic Error - Cannot decompress into a read-only stream"); 

            // remember this for later 
            long storedPosition = -1;

            try
            { 
                if (source.CanSeek)
                { 
                    storedPosition = source.Position; 
                    source.Position = 0;
                } 
                if (sink.CanSeek)
                    sink.Position = 0;

                // zlib state 
                UnsafeNativeMethods.ZStream zStream = new UnsafeNativeMethods.ZStream();
 
                // initialize the zlib library 
                UnsafeNativeMethods.ZLib.ErrorCode retVal = 0;
                retVal = UnsafeNativeMethods.ZLib.ums_inflate_init(ref zStream, 
                        UnsafeNativeMethods.ZLib.ZLibVersion, Marshal.SizeOf(zStream));

                ThrowIfZLibError(retVal);
 
                byte[] sourceBuf = null;                    // source buffer
                byte[] sinkBuf = null;                      // destination buffer - where to write data 
                GCHandle gcSourceBuf = new GCHandle();      // Preallocate these so we can safely access them 
                GCHandle gcSinkBuf = new GCHandle();        // in the next finally block.
 
                try
                {
                    // read all available data
                    // each block is preceded by a header that is 3 ulongs 
                    int uncompressedSize, compressedSize;
                    long destStreamLength = 0;      // keep track of decompressed size 
                    while (ReadBlockHeader(source, out uncompressedSize, out compressedSize)) 
                    {
                        // ensure we have space 
                        AllocOrRealloc(compressedSize, ref sourceBuf, ref gcSourceBuf);
                        AllocOrRealloc(uncompressedSize, ref sinkBuf, ref gcSinkBuf);

                        // read the data into the sourceBuf 
                        int bytesRead = PackagingUtilities.ReliableRead(source, sourceBuf, 0, compressedSize);
                        if (bytesRead > 0) 
                        { 
                            if (compressedSize != bytesRead)
                                throw new FileFormatException(SR.Get(SRID.CorruptStream)); 

                            // prepare structure
                            // The buffer pointers must be reset for every call
                            // because ums_inflate modifies them 
                            zStream.pInBuf = gcSourceBuf.AddrOfPinnedObject();
                            zStream.pOutBuf = gcSinkBuf.AddrOfPinnedObject(); 
                            zStream.cbIn = (uint)bytesRead;     // this is number of bytes available for decompression at pInBuf and is updated by ums_deflate call 
                            zStream.cbOut = (uint)sinkBuf.Length;   // this is the number of bytes free in pOutBuf and is updated by ums_deflate call
 
                            // InvokeZLib does the actual interop.  It updates zStream, and sinkBuf (sourceBuf passed by ref to avoid copying)
                            // and leaves the decompressed data in sinkBuf.
                            //                        int decompressedSize = InvokeZLib(bytesRead, ref zStream, ref sourceBuf, ref sinkBuf, pSource, pSink, false);
                            retVal = UnsafeNativeMethods.ZLib.ums_inflate(ref zStream, 
                               (int)UnsafeNativeMethods.ZLib.FlushCodes.SyncFlush);
 
                            ThrowIfZLibError(retVal); 

                            checked 
                            {
                                int decompressedSize = sinkBuf.Length - (int)zStream.cbOut;

                                // verify that data matches header 
                                if (decompressedSize != uncompressedSize)
                                    throw new FileFormatException(SR.Get(SRID.CorruptStream)); 
 
                                destStreamLength += decompressedSize;
 
                                // write to the base stream
                                sink.Write(sinkBuf, 0, decompressedSize);
                            }
                        } 
                        else
                        { 
                            // block header but no block data 
                            if (compressedSize != 0)
                                throw new FileFormatException(SR.Get(SRID.CorruptStream)); 
                        }
                    }

                    // make sure we truncate if the destination stream was longer than this current decompress 
                    if (sink.CanSeek)
                        sink.SetLength(destStreamLength); 
                } 
                finally
                { 
                    if (gcSourceBuf.IsAllocated)
                        gcSourceBuf.Free();

                    if (gcSinkBuf.IsAllocated) 
                        gcSinkBuf.Free();
                } 
            } 
            finally
            { 
                // seek to the current logical position before returning
                if (source.CanSeek)
                    source.Position = storedPosition;
            } 
        }
 
        /// <summary> 
        /// Compress delegate - invoke ZLib in a manner consistent with RMA/Office
        /// </summary> 
        /// <param name="source"></param>
        /// <param name="sink"></param>
        /// <remarks>We are careful to avoid use of Position, Length or SetLength on non-seekable streams.  If
        /// source or sink are non-seekable, it is assumed that positions are correctly set upon entry and that 
        /// they need not be restored.  We also assume that destination stream length need not be truncated.</remarks>
        ///<SecurityNote> 
        ///     Critical: calls AllocOrRealloc which is allocates pinned memory based on arguments 
        ///     TreatAsSafe: Callers cannot use this to allocate memory of arbitrary size.
        ///          AllocOrRealloc is used in two occasions: 
        ///          1. Compress - here we provide size based on our default block size (of 4k)
        ///                  and growth will not exceed double this size (we are compressing, but sometimes
        ///                  compression doesn't succeed in reducing sizes).
        ///          2. Decompress - here the block size is based on values obtained from the 
        ///                  stream itself (see ReadBlockHeader) but we are careful to throw on malicious
        ///                  input.  Any size > 1MB is considered malicious and rejected. 
        ///</SecurityNote> 
        [SecurityCritical, SecurityTreatAsSafe]
        public void Compress(Stream source, Stream sink) 
        {
            if (source == null)
                throw new ArgumentNullException("source");
 
            if (sink == null)
                throw new ArgumentNullException("sink"); 
 
            Invariant.Assert(source.CanRead);
            Invariant.Assert(sink.CanWrite, "Logic Error - Cannot compress into a read-only stream"); 

            // remember this for later if possible
            long storedPosition = -1;       // default to illegal value to catch any logic errors
 
            try
            { 
                int sourceBufferSize;   // don't allocate 4k for really tiny source streams 
                if (source.CanSeek)
                { 
                    storedPosition = source.Position;
                    source.Position = 0;

                    // Casting result to int is safe because _defaultBlockSize is very small and the result 
                    // of Math.Min(x, _defaultBlockSize) must be no larger than _defaultBlockSize.
                    sourceBufferSize = (int)(Math.Min(source.Length, (long)_defaultBlockSize)); 
                } 
                else
                    sourceBufferSize = _defaultBlockSize;    // can't call Length so fallback to default 

                if (sink.CanSeek)
                    sink.Position = 0;
 
                // zlib state
                UnsafeNativeMethods.ZStream zStream = new UnsafeNativeMethods.ZStream(); 
 
                // initialize the zlib library
                UnsafeNativeMethods.ZLib.ErrorCode retVal = UnsafeNativeMethods.ZLib.ums_deflate_init(ref zStream, _compressionLevel, 
                    UnsafeNativeMethods.ZLib.ZLibVersion, Marshal.SizeOf(zStream));

                ThrowIfZLibError(retVal);
 
                // where to write data - can actually grow if data is uncompressible
                long destStreamLength = 0; 
                byte[] sourceBuf = null;            // source buffer 
                byte[] sinkBuf = null;              // destination buffer
                GCHandle gcSourceBuf = new GCHandle(); 
                GCHandle gcSinkBuf = new GCHandle();
                try
                {
                    // allocate managed buffers 
                    AllocOrRealloc(sourceBufferSize, ref sourceBuf, ref gcSourceBuf);
                    AllocOrRealloc(_defaultBlockSize + (_defaultBlockSize >> 1), ref sinkBuf, ref gcSinkBuf); 
 
                    // while (more data is available)
                    //  - read into the sourceBuf 
                    //  - compress into the sinkBuf
                    //  - emit the header
                    //  - write out to the _baseStream
 
                    // Suppress 6518 Local IDisposable object not disposed:
                    // Reason: The stream is not owned by us, therefore we cannot 
                    // close the BinaryWriter as it will Close the stream underneath. 
#pragma warning disable 6518
                    BinaryWriter writer = new BinaryWriter(sink); 
                    int bytesRead;
                    while ((bytesRead = PackagingUtilities.ReliableRead(source, sourceBuf, 0, sourceBuf.Length)) > 0)
                    {
                        Invariant.Assert(bytesRead <= sourceBufferSize); 

                        // prepare structure 
                        // these pointers must be re-assigned for each loop because 
                        // ums_deflate modifies them
                        zStream.pInBuf = gcSourceBuf.AddrOfPinnedObject(); 
                        zStream.pOutBuf = gcSinkBuf.AddrOfPinnedObject();
                        zStream.cbIn = (uint)bytesRead;         // this is number of bytes available for compression at pInBuf and is updated by ums_deflate call
                        zStream.cbOut = (uint)sinkBuf.Length;   // this is the number of bytes free in pOutBuf and is updated by ums_deflate call
 
                        // cast is safe because SyncFlush is a constant
                        retVal = UnsafeNativeMethods.ZLib.ums_deflate(ref zStream, 
                            (int)UnsafeNativeMethods.ZLib.FlushCodes.SyncFlush); 
                        ThrowIfZLibError(retVal);
 
                        checked
                        {
                            int compressedSize = sinkBuf.Length - (int)zStream.cbOut;
                            Invariant.Assert(compressedSize > 0, "compressing non-zero bytes creates a non-empty block"); 

                            // This should never happen because our destination buffer 
                            // is twice as large as our source buffer 
                            Invariant.Assert(zStream.cbIn == 0, "Expecting all data to be compressed!");
 
                            // write the header
                            writer.Write(_blockHeaderToken);      // token
                            writer.Write((UInt32)bytesRead);
                            writer.Write((UInt32)compressedSize); 
                            destStreamLength += _headerBuf.Length;
 
                            // write to the base stream 
                            sink.Write(sinkBuf, 0, compressedSize);
                            destStreamLength += compressedSize; 
                        }
                    }

                    // post-compression 
                    // truncate if necessary
                    if (sink.CanSeek) 
                        sink.SetLength(destStreamLength); 
                }
                finally 
                {
                    if (gcSourceBuf.IsAllocated)
                        gcSourceBuf.Free();
 
                    if (gcSinkBuf.IsAllocated)
                        gcSinkBuf.Free(); 
                } 
#pragma warning restore 6518
            } 
            finally
            {
                // seek to the current logical position before returning
                if (sink.CanSeek) 
                    source.Position = storedPosition;
            } 
        } 

        //------------------------------------------------------ 
        //
        //  Private Methods
        //
        //------------------------------------------------------ 

        /// <summary> 
        /// Ensures that Buffer has enough room for size using alloc or realloc 
        /// </summary>
        /// <param name="buffer">buffer - may be null</param> 
        /// <param name="size">desired size</param>
        /// <param name="gcHandle">handle</param>
        /// <remarks>When this exits, buffer is at least as large as size
        /// and gcHandle is pointing to the pinned buffer.  If the buffer was already large enough, 
        /// no action is taken.</remarks>
        /// <SecurityNote> 
        ///     Critical - allocates pinned memory based on arguments 
        /// </SecurityNote>
        [SecurityCritical] 
        private static void AllocOrRealloc(int size, ref byte[] buffer, ref GCHandle gcHandle)
        {
            Invariant.Assert(size >= 0, "Cannot allocate negative number of bytes");
 
            // verify we have room
            if (buffer != null) 
            { 
                // do we have room?
                if (buffer.Length < size) 
                {
                    // overallocate to reduce the chance of future reallocations
                    size = Math.Max(size, buffer.Length + (buffer.Length >> 1));  // fast Length * 1.5
 
                    // free existing because it's too small
                    if (gcHandle.IsAllocated) 
                        gcHandle.Free(); 
                }
                else 
                    return;  // current buffer satisfies the request so there is no need to alloc
            }

            // We have to allocate in two cases: 
            // 1. We were called with buffer == null
            // 2. The original buffer was too small 
            buffer = new byte[size];                                // managed source buffer 
            gcHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned); // pinned so unmanaged code can read/write it
        } 

        /// <summary>
        /// ReadBlockHeader - reads the block header and returns true if successful
        /// </summary> 
        /// <param name="source">stream to read from</param>
        /// <param name="compressedSize">compressedSize from header</param> 
        /// <param name="uncompressedSize">uncompressedSize from header</param> 
        /// <returns>true if header found</returns>
        private bool ReadBlockHeader(Stream source, 
            out int uncompressedSize, out int compressedSize)
        {
            int bytesRead = PackagingUtilities.ReliableRead(source, _headerBuf, 0, _headerBuf.Length);
            if (bytesRead > 0) 
            {
                if (bytesRead < _headerBuf.Length) 
                    throw new FileFormatException(SR.Get(SRID.CorruptStream)); 

                // header format = 3 ulong's 
                // read and inspect token
                uint token = BitConverter.ToUInt32(_headerBuf, _ulongSize * 0);
                if (token != _blockHeaderToken)
                    throw new FileFormatException(SR.Get(SRID.CorruptStream)); 

                // convert to int's as that's what we use everywhere 
                checked 
                {
                    uncompressedSize = (int)BitConverter.ToUInt32(_headerBuf, _ulongSize * 1); 
                    compressedSize = (int)BitConverter.ToUInt32(_headerBuf, _ulongSize * 2);

                    // screen out malicious data
                    if (uncompressedSize < 0 || uncompressedSize > _maxAllowableBlockSize 
                        || compressedSize < 0 || compressedSize > _maxAllowableBlockSize)
                        throw new FileFormatException(SR.Get(SRID.CorruptStream)); 
                } 
            }
            else 
            {
                uncompressedSize = compressedSize = 0;
            }
 
            return (bytesRead > 0);
        } 
 
        /// <summary>
        /// Throw exception based on ZLib error code 
        /// </summary>
        /// <param name="retVal"></param>
        private static void ThrowIfZLibError(UnsafeNativeMethods.ZLib.ErrorCode retVal)
        { 
            // switch does not support fall-through
            bool invalidOperation = false; 
            bool corruption = false; 

            switch (retVal) 
            {
                case UnsafeNativeMethods.ZLib.ErrorCode.Success:
                    return;
 
                case UnsafeNativeMethods.ZLib.ErrorCode.StreamEnd:
                    invalidOperation = true; break; 
 
                case UnsafeNativeMethods.ZLib.ErrorCode.NeedDictionary:
                    corruption = true; break; 

                case UnsafeNativeMethods.ZLib.ErrorCode.StreamError:
                    corruption = true; break;
 
                case UnsafeNativeMethods.ZLib.ErrorCode.DataError:
                    corruption = true; break; 
 
                case UnsafeNativeMethods.ZLib.ErrorCode.MemError:
                    throw new OutOfMemoryException(); 

               case UnsafeNativeMethods.ZLib.ErrorCode.BufError:
                    invalidOperation = true; break;
 
                case UnsafeNativeMethods.ZLib.ErrorCode.VersionError:
                    throw new InvalidOperationException(SR.Get(SRID.ZLibVersionError, 
                        UnsafeNativeMethods.ZLib.ZLibVersion)); 

                default: 
                    {
                        // ErrorNo
                        throw new IOException();
                    } 
            }
 
            if (invalidOperation) 
                throw new InvalidOperationException();
 
            if (corruption)
                throw new FileFormatException(SR.Get(SRID.CorruptStream));

        } 

        //----------------------------------------------------- 
        // 
        //  Private Fields
        // 
        //------------------------------------------------------

        // for reading each block header
        private byte[] _headerBuf = new byte[_blockHeaderSize];         // 3 ulongs 

        // static 
        private const int _defaultBlockSize = 0x1000;         // 4k default 
        private const int _maxAllowableBlockSize = 0xFFFFF;   // The spec is open ended about supported block sizes but we
                                                              // want to defend against malicious input so we restrict input to 1MB. 
        private const int _compressionLevel = 9;              // mandated by format
        private const int _ulongSize = 4;                     // a ULONG in unmanaged C++ is 4 bytes
        private const UInt32 _blockHeaderToken = 0x0FA0;      // signature at start of each block header
        private const int _blockHeaderSize = _ulongSize * 3;  // length of block header 
    }
} 

