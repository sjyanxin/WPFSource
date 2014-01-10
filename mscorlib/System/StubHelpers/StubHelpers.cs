// ==++== 
//
//   Copyright (c) Microsoft Corporation.  All rights reserved.
//
// ==--== 

namespace  System.StubHelpers { 
 
    using System.Text;
    using Microsoft.Win32; 
    using System.Security;
    using System.Collections.Generic;
    using System.Runtime;
    using System.Runtime.InteropServices; 
    using System.Runtime.CompilerServices;
    using System.Runtime.ConstrainedExecution; 
    using System.Diagnostics.Contracts; 

    [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)] 
    internal static class AnsiCharMarshaler
    {
        // The length of the returned array is an approximation based on the length of the input string and the system
        // character set. It is only guaranteed to be larger or equal to cbLength, don't depend on the exact value. 
        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization] 
        #endif //!FEATURE_CORECLR 
        [System.Security.SecurityCritical]
        static internal byte[] DoAnsiConversion(string str, bool fBestFit, bool fThrowOnUnmappableChar, out int cbLength) 
        {
            return str.ConvertToAnsi(Marshal.SystemMaxDBCSCharSize, fBestFit, fThrowOnUnmappableChar, out cbLength);
        }
 
        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization] 
        #endif //!FEATURE_CORECLR 
        [System.Security.SecurityCritical]
        static internal byte ConvertToNative(char managedChar, bool fBestFit, bool fThrowOnUnmappableChar) 
        {
            int cbLength;
            byte[] bytes = DoAnsiConversion(managedChar.ToString(), fBestFit, fThrowOnUnmappableChar, out cbLength);
 
            BCLDebug.Assert(cbLength > 0, "Zero bytes returned from DoAnsiConversion in AnsiCharMarshaler.ConvertToNative");
            return bytes[0]; 
        } 

        #if !FEATURE_CORECLR 
        [System.Runtime.ForceTokenStabilization]
        #endif //!FEATURE_CORECLR
        static internal char ConvertToManaged(byte nativeChar)
        { 
            byte[] bytes = new byte[1] { nativeChar };
            string str = Encoding.Default.GetString(bytes); 
            return str[0]; 
        }
    } 

    [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
    internal static class CSTRMarshaler
    { 
        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization] 
        #endif //!FEATURE_CORECLR 
        [System.Security.SecurityCritical]  // auto-generated
        static internal unsafe IntPtr ConvertToNative(int flags, string strManaged, IntPtr pNativeBuffer) 
        {
            if (null == strManaged)
            {
                return IntPtr.Zero; 
            }
 
            StubHelpers.CheckStringLength(strManaged.Length); 

            int nb; 
            byte[] bytes = AnsiCharMarshaler.DoAnsiConversion(strManaged, 0 != (flags & 0xFF), 0 != (flags >> 8), out nb);

            // Use the pre-allocated buffer (allocated by localloc IL instruction) if not NULL,
            // otherwise fallback to AllocCoTaskMem 
            byte *pbNativeBuffer = (byte *)pNativeBuffer;
            if (pbNativeBuffer == null) 
            { 
                // + 1 for the null character from the user.  + 1 for the null character we put in.
                pbNativeBuffer = (byte*)Marshal.AllocCoTaskMem(nb + 2); 
            }

            Buffer.memcpy(bytes, 0, pbNativeBuffer, 0, nb);
 
            pbNativeBuffer[nb]     = 0x00;
            pbNativeBuffer[nb + 1] = 0x00; 
 
            return (IntPtr)pbNativeBuffer;
        } 

        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization]
        #endif //!FEATURE_CORECLR 
        [System.Security.SecurityCritical]  // auto-generated
        static internal unsafe string ConvertToManaged(IntPtr cstr) 
        { 
            if (IntPtr.Zero == cstr)
                return null; 
            else
                return new String((sbyte*)cstr);
        }
 
        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization] 
        #endif //!FEATURE_CORECLR 
        [System.Security.SecurityCritical]  // auto-generated
        static internal void ClearNative(IntPtr pNative) 
        {
            Win32Native.CoTaskMemFree(pNative);
        }
    } 

    [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)] 
    internal static class BSTRMarshaler 
    {
        #if !FEATURE_CORECLR 
        [System.Runtime.ForceTokenStabilization]
        #endif //!FEATURE_CORECLR
        [System.Security.SecurityCritical]  // auto-generated
        static internal unsafe IntPtr ConvertToNative(string strManaged, IntPtr pNativeBuffer) 
        {
            if (null == strManaged) 
            { 
                return IntPtr.Zero;
            } 
            else
            {
                StubHelpers.CheckStringLength(strManaged.Length);
 
                byte trailByte;
                bool hasTrailByte = strManaged.TryGetTrailByte(out trailByte); 
 
                uint lengthInBytes = (uint)strManaged.Length * 2;
 
                if (hasTrailByte)
                {
                    // this is an odd-sized string with a trailing byte stored in its [....] block
                    lengthInBytes++; 
                }
 
                byte *ptrToFirstChar; 

                if (pNativeBuffer != IntPtr.Zero) 
                {
                    // If caller provided a buffer, construct the BSTR manually. The size
                    // of the buffer must be at least (lengthInBytes + 6) bytes.
#if _DEBUG 
                    uint length = *((uint *)pNativeBuffer.ToPointer());
                    BCLDebug.Assert(length >= lengthInBytes + 6, "BSTR localloc'ed buffer is too small"); 
#endif // _DEBUG 

                    // set length 
                    *((uint *)pNativeBuffer.ToPointer()) = lengthInBytes;

                    ptrToFirstChar = (byte *)pNativeBuffer.ToPointer() + 4;
                } 
                else
                { 
                    // If not provided, allocate the buffer using SysAllocStringByteLen so 
                    // that odd-sized strings will be handled as well.
                    ptrToFirstChar = (byte *)Win32Native.SysAllocStringByteLen(null, lengthInBytes).ToPointer(); 
                }

                // copy characters from the managed string
                fixed (char* ch = strManaged) 
                {
                    Buffer.memcpyimpl( 
                        (byte *)ch, 
                        ptrToFirstChar,
                        (strManaged.Length + 1) * 2); 
                }

                // copy the trail byte if present
                if (hasTrailByte) 
                {
                    ptrToFirstChar[lengthInBytes - 1] = trailByte; 
                } 

                // return ptr to first character 
                return (IntPtr)ptrToFirstChar;
            }
        }
 
        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization] 
        #endif //!FEATURE_CORECLR 
        [System.Security.SecurityCritical]  // auto-generated
        static internal unsafe string ConvertToManaged(IntPtr bstr) 
        {
            if (IntPtr.Zero == bstr)
            {
                return null; 
            }
            else 
            { 
                uint length = Win32Native.SysStringByteLen(bstr);
 
                // Intentionally checking the number of bytes not characters to match the behavior
                // of ML marshalers. This prevents roundtripping of very large strings as the check
                // in the managed->native direction is done on String length but considering that
                // it's completely moot on 32-bit and not expected to be important on 64-bit either, 
                // the ability to catch random garbage in the BSTR's length field outweighs this
                // restriction. If an ordinary null-terminated string is passed instead of a BSTR, 
                // chances are that the length field - possibly being unallocated memory - contains 
                // a heap fill pattern that will have the highest bit set, caught by the check.
                StubHelpers.CheckStringLength(length); 

                string ret = new String((char*)bstr, 0, (int)(length / 2));
                if ((length & 1) == 1)
                { 
                    // odd-sized strings need to have the trailing byte saved in their [....] block
                    ret.SetTrailByte(((byte *)bstr.ToPointer())[length - 1]); 
                } 
                return ret;
            } 
        }

        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization] 
        #endif //!FEATURE_CORECLR
        [System.Security.SecurityCritical]  // auto-generated 
        static internal void ClearNative(IntPtr pNative) 
        {
            if (IntPtr.Zero != pNative) 
            {
                Win32Native.SysFreeString(pNative);
            }
        } 
    }
 
    [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)] 
    internal static class VBByValStrMarshaler
    { 
        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization]
        #endif //!FEATURE_CORECLR
        [System.Security.SecurityCritical]  // auto-generated 
        static internal unsafe IntPtr ConvertToNative(string strManaged, bool fBestFit, bool fThrowOnUnmappableChar, ref int cch)
        { 
            if (null == strManaged) 
            {
                return IntPtr.Zero; 
            }

            byte* pNative;
 
            cch = strManaged.Length;
 
            StubHelpers.CheckStringLength(cch); 

            // length field at negative offset + (# of characters incl. the terminator) * max ANSI char size 
            int nbytes = sizeof(uint) + ((cch + 1) * Marshal.SystemMaxDBCSCharSize);

            pNative = (byte*)Marshal.AllocCoTaskMem(nbytes);
            int* pLength = (int*)pNative; 

            pNative = pNative + sizeof(uint); 
 
            if (0 == cch)
            { 
                *pNative = 0;
                *pLength = 0;
            }
            else 
            {
                int nbytesused; 
                byte[] bytes = AnsiCharMarshaler.DoAnsiConversion(strManaged, fBestFit, fThrowOnUnmappableChar, out nbytesused); 

                BCLDebug.Assert(nbytesused < nbytes, "Insufficient buffer allocated in VBByValStrMarshaler.ConvertToNative"); 
                Buffer.memcpy(bytes, 0, pNative, 0, nbytesused);

                pNative[nbytesused] = 0;
                *pLength = nbytesused; 
            }
 
            return new IntPtr(pNative); 
        }
 
        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization]
        #endif //!FEATURE_CORECLR
        [System.Security.SecurityCritical]  // auto-generated 
        static internal unsafe string ConvertToManaged(IntPtr pNative, int cch)
        { 
            if (IntPtr.Zero == pNative) 
            {
                return null; 
            }

            return new String((sbyte*)pNative, 0, cch);
        } 

        #if !FEATURE_CORECLR 
        [System.Runtime.ForceTokenStabilization] 
        #endif //!FEATURE_CORECLR
        [System.Security.SecurityCritical]  // auto-generated 
        static internal unsafe void ClearNative(IntPtr pNative)
        {
            if (IntPtr.Zero != pNative)
            { 
                Win32Native.CoTaskMemFree((IntPtr)(((long)pNative) - sizeof(uint)));
            } 
        } 
    }
 
    [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
    internal static class AnsiBSTRMarshaler
    {
        #if !FEATURE_CORECLR 
        [System.Runtime.ForceTokenStabilization]
        #endif //!FEATURE_CORECLR 
        [System.Security.SecurityCritical]  // auto-generated 
        static internal unsafe IntPtr ConvertToNative(int flags, string strManaged)
        { 
            if (null == strManaged)
            {
                return IntPtr.Zero;
            } 

            int length = strManaged.Length; 
 
            StubHelpers.CheckStringLength(length);
 
            byte[]  bytes = null;
            int     nb = 0;

            if (length > 0) 
            {
                bytes = AnsiCharMarshaler.DoAnsiConversion(strManaged, 0 != (flags & 0xFF), 0 != (flags >> 8), out nb); 
            } 

            return Win32Native.SysAllocStringByteLen(bytes, (uint)nb); 
        }

        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization] 
        #endif //!FEATURE_CORECLR
        [System.Security.SecurityCritical]  // auto-generated 
        static internal unsafe string ConvertToManaged(IntPtr bstr) 
        {
            if (IntPtr.Zero == bstr) 
            {
                return null;
            }
            else 
            {
                // We intentionally ignore the length field of the BSTR for back compat reasons. 
                // Unfortunately VB.NET uses Ansi BSTR marshaling when a string is passed ByRef 
                // and we cannot afford to break this common scenario.
                return new String((sbyte*)bstr); 
            }
        }

        #if !FEATURE_CORECLR 
        [System.Runtime.ForceTokenStabilization]
        #endif //!FEATURE_CORECLR 
        [System.Security.SecurityCritical]  // auto-generated 
        static internal unsafe void ClearNative(IntPtr pNative)
        { 
            if (IntPtr.Zero != pNative)
            {
                Win32Native.SysFreeString(pNative);
            } 
        }
    } 
 
    [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
    internal static class WSTRBufferMarshaler 
    {
        static internal IntPtr ConvertToNative(string strManaged)
        {
            Contract.Assert(false, "NYI"); 
            return IntPtr.Zero;
        } 
 
        static internal unsafe string ConvertToManaged(IntPtr bstr)
        { 
            Contract.Assert(false, "NYI");
            return null;
        }
 
        static internal void ClearNative(IntPtr pNative)
        { 
            Contract.Assert(false, "NYI"); 
        }
    } 

#if FEATURE_COMINTEROP
    [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
    internal static class ObjectMarshaler 
    {
        #if !FEATURE_CORECLR 
        [System.Runtime.ForceTokenStabilization] 
        #endif //!FEATURE_CORECLR
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        static internal extern void ConvertToNative(object objSrc, IntPtr pDstVariant);

        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization] 
        #endif //!FEATURE_CORECLR
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        static internal extern object ConvertToManaged(IntPtr pSrcVariant); 

        #if !FEATURE_CORECLR 
        [System.Runtime.ForceTokenStabilization]
        #endif //!FEATURE_CORECLR
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        static internal extern void ClearNative(IntPtr pVariant); 
    }
#endif // FEATURE_COMINTEROP 
 
    [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
    internal static class ValueClassMarshaler 
    {
        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization]
        #endif //!FEATURE_CORECLR 
        [SecurityCritical]
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        static internal extern void ConvertToNative(IntPtr dst, IntPtr src, IntPtr pMT, ref CleanupWorkList pCleanupWorkList); 

        #if !FEATURE_CORECLR 
        [System.Runtime.ForceTokenStabilization]
        #endif //!FEATURE_CORECLR
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        static internal extern void ConvertToManaged(IntPtr dst, IntPtr src, IntPtr pMT); 

        #if !FEATURE_CORECLR 
        [System.Runtime.ForceTokenStabilization] 
        #endif //!FEATURE_CORECLR
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        static internal extern void ClearNative(IntPtr dst, IntPtr pMT);
    }

    [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)] 
    internal static class DateMarshaler
    { 
        #if !FEATURE_CORECLR 
        [System.Runtime.ForceTokenStabilization]
        #endif //!FEATURE_CORECLR 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        static internal extern double ConvertToNative(DateTime managedDate);

        // The return type is really DateTime but we use long to avoid the pain associated with returning structures. 
        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization] 
        #endif //!FEATURE_CORECLR 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        static internal extern long ConvertToManaged(double nativeDate); 
    }

#if FEATURE_COMINTEROP
    [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)] 
    internal static class InterfaceMarshaler
    { 
        #if !FEATURE_CORECLR 
        [System.Runtime.ForceTokenStabilization]
        #endif //!FEATURE_CORECLR 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        static internal extern IntPtr ConvertToNative(object objSrc, IntPtr itfMT, IntPtr classMT, int flags);

        #if !FEATURE_CORECLR 
        [System.Runtime.ForceTokenStabilization]
        #endif //!FEATURE_CORECLR 
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        static internal extern object ConvertToManaged(IntPtr pUnk, IntPtr itfMT, IntPtr classMT, int flags);
 
        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization]
        #endif //!FEATURE_CORECLR
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        static internal extern void ClearNative(IntPtr pUnk);
    } 
#endif // FEATURE_COMINTEROP 

    [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)] 
    internal static class MngdNativeArrayMarshaler
    {
        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization] 
        #endif //!FEATURE_CORECLR
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        static internal extern void CreateMarshaler(IntPtr pMarshalState, IntPtr pMT, int dwFlags); 

        #if !FEATURE_CORECLR 
        [System.Runtime.ForceTokenStabilization]
        #endif //!FEATURE_CORECLR
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        static internal extern void ConvertSpaceToNative(IntPtr pMarshalState, ref object pManagedHome, IntPtr pNativeHome); 

        #if !FEATURE_CORECLR 
        [System.Runtime.ForceTokenStabilization] 
        #endif //!FEATURE_CORECLR
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        static internal extern void ConvertContentsToNative(IntPtr pMarshalState, ref object pManagedHome, IntPtr pNativeHome);

        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization] 
        #endif //!FEATURE_CORECLR
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        static internal extern void ConvertSpaceToManaged(IntPtr pMarshalState, ref object pManagedHome, IntPtr pNativeHome, 
                                                          int cElements);
 
        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization]
        #endif //!FEATURE_CORECLR
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        static internal extern void ConvertContentsToManaged(IntPtr pMarshalState, ref object pManagedHome, IntPtr pNativeHome);
 
        #if !FEATURE_CORECLR 
        [System.Runtime.ForceTokenStabilization]
        #endif //!FEATURE_CORECLR 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        static internal extern void ClearNative(IntPtr pMarshalState, IntPtr pNativeHome, int cElements);

        #if !FEATURE_CORECLR 
        [System.Runtime.ForceTokenStabilization]
        #endif //!FEATURE_CORECLR 
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        static internal extern void ClearNativeContents(IntPtr pMarshalState, IntPtr pNativeHome, int cElements);
    } 

#if FEATURE_COMINTEROP
    [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
    internal static class MngdSafeArrayMarshaler 
    {
        #if !FEATURE_CORECLR 
        [System.Runtime.ForceTokenStabilization] 
        #endif //!FEATURE_CORECLR
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        static internal extern void CreateMarshaler(IntPtr pMarshalState, IntPtr pMT, int iRank, int dwFlags);

        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization] 
        #endif //!FEATURE_CORECLR
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        static internal extern void ConvertSpaceToNative(IntPtr pMarshalState, ref object pManagedHome, IntPtr pNativeHome); 

        #if !FEATURE_CORECLR 
        [System.Runtime.ForceTokenStabilization]
        #endif //!FEATURE_CORECLR
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        static internal extern void ConvertContentsToNative(IntPtr pMarshalState, ref object pManagedHome, IntPtr pNativeHome, object pOriginalManaged); 

        #if !FEATURE_CORECLR 
        [System.Runtime.ForceTokenStabilization] 
        #endif //!FEATURE_CORECLR
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        static internal extern void ConvertSpaceToManaged(IntPtr pMarshalState, ref object pManagedHome, IntPtr pNativeHome);

        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization] 
        #endif //!FEATURE_CORECLR
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        static internal extern void ConvertContentsToManaged(IntPtr pMarshalState, ref object pManagedHome, IntPtr pNativeHome); 

        #if !FEATURE_CORECLR 
        [System.Runtime.ForceTokenStabilization]
        #endif //!FEATURE_CORECLR
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        static internal extern void ClearNative(IntPtr pMarshalState, ref object pManagedHome, IntPtr pNativeHome); 
    }
#endif // FEATURE_COMINTEROP 
 
    [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
    internal static class MngdRefCustomMarshaler 
    {
        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization]
        #endif //!FEATURE_CORECLR 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        static internal extern void CreateMarshaler(IntPtr pMarshalState, IntPtr pCMHelper); 
 
        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization] 
        #endif //!FEATURE_CORECLR
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        static internal extern void ConvertContentsToNative(IntPtr pMarshalState, ref object pManagedHome, IntPtr pNativeHome);
 
        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization] 
        #endif //!FEATURE_CORECLR 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        static internal extern void ConvertContentsToManaged(IntPtr pMarshalState, ref object pManagedHome, IntPtr pNativeHome); 

        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization]
        #endif //!FEATURE_CORECLR 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        static internal extern void ClearNative(IntPtr pMarshalState, ref object pManagedHome, IntPtr pNativeHome); 
 
        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization] 
        #endif //!FEATURE_CORECLR
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        static internal extern void ClearManaged(IntPtr pMarshalState, ref object pManagedHome, IntPtr pNativeHome);
    } 

    [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)] 
    #if !FEATURE_CORECLR 
    [System.Runtime.ForceTokenStabilization]
    #endif //!FEATURE_CORECLR 
    [System.Security.SecurityCritical]
    internal struct AsAnyMarshaler
    {
        private const ushort VTHACK_ANSICHAR = 253; 
        private const ushort VTHACK_WINBOOL  = 254;
 
        private enum BackPropAction 
        {
            None, 
            Array,
            Layout,
            StringBuilderAnsi,
            StringBuilderUnicode 
        }
 
        // Pointer to MngdNativeArrayMarshaler, ownership not assumed. 
        private IntPtr pvArrayMarshaler;
 
        // Type of action to perform after the CLR-to-unmanaged call.
        private BackPropAction backPropAction;

        // The managed layout type for BackPropAction.Layout. 
        private Type layoutType;
 
        // Cleanup list to be destroyed when clearing the native view (for layouts with SafeHandles). 
        private CleanupWorkList cleanupWorkList;
 
        private static bool IsIn(int dwFlags)      { return ((dwFlags & 0x10000000) != 0); }
        private static bool IsOut(int dwFlags)     { return ((dwFlags & 0x20000000) != 0); }
        private static bool IsAnsi(int dwFlags)    { return ((dwFlags & 0x00FF0000) != 0); }
        private static bool IsThrowOn(int dwFlags) { return ((dwFlags & 0x0000FF00) != 0); } 
        private static bool IsBestFit(int dwFlags) { return ((dwFlags & 0x000000FF) != 0); }
 
        #if !FEATURE_CORECLR 
        [System.Runtime.ForceTokenStabilization]
        #endif //!FEATURE_CORECLR 
        internal AsAnyMarshaler(IntPtr pvArrayMarshaler)
        {
            // we need this in case the value being marshaled turns out to be array
            BCLDebug.Assert(pvArrayMarshaler != IntPtr.Zero, "pvArrayMarshaler must not be null"); 

            this.pvArrayMarshaler = pvArrayMarshaler; 
            this.backPropAction = BackPropAction.None; 
            this.layoutType = null;
            this.cleanupWorkList = null; 
        }

        #region ConvertToNative helpers
 
        [System.Security.SecurityCritical]
        private unsafe IntPtr ConvertArrayToNative(object pManagedHome, int dwFlags) 
        { 
            Type elementType = pManagedHome.GetType().GetElementType();
            VarEnum vt = VarEnum.VT_EMPTY; 

            switch (Type.GetTypeCode(elementType))
            {
                case TypeCode.SByte:   vt = VarEnum.VT_I1;  break; 
                case TypeCode.Byte:    vt = VarEnum.VT_UI1; break;
                case TypeCode.Int16:   vt = VarEnum.VT_I2;  break; 
                case TypeCode.UInt16:  vt = VarEnum.VT_UI2; break; 
                case TypeCode.Int32:   vt = VarEnum.VT_I4;  break;
                case TypeCode.UInt32:  vt = VarEnum.VT_UI4; break; 
                case TypeCode.Int64:   vt = VarEnum.VT_I8;  break;
                case TypeCode.UInt64:  vt = VarEnum.VT_UI8; break;
                case TypeCode.Single:  vt = VarEnum.VT_R4;  break;
                case TypeCode.Double:  vt = VarEnum.VT_R8;  break; 
                case TypeCode.Char:    vt = (IsAnsi(dwFlags) ? (VarEnum)VTHACK_ANSICHAR : VarEnum.VT_UI2); break;
                case TypeCode.Boolean: vt = (VarEnum)VTHACK_WINBOOL; break; 
 
                case TypeCode.Object:
                { 
                    if (elementType == typeof(IntPtr))
                    {
                        vt = (IntPtr.Size == 4 ? VarEnum.VT_I4 : VarEnum.VT_I8);
                    } 
                    else if (elementType == typeof(UIntPtr))
                    { 
                        vt = (IntPtr.Size == 4 ? VarEnum.VT_UI4 : VarEnum.VT_UI8); 
                    }
                    else goto default; 
                    break;
                }

                default: 
                    throw new ArgumentException(Environment.GetResourceString("Arg_NDirectBadObject"));
            } 
 
            // marshal the object as C-style array (UnmanagedType.LPArray)
            int dwArrayMarshalerFlags = (int)vt; 
            if (IsBestFit(dwFlags)) dwArrayMarshalerFlags |= (1 << 16);
            if (IsThrowOn(dwFlags)) dwArrayMarshalerFlags |= (1 << 24);

            MngdNativeArrayMarshaler.CreateMarshaler( 
                pvArrayMarshaler,
                IntPtr.Zero,      // not needed as we marshal primitive VTs only 
                dwArrayMarshalerFlags); 

            IntPtr pNativeHome; 
            IntPtr pNativeHomeAddr = new IntPtr(&pNativeHome);

            MngdNativeArrayMarshaler.ConvertSpaceToNative(
                pvArrayMarshaler, 
                ref pManagedHome,
                pNativeHomeAddr); 
 
            if (IsIn(dwFlags))
            { 
                MngdNativeArrayMarshaler.ConvertContentsToNative(
                    pvArrayMarshaler,
                    ref pManagedHome,
                    pNativeHomeAddr); 
            }
            if (IsOut(dwFlags)) 
            { 
                backPropAction = BackPropAction.Array;
            } 

            return pNativeHome;
        }
 
        [System.Security.SecurityCritical]
        private static IntPtr ConvertStringToNative(string pManagedHome, int dwFlags) 
        { 
            IntPtr pNativeHome;
 
            // IsIn, IsOut are ignored for strings - they're always in-only
            if (IsAnsi(dwFlags))
            {
                // marshal the object as Ansi string (UnmanagedType.LPStr) 
                pNativeHome = CSTRMarshaler.ConvertToNative(
                    dwFlags & 0xFFFF, // (throw on unmappable char << 8 | best fit) 
                    pManagedHome,     // 
                    IntPtr.Zero);     // unmanaged buffer will be allocated
            } 
            else
            {
                // marshal the object as Unicode string (UnmanagedType.LPWStr)
                StubHelpers.CheckStringLength(pManagedHome.Length); 

                int allocSize = (pManagedHome.Length + 1) * 2; 
                pNativeHome = Marshal.AllocCoTaskMem(allocSize); 

                String.InternalCopy(pManagedHome, pNativeHome, allocSize); 
            }

            return pNativeHome;
        } 

        [System.Security.SecurityCritical] 
        private unsafe IntPtr ConvertStringBuilderToNative(StringBuilder pManagedHome, int dwFlags) 
        {
            IntPtr pNativeHome; 

            // P/Invoke can be used to call Win32 apis that don't strictly follow CLR in/out semantics and thus may
            // leave garbage in the buffer in circumstances that we can't detect. To prevent us from crashing when
            // converting the contents back to managed, put a hidden NULL terminator past the end of the official buffer. 

            // Unmanaged layout: 
            // +====================================+ 
            // | Extra hidden NULL                  |
            // +====================================+ \ 
            // |                                    | |
            // | [Converted] NULL-terminated string | |- buffer that the target may change
            // |                                    | |
            // +====================================+ / <-- native home 

            // Note that StringBuilder.Capacity is the number of characters NOT including any terminators. 
 
            if (IsAnsi(dwFlags))
            { 
                StubHelpers.CheckStringLength(pManagedHome.Capacity);

                // marshal the object as Ansi string (UnmanagedType.LPStr)
                int allocSize = (pManagedHome.Capacity * Marshal.SystemMaxDBCSCharSize) + 4; 
                pNativeHome = Marshal.AllocCoTaskMem(allocSize);
 
                byte* ptr = (byte*)pNativeHome; 
                *(ptr + allocSize - 3) = 0;
                *(ptr + allocSize - 2) = 0; 
                *(ptr + allocSize - 1) = 0;

                if (IsIn(dwFlags))
                { 
                    int length;
 
                    byte[] bytes = AnsiCharMarshaler.DoAnsiConversion( 
                        pManagedHome.ToString(),
                        IsBestFit(dwFlags), 
                        IsThrowOn(dwFlags),
                        out length);

                    Buffer.memcpy( 
                        bytes,         // src array
                        0,             // src index 
                        ptr,           // dst buffer 
                        0,             // dts index
                        length);       // len 

                    // null-terminate the native string
                    *(ptr + length) = 0;
                } 
                if (IsOut(dwFlags))
                { 
                    backPropAction = BackPropAction.StringBuilderAnsi; 
                }
            } 
            else
            {
                // marshal the object as Unicode string (UnmanagedType.LPWStr)
                int allocSize = (pManagedHome.Capacity * 2) + 4; 
                pNativeHome = Marshal.AllocCoTaskMem(allocSize);
 
                byte* ptr = (byte*)pNativeHome; 
                *(ptr + allocSize - 1) = 0;
                *(ptr + allocSize - 2) = 0; 

                if (IsIn(dwFlags))
                {
                    int length = pManagedHome.Length * 2; 
                    pManagedHome.InternalCopy(pNativeHome, length);
 
                    // null-terminate the native string 
                    *(ptr + length + 0) = 0;
                    *(ptr + length + 1) = 0; 
                }
                if (IsOut(dwFlags))
                {
                    backPropAction = BackPropAction.StringBuilderUnicode; 
                }
            } 
 
            return pNativeHome;
        } 

        [System.Security.SecurityCritical]
        private unsafe IntPtr ConvertLayoutToNative(object pManagedHome, int dwFlags)
        { 
            // Note that the following call will not throw exception if the type
            // of pManagedHome is not marshalable. That's intentional because we 
            // want to maintain the original behavior where this was indicated 
            // by TypeLoadException during the actual field marshaling.
            int allocSize = Marshal.SizeOfHelper(pManagedHome.GetType(), false); 
            IntPtr pNativeHome = Marshal.AllocCoTaskMem(allocSize);

            // marshal the object as class with layout (UnmanagedType.LPStruct)
            if (IsIn(dwFlags)) 
            {
                StubHelpers.FmtClassUpdateNativeInternal(pManagedHome, (byte *)pNativeHome.ToPointer(), ref cleanupWorkList); 
            } 
            if (IsOut(dwFlags))
            { 
                backPropAction = BackPropAction.Layout;
            }
            layoutType = pManagedHome.GetType();
 
            return pNativeHome;
        } 
 
        #endregion
 
        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization]
        #endif //!FEATURE_CORECLR
        [System.Security.SecurityCritical] 
        internal IntPtr ConvertToNative(object pManagedHome, int dwFlags)
        { 
            if (pManagedHome == null) 
                return IntPtr.Zero;
 
            if (pManagedHome is ArrayWithOffset)
                throw new ArgumentException(Environment.GetResourceString("Arg_MarshalAsAnyRestriction"));

            IntPtr pNativeHome; 

            if (pManagedHome.GetType().IsArray) 
            { 
                // array (LPArray)
                pNativeHome = ConvertArrayToNative(pManagedHome, dwFlags); 
            }
            else
            {
                string strValue; 
                StringBuilder sbValue;
 
                if ((strValue = pManagedHome as string) != null) 
                {
                    // string (LPStr or LPWStr) 
                    pNativeHome = ConvertStringToNative(strValue, dwFlags);
                }
                else if ((sbValue = pManagedHome as StringBuilder) != null)
                { 
                    // StringBuilder (LPStr or LPWStr)
                    pNativeHome = ConvertStringBuilderToNative(sbValue, dwFlags); 
                } 
                else if (pManagedHome.GetType().IsLayoutSequential || pManagedHome.GetType().IsExplicitLayout)
                { 
                    // layout (LPStruct)
                    pNativeHome = ConvertLayoutToNative(pManagedHome, dwFlags);
                }
                else 
                {
                    // this type is not supported for AsAny marshaling 
                    throw new ArgumentException(Environment.GetResourceString("Arg_NDirectBadObject")); 
                }
            } 

            return pNativeHome;
        }
 
        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization] 
        #endif //!FEATURE_CORECLR 
        [System.Security.SecurityCritical]
        internal unsafe void ConvertToManaged(object pManagedHome, IntPtr pNativeHome) 
        {
            switch (backPropAction)
            {
                case BackPropAction.Array: 
                {
                    MngdNativeArrayMarshaler.ConvertContentsToManaged( 
                        pvArrayMarshaler, 
                        ref pManagedHome,
                        new IntPtr(&pNativeHome)); 
                    break;
                }

                case BackPropAction.Layout: 
                {
                    StubHelpers.FmtClassUpdateCLRInternal(pManagedHome, (byte *)pNativeHome.ToPointer()); 
                    break; 
                }
 
                case BackPropAction.StringBuilderAnsi:
                {
                    sbyte* ptr = (sbyte*)pNativeHome.ToPointer();
                    ((StringBuilder)pManagedHome).ReplaceBufferAnsiInternal(ptr, Win32Native.lstrlenA(pNativeHome)); 
                    break;
                } 
 
                case BackPropAction.StringBuilderUnicode:
                { 
                    char* ptr = (char*)pNativeHome.ToPointer();
                    ((StringBuilder)pManagedHome).ReplaceBufferInternal(ptr, Win32Native.lstrlenW(pNativeHome));
                    break;
                } 

                // nothing to do for BackPropAction.None 
            } 
        }
 
        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization]
        #endif //!FEATURE_CORECLR
        [System.Security.SecurityCritical] 
        internal void ClearNative(IntPtr pNativeHome)
        { 
            if (pNativeHome != IntPtr.Zero) 
            {
                if (layoutType != null) 
                {
                    // this must happen regardless of BackPropAction
                    Marshal.DestroyStructure(pNativeHome, layoutType);
                } 
                Win32Native.CoTaskMemFree(pNativeHome);
            } 
            StubHelpers.DestroyCleanupList(ref cleanupWorkList); 
        }
    } 

    [StructLayout(LayoutKind.Sequential)]
    #if !FEATURE_CORECLR
    [System.Runtime.ForceTokenStabilization] 
    #endif //!FEATURE_CORECLR
    internal struct NativeVariant 
    { 
        ushort vt;
        ushort wReserved1; 
        ushort wReserved2;
        ushort wReserved3;
        IntPtr data1;
        IntPtr data2; 
    }
 
#if !WIN64 && !FEATURE_CORECLR 
    // Structure filled by IL stubs if copy constructor(s) and destructor(s) need to be called
    // on value types pushed on the stack. The structure is stored in s_copyCtorStubDesc by 
    // SetCopyCtorCookieChain and fetched by CopyCtorCallStubWorker. Must be stack-allocated.
    [StructLayout(LayoutKind.Sequential)]
    [System.Runtime.ForceTokenStabilization]
    unsafe internal struct CopyCtorStubCookie 
    {
        [System.Runtime.ForceTokenStabilization] 
        public void SetData(IntPtr srcInstancePtr, uint dstStackOffset, IntPtr ctorPtr, IntPtr dtorPtr) 
        {
            m_srcInstancePtr = srcInstancePtr; 
            m_dstStackOffset = dstStackOffset;
            m_ctorPtr = ctorPtr;
            m_dtorPtr = dtorPtr;
        } 

        [System.Runtime.ForceTokenStabilization] 
        public void SetNext(IntPtr pNext) 
        {
            m_pNext = pNext; 
        }

        public IntPtr m_srcInstancePtr; // pointer to the source instance
        public uint   m_dstStackOffset; // offset from the start of stack arguments of the pushed 'this' instance 

        public IntPtr m_ctorPtr;        // fnptr to the managed copy constructor, result of ldftn 
        public IntPtr m_dtorPtr;        // fnptr to the managed destructor, result of ldftn 

        public IntPtr m_pNext;          // pointer to next cookie in the chain or IntPtr.Zero 
    }

    // Aggregates pointer to CopyCtorStubCookie and the target of the interop call.
    [StructLayout(LayoutKind.Sequential)] 
    unsafe internal struct CopyCtorStubDesc
    { 
        public IntPtr m_pCookie; 
        public IntPtr m_pTarget;
    } 
#endif // !WIN64 && !FEATURE_CORECLR

#if WIN64 // x86 IL stubs do not manipulate frames
    // 
    // This struct maps the the EE's Frame struct in vm\Frames.h
    // 
    [StructLayout(LayoutKind.Sequential)] 
    [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
    internal unsafe struct EEFrame 
    {
        internal void* __VFN_table;
        internal void* m_Next;
 
        internal const long OFFSETOF__Thread__m_pFrame = 0x10L;
 
        // 
        // pass in an explicit 'this' pointer so that we don't have to
        // use 'fixed' to take the address of 'this' 
        //
        [System.Security.SecurityCritical]  // auto-generated
        internal static void Push(void* pThis, void* pThread)
        { 
            EEFrame* pThisFrame = (EEFrame*)pThis;
#if _DEBUG 
            StubHelpers.Verify__EEFrame__Push(pThisFrame, pThread, OFFSETOF__Thread__m_pFrame); 
#endif // _DEBUG
 
            void** ppFrame = (void**)(((byte*)pThread) + OFFSETOF__Thread__m_pFrame);
            pThisFrame->m_Next = *ppFrame;
            *ppFrame = pThis;
        } 

        // 
        // uses an explicit 'this' pointer to be consistent with Push above 
        //
        #if !FEATURE_CORECLR 
        [System.Runtime.ForceTokenStabilization]
        #endif //!FEATURE_CORECLR
        [System.Security.SecurityCritical]  // auto-generated
        internal static void Pop(void* pThis, void* pThread) 
        {
            EEFrame* pThisFrame = (EEFrame*)pThis; 
#if _DEBUG 
            StubHelpers.Verify__EEFrame__Pop(pThisFrame, pThread, OFFSETOF__Thread__m_pFrame);
#endif // _DEBUG 

            void** ppFrame = (void**)(((byte*)pThread) + OFFSETOF__Thread__m_pFrame);
            *ppFrame = pThisFrame->m_Next;
        } 
    }
#endif // WIN64 
 
    // Aggregates SafeHandle and the "owned" bit which indicates whether the SafeHandle
    // has been successfully AddRef'ed. This allows us to do realiable cleanup (Release) 
    // if and only if it is needed.
    [System.Security.SecurityCritical]
    internal sealed class CleanupWorkListElement
    { 
        public CleanupWorkListElement(SafeHandle handle)
        { 
            m_handle = handle; 
        }
 
        public SafeHandle m_handle;

        // This field is passed by-ref to SafeHandle.DangerousAddRef.
        // CleanupWorkList.Destroy ignores this element if m_owned is not set to true. 
        public bool m_owned;
    } 
 
    [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
    #if !FEATURE_CORECLR 
    [System.Runtime.ForceTokenStabilization]
    #endif //!FEATURE_CORECLR
    [System.Security.SecurityCritical]
    internal sealed class CleanupWorkList 
    {
        private List<CleanupWorkListElement> m_list = new List<CleanupWorkListElement>(); 
 
        public void Add(CleanupWorkListElement elem)
        { 
            BCLDebug.Assert(elem.m_owned == false, "m_owned is supposed to be false and set later by DangerousAddRef");
            m_list.Add(elem);
        }
 
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        public void Destroy() 
        { 
            for (int i = m_list.Count - 1; i >= 0; i--)
            { 
                if (m_list[i].m_owned)
                    StubHelpers.SafeHandleRelease(m_list[i].m_handle);
            }
        } 
    }
 
    [System.Security.SecurityCritical]  // auto-generated 
    [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
    [SuppressUnmanagedCodeSecurityAttribute()] 
    internal static class StubHelpers
    {
        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization] 
        #endif //!FEATURE_CORECLR
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        static internal extern bool IsQCall(IntPtr pMD); 

        #if !FEATURE_CORECLR 
        [System.Runtime.ForceTokenStabilization]
        #endif //!FEATURE_CORECLR
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        static internal extern void InitDeclaringType(IntPtr pMD); 

        #if !FEATURE_CORECLR 
        [System.Runtime.ForceTokenStabilization] 
        #endif //!FEATURE_CORECLR
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        static internal extern IntPtr GetNDirectTarget(IntPtr pMD);

        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization] 
        #endif //!FEATURE_CORECLR
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        static internal extern IntPtr GetDelegateTarget(Delegate pThis); 

#if WIN64 
#if _DEBUG
        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization]
        #endif //!FEATURE_CORECLR 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        static internal unsafe extern void Verify__EEFrame__Pop(EEFrame* pFrame, void* pThread, long managed_OFFSETOF__Thread__m_pFrame); 
 
        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization] 
        #endif //!FEATURE_CORECLR
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        static internal unsafe extern void Verify__EEFrame__Push(EEFrame* pFrame, void* pThread, long managed_OFFSETOF__Thread__m_pFrame);
#endif // _DEBUG 

        //-------------------------------------------------------- 
        // PInvoke stub helpers 
        //-------------------------------------------------------
#if _DEBUG 
        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization]
        #endif //!FEATURE_CORECLR
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        static internal extern UIntPtr GetProcessGSCookie();
 
        #if !FEATURE_CORECLR 
        [System.Runtime.ForceTokenStabilization]
        #endif //!FEATURE_CORECLR 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        static internal extern void FailFast();
#endif // _DEBUG
        #if !FEATURE_CORECLR 
        [System.Runtime.ForceTokenStabilization]
        #endif //!FEATURE_CORECLR 
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        static internal extern void DoNDirectCall();
 
#if FEATURE_COMINTEROP
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization] 
        #endif //!FEATURE_CORECLR
        static internal extern void DoCLRToCOMCall(object thisPtr); 
#endif // FEATURE_COMINTEROP 

        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization]
        #endif //!FEATURE_CORECLR
        static internal extern IntPtr BeginStandalone(IntPtr pFrame, IntPtr pNMD, int dwStubFlags); 

#if FEATURE_COMINTEROP 
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization] 
        #endif //!FEATURE_CORECLR
        static internal extern IntPtr BeginCLRToCOMStandalone(IntPtr pFrame, IntPtr pCPCMD, int dwStubFlags, object pThis);
#endif // FEATURE_COMINTEROP
#else // WIN64 
#if !FEATURE_CORECLR // CAS
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        [System.Runtime.ForceTokenStabilization] 
        static internal extern void DemandPermission(IntPtr pNMD);
 
        // Written to by a managed stub helper, read by CopyCtorCallStubWorker in VM.
        [ThreadStatic]
        static CopyCtorStubDesc s_copyCtorStubDesc;
 
        [System.Runtime.ForceTokenStabilization]
        static internal void SetCopyCtorCookieChain(IntPtr pStubArg, IntPtr pUnmngThis, int dwStubFlags, IntPtr pCookie) 
        { 
            // we store both the cookie chain head and the target of the copy ctor stub to a thread
            // static field to be accessed by the copy ctor (see code:CopyCtorCallStubWorker) 
            s_copyCtorStubDesc.m_pCookie = pCookie;
            s_copyCtorStubDesc.m_pTarget = GetFinalStubTarget(pStubArg, pUnmngThis, dwStubFlags);
        }
 
        // Returns the final unmanaged stub target, ignores interceptors.
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        [System.Runtime.ForceTokenStabilization] 
        static internal extern IntPtr GetFinalStubTarget(IntPtr pStubArg, IntPtr pUnmngThis, int dwStubFlags);
#endif // !FEATURE_CORECLR 
#endif // WIN64

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        #if !FEATURE_CORECLR 
        [System.Runtime.ForceTokenStabilization]
        #endif //!FEATURE_CORECLR 
        static internal extern void SetLastError(); 

        #if !FEATURE_CORECLR 
        [System.Runtime.ForceTokenStabilization]
        #endif //!FEATURE_CORECLR
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        static internal extern void ThrowDeferredException(); 

        #if !FEATURE_CORECLR 
        [System.Runtime.ForceTokenStabilization] 
        #endif //!FEATURE_CORECLR
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        static internal extern void ThrowInteropParamException(int resID, int paramIdx);

        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization] 
        #endif //!FEATURE_CORECLR
        [System.Security.SecurityCritical] 
        static internal IntPtr AddToCleanupList(ref CleanupWorkList pCleanupWorkList, SafeHandle handle) 
        {
            if (pCleanupWorkList == null) 
                pCleanupWorkList = new CleanupWorkList();

            CleanupWorkListElement element = new CleanupWorkListElement(handle);
            pCleanupWorkList.Add(element); 

            // element.m_owned will be true iff the AddRef succeeded 
            return SafeHandleAddRef(handle, ref element.m_owned); 
        }
 
        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization]
        #endif //!FEATURE_CORECLR
        [System.Security.SecurityCritical] 
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        static internal void DestroyCleanupList(ref CleanupWorkList pCleanupWorkList) 
        { 
            if (pCleanupWorkList != null)
            { 
                pCleanupWorkList.Destroy();
                pCleanupWorkList = null;
            }
        } 

        #if !FEATURE_CORECLR 
        [System.Runtime.ForceTokenStabilization] 
        #endif //!FEATURE_CORECLR
        static internal Exception GetHRExceptionObject(int hr) 
        {
            Exception ex = InternalGetHRExceptionObject(hr);
            ex.InternalPreserveStackTrace();
            return ex; 
        }
 
        #if !FEATURE_CORECLR 
        [System.Runtime.ForceTokenStabilization]
        #endif //!FEATURE_CORECLR 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        static internal extern Exception InternalGetHRExceptionObject(int hr);

#if FEATURE_COMINTEROP 

        // [ForceToken] SHOULD be under !FEATURE_CORECLR but putting this here wakes up yet another createBclSmall bug. 
        // Since this is under FEATURE_COMINTEROP, we can live without the if until we switch over to the rewriter. 
        [System.Runtime.ForceTokenStabilization]
        static internal Exception GetCOMHRExceptionObject(int hr, IntPtr pCPCMD, object pThis) 
        {
            Exception ex = InternalGetCOMHRExceptionObject(hr, pCPCMD, pThis);
            ex.InternalPreserveStackTrace();
            return ex; 
        }
 
 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        #if !FEATURE_CORECLR 
        [System.Runtime.ForceTokenStabilization]
        #endif //!FEATURE_CORECLR
        static internal extern Exception InternalGetCOMHRExceptionObject(int hr, IntPtr pCPCMD, object pThis);
 
#endif // FEATURE_COMINTEROP
 
        #if !FEATURE_CORECLR 
        [System.Runtime.ForceTokenStabilization]
        #endif //!FEATURE_CORECLR 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        static internal extern IntPtr CreateCustomMarshalerHelper(IntPtr pMD, int paramToken, IntPtr hndManagedType);

        //------------------------------------------------------- 
        // SafeHandle Helpers
        //------------------------------------------------------- 
 
        // AddRefs the SH and returns the underlying unmanaged handle.
        #if !FEATURE_CORECLR 
        [System.Runtime.ForceTokenStabilization]
        #endif //!FEATURE_CORECLR
        [System.Security.SecurityCritical]  // auto-generated
        static internal IntPtr SafeHandleAddRef(SafeHandle pHandle, ref bool success) 
        {
            if (pHandle == null) 
            { 
                throw new ArgumentNullException(Environment.GetResourceString("ArgumentNull_SafeHandle"));
            } 
            Contract.EndContractBlock();

            pHandle.DangerousAddRef(ref success);
 
            return (success ? pHandle.DangerousGetHandle() : IntPtr.Zero);
        } 
 
        // Releases the SH (to be called from finally block).
        #if !FEATURE_CORECLR 
        [System.Runtime.ForceTokenStabilization]
        #endif //!FEATURE_CORECLR
        [System.Security.SecurityCritical]  // auto-generated
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)] 
        static internal void SafeHandleRelease(SafeHandle pHandle)
        { 
            if (pHandle == null) 
            {
                throw new ArgumentNullException(Environment.GetResourceString("ArgumentNull_SafeHandle")); 
            }
            Contract.EndContractBlock();

            try 
            {
                pHandle.DangerousRelease(); 
            } 
#if MDA_SUPPORTED
            catch (Exception ex) 
            {
                Mda.ReportErrorSafeHandleRelease(ex);
            }
#else // MDA_SUPPORTED 
            catch (Exception)
            { } 
#endif // MDA_SUPPORTED 
        }
 
#if FEATURE_COMINTEROP
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization] 
        #endif //!FEATURE_CORECLR
        static internal extern IntPtr GetCLRToCOMTarget(IntPtr pUnk, IntPtr pCPCMD); 
 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        #if !FEATURE_CORECLR 
        [System.Runtime.ForceTokenStabilization]
        #endif //!FEATURE_CORECLR
        static internal extern IntPtr GetCOMIPFromRCW(object objSrc, IntPtr pCPCMD, out bool pfNeedsRelease);
 
        //--------------------------------------------------------
        // Helper for the MDA ----OnRCWCleanup 
        //------------------------------------------------------- 

        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization]
        #endif //!FEATURE_CORECLR
        static internal extern void StubRegisterRCW(object pThis, IntPtr pThread); 

        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        #if !FEATURE_CORECLR 
        [System.Runtime.ForceTokenStabilization]
        #endif //!FEATURE_CORECLR 
        static internal extern void StubUnregisterRCW(object pThis, IntPtr pThread);
#endif // FEATURE_COMINTEROP

#if MDA_SUPPORTED 
        [System.Runtime.ForceTokenStabilization]
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        static internal extern void CheckCollectedDelegateMDA(IntPtr pEntryThunk); 
#endif // MDA_SUPPORTED
 
        //--------------------------------------------------------
        // Profiler helpers
        //--------------------------------------------------------
#if PROFILING_SUPPORTED 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        #if !FEATURE_CORECLR 
        [System.Runtime.ForceTokenStabilization] 
        #endif //!FEATURE_CORECLR
        static internal extern IntPtr ProfilerBeginTransitionCallback(IntPtr pSecretParam, IntPtr pThread, object pThis); 

        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization]
        #endif //!FEATURE_CORECLR 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        static internal extern void ProfilerEndTransitionCallback(IntPtr pMD, IntPtr pThread); 
#endif // PROFILING_SUPPORTED 
        //-------------------------------------------------------
        // Debugger helpers 
        //--------------------------------------------------------

        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization] 
        #endif //!FEATURE_CORECLR
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        static internal extern void DebuggerTraceCall(IntPtr pSecretParam); 

        //----------------------------------------------------- 
        // misc
        //-----------------------------------------------------
        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization] 
        [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
        #endif //!FEATURE_CORECLR 
        static internal void CheckStringLength(int length) 
        {
            CheckStringLength((uint)length); 
        }

        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization] 
        [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
        #endif //!FEATURE_CORECLR 
        static internal void CheckStringLength(uint length) 
        {
            if (length > 0x7ffffff0) 
            {
                throw new MarshalDirectiveException(Environment.GetResourceString("Marshaler_StringTooLong"));
            }
        } 

        #if !FEATURE_CORECLR 
        [System.Runtime.ForceTokenStabilization] 
        #endif //!FEATURE_CORECLR
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        static internal unsafe extern int strlen(sbyte* ptr);

        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization] 
        #endif //!FEATURE_CORECLR
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        static internal extern void DecimalCanonicalizeInternal(ref Decimal dec); 

        #if !FEATURE_CORECLR 
        [System.Runtime.ForceTokenStabilization]
        #endif //!FEATURE_CORECLR
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        static internal unsafe extern void FmtClassUpdateNativeInternal(object obj, byte* pNative, ref CleanupWorkList pCleanupWorkList); 
        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization] 
        #endif //!FEATURE_CORECLR 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        static internal unsafe extern void FmtClassUpdateCLRInternal(object obj, byte* pNative); 
        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization]
        #endif //!FEATURE_CORECLR
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        static internal unsafe extern void LayoutDestroyNativeInternal(byte* pNative, IntPtr pMT);
        #if !FEATURE_CORECLR 
        [System.Runtime.ForceTokenStabilization] 
        #endif //!FEATURE_CORECLR
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        static internal extern object AllocateInternal(IntPtr typeHandle);

        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization] 
        #endif //!FEATURE_CORECLR
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        static internal extern void MarshalToUnmanagedVaListInternal(IntPtr va_list, uint vaListSize, IntPtr pArgIterator); 

        #if !FEATURE_CORECLR 
        [System.Runtime.ForceTokenStabilization]
        #endif //!FEATURE_CORECLR
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        static internal extern void MarshalToManagedVaListInternal(IntPtr va_list, IntPtr pArgIterator); 

        #if !FEATURE_CORECLR 
        [System.Runtime.ForceTokenStabilization] 
        #endif //!FEATURE_CORECLR
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        static internal extern uint CalcVaListSize(IntPtr va_list);

        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization] 
        #endif //!FEATURE_CORECLR
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        static internal extern void ValidateObject(object obj, IntPtr pMD, object pThis); 

        #if !FEATURE_CORECLR 
        [System.Runtime.ForceTokenStabilization]
        #endif //!FEATURE_CORECLR
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        static internal extern void ValidateByref(IntPtr byref, IntPtr pMD, object pThis); // the byref is pinned so we can safely "cast" it to IntPtr 

        #if !FEATURE_CORECLR 
        [System.Runtime.ForceTokenStabilization] 
        #endif //!FEATURE_CORECLR
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        static internal extern IntPtr GetStubContext();

#if MDA_SUPPORTED
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization] 
        #endif //!FEATURE_CORECLR 
        static internal extern void TriggerGCForMDA();
#endif // MDA_SUPPORTED 
    }
}

