// ==++== 
//
//   Copyright (c) Microsoft Corporation.  All rights reserved.
//
// ==--== 
/*==============================================================================
** 
** Class: Marshal 
**
** 
** Purpose: This class contains methods that are mainly used to marshal
**          between unmanaged and managed types.
**
** 
=============================================================================*/
 
namespace System.Runtime.InteropServices 
{
    using System; 
    using System.Collections.Generic;
    using System.Reflection;
    using System.Reflection.Emit;
    using System.Security; 
    using System.Security.Permissions;
    using System.Text; 
    using System.Threading; 
    using System.Runtime.Remoting;
    using System.Runtime.CompilerServices; 
    using System.Globalization;
    using System.Runtime.ConstrainedExecution;
    using System.Runtime.Versioning;
    using Win32Native = Microsoft.Win32.Win32Native; 
    using Microsoft.Win32.SafeHandles;
    using System.Diagnostics.Contracts; 
#if FEATURE_COMINTEROP 
    using System.Runtime.InteropServices.ComTypes;
#endif 

    [Serializable]
    public enum CustomQueryInterfaceMode
    { 
        Ignore  = 0,
        Allow = 1 
    } 

    //======================================================================= 
    // All public methods, including PInvoke, are protected with linkchecks.
    // Remove the default demands for all PInvoke methods with this global
    // declaration on the class.
    //======================================================================= 

    public static class Marshal 
    { 
        //===================================================================
        // Defines used inside the Marshal class. 
        //====================================================================
        private const int LMEM_FIXED = 0;
#if !FEATURE_PAL
        private const int LMEM_MOVEABLE = 2; 
        private static readonly IntPtr HIWORDMASK = unchecked(new IntPtr((long)0xffffffffffff0000L));
#endif //!FEATURE_PAL 
#if FEATURE_COMINTEROP 
        private static Guid IID_IUnknown = new Guid("00000000-0000-0000-C000-000000000046");
#endif //FEATURE_COMINTEROP 

        [System.Security.SecuritySafeCritical] // static constructors should be safe to call
        static Marshal()
        { 
        }
 
        // Win32 has the concept of Atoms, where a pointer can either be a pointer 
        // or an int.  If it's less than 64K, this is guaranteed to NOT be a
        // pointer since the bottom 64K bytes are reserved in a process' page table. 
        // We should be careful about deallocating this stuff.  Extracted to
        // a function to avoid C# problems with lack of support for IntPtr.
        // We have 2 of these methods for slightly different semantics for NULL.
        private static bool IsWin32Atom(IntPtr ptr) 
        {
#if FEATURE_PAL 
        return false; 
#else
            long lPtr = (long)ptr; 
            return 0 == (lPtr & (long)HIWORDMASK);
#endif
        }
 
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        private static bool IsNotWin32Atom(IntPtr ptr) 
        { 
#if FEATURE_PAL
    return false; 
#else
            long lPtr = (long)ptr;
            return 0 != (lPtr & (long)HIWORDMASK);
#endif 
        }
 
        //=================================================================== 
        // The default character size for the system. This is always 2 because
        // the framework only runs on UTF-16 systems. 
        //====================================================================
        public static readonly int SystemDefaultCharSize = 2;

        //==================================================================== 
        // The max DBCS character size for the system.
        //=================================================================== 
        public static readonly int SystemMaxDBCSCharSize = GetSystemMaxDBCSCharSize(); 

 
        //====================================================================
        // The name, title and description of the assembly that will contain
        // the dynamically generated interop types.
        //=================================================================== 
        private const String s_strConvertedTypeInfoAssemblyName   = "InteropDynamicTypes";
        private const String s_strConvertedTypeInfoAssemblyTitle  = "Interop Dynamic Types"; 
        private const String s_strConvertedTypeInfoAssemblyDesc   = "Type dynamically generated from ITypeInfo's"; 
        private const String s_strConvertedTypeInfoNameSpace      = "InteropDynamicTypes";
 

        //===================================================================
        // Helper method to retrieve the system's maximum DBCS character size.
        //=================================================================== 
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        private static extern int GetSystemMaxDBCSCharSize(); 

        [System.Security.SecurityCritical]  // auto-generated_required 
        public static String PtrToStringAnsi(IntPtr ptr)
        {
            if (Win32Native.NULL == ptr) {
                return null; 
            }
            else if (IsWin32Atom(ptr)) { 
                return null; 
            }
            else { 
                int nb = Win32Native.lstrlenA(ptr);
                if( nb == 0) {
                    return string.Empty;
                } 
                else {
                    StringBuilder sb = new StringBuilder(nb); 
                    Win32Native.CopyMemoryAnsi(sb, ptr, new IntPtr(1+nb)); 
                    return sb.ToString();
                } 
            }
        }

        [System.Security.SecurityCritical]  // auto-generated_required 
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        public static extern String PtrToStringAnsi(IntPtr ptr, int len); 

        [System.Security.SecurityCritical]  // auto-generated_required 
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        public static extern String PtrToStringUni(IntPtr ptr, int len);
 
        [System.Security.SecurityCritical]  // auto-generated_required
        public static String PtrToStringAuto(IntPtr ptr, int len) 
        { 
            // Ansi platforms are no longer supported
            return PtrToStringUni(ptr, len); 
        }

        [System.Security.SecurityCritical]  // auto-generated_required
        public static String PtrToStringUni(IntPtr ptr) 
        {
            if (Win32Native.NULL == ptr) { 
                return null; 
            }
            else if (IsWin32Atom(ptr)) { 
                return null;
            }
            else {
                int nc = Win32Native.lstrlenW(ptr); 
                StringBuilder sb = new StringBuilder(nc);
                Win32Native.CopyMemoryUni(sb, ptr, new IntPtr(2*(1+nc))); 
                return sb.ToString(); 
            }
        } 

        [System.Security.SecurityCritical]  // auto-generated_required
        public static String PtrToStringAuto(IntPtr ptr)
        { 
            // Ansi platforms are no longer supported
            return PtrToStringUni(ptr); 
        } 

        //==================================================================== 
        // SizeOf()
        //===================================================================
        [ResourceExposure(ResourceScope.None)]
        [System.Runtime.InteropServices.ComVisible(true)] 
        public static int SizeOf(Object structure)
        { 
            if (structure == null) 
                throw new ArgumentNullException("structure");
            // we never had a check for generics here 
            Contract.EndContractBlock();

            return SizeOfHelper(structure.GetType(), true);
        } 

        [ResourceExposure(ResourceScope.None)] 
        public static int SizeOf(Type t) 
        {
            if (t == null) 
                throw new ArgumentNullException("t");
            if (!(t is RuntimeType))
                throw new ArgumentException(Environment.GetResourceString("Argument_MustBeRuntimeType"), "t");
            if (t.IsGenericType) 
                throw new ArgumentException(Environment.GetResourceString("Argument_NeedNonGenericType"), "t");
            Contract.EndContractBlock(); 
 
            return SizeOfHelper(t, true);
        } 

#if !FEATURE_CORECLR
        /// <summary>
        /// Returns the size of an instance of a value type. 
        /// </summary>
        /// <typeparam name="T">Provide a value type to figure out its size</typeparam> 
        /// <returns>The size of T in bytes.</returns> 
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        internal static uint SizeOf<T>() where T : struct 
        {
            return SizeOfType(typeof(T));
        }
 
        /// <summary>
        /// Returns the aligned size of an instance of a value type. 
        /// </summary> 
        /// <typeparam name="T">Provide a value type to figure out its size</typeparam>
        /// <returns>The aligned size of T in bytes.</returns> 
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        internal static uint AlignedSizeOf<T>() where T : struct
        {
            uint size = SizeOf<T>(); 
            if (size == 1 || size == 2)
            { 
                return size; 
            }
            if (IntPtr.Size == 8 && size == 4) 
            {
                return size;
            }
            return AlignedSizeOfType(typeof(T)); 
        }
 
        // Type must be a value type with no object reference fields.  We only 
        // assert this, due to the lack of a suitable generic constraint.
        [MethodImpl(MethodImplOptions.InternalCall)] 
        [ResourceExposure(ResourceScope.None)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        private static extern uint SizeOfType(Type type);
 
        // Type must be a value type with no object reference fields.  We only
        // assert this, due to the lack of a suitable generic constraint. 
        [MethodImpl(MethodImplOptions.InternalCall)] 
        [ResourceExposure(ResourceScope.None)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)] 
        private static extern uint AlignedSizeOfType(Type type);
#endif // !FEATURE_CORECLR

#if !FEATURE_CORECLR // Marshal is critical in CoreCLR, so SafeCritical members trigger Annotator violations 
        [System.Security.SecuritySafeCritical]
#endif // !FEATURE_CORECLR 
        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal static extern int SizeOfHelper(Type t, bool throwIfNotMarshalable); 

        //====================================================================
        // OffsetOf()
        //==================================================================== 
        [System.Security.SecuritySafeCritical]
        public static IntPtr OffsetOf(Type t, String fieldName) 
        { 
            if (t == null)
                throw new ArgumentNullException("t"); 
            Contract.EndContractBlock();

            FieldInfo f = t.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f == null) 
                throw new ArgumentException(Environment.GetResourceString("Argument_OffsetOfFieldNotFound", t.FullName), "fieldName");
            else if (!(f is RuntimeFieldInfo)) 
                throw new ArgumentException(Environment.GetResourceString("Argument_MustBeRuntimeFieldInfo"), "fieldName"); 

            return OffsetOfHelper(((RuntimeFieldInfo)f).FieldHandle.GetRuntimeFieldInfo()); 
        }
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        private static extern IntPtr OffsetOfHelper(IRuntimeFieldInfo f); 

        //=================================================================== 
        // UnsafeAddrOfPinnedArrayElement() 
        //
        // IMPORTANT NOTICE: This method does not do any verification on the 
        // array. It must be used with EXTREME CAUTION since passing in
        // an array that is not pinned or in the fixed heap can cause
        // unexpected results !
        //==================================================================== 
        [System.Security.SecurityCritical]  // auto-generated_required
        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        public static extern IntPtr UnsafeAddrOfPinnedArrayElement(Array arr, int index);
 

        //===================================================================
        // Copy blocks from CLR arrays to native memory.
        //=================================================================== 
        [System.Security.SecurityCritical]  // auto-generated_required
        public static void Copy(int[]     source, int startIndex, IntPtr destination, int length) 
        { 
            CopyToNative(source, startIndex, destination, length);
        } 
        [System.Security.SecurityCritical]  // auto-generated_required
        public static void Copy(char[]    source, int startIndex, IntPtr destination, int length)
        {
            CopyToNative(source, startIndex, destination, length); 
        }
        [System.Security.SecurityCritical]  // auto-generated_required 
        public static void Copy(short[]   source, int startIndex, IntPtr destination, int length) 
        {
            CopyToNative(source, startIndex, destination, length); 
        }
        [System.Security.SecurityCritical]  // auto-generated_required
        public static void Copy(long[]    source, int startIndex, IntPtr destination, int length)
        { 
            CopyToNative(source, startIndex, destination, length);
        } 
        [System.Security.SecurityCritical]  // auto-generated_required 
        public static void Copy(float[]   source, int startIndex, IntPtr destination, int length)
        { 
            CopyToNative(source, startIndex, destination, length);
        }
        [System.Security.SecurityCritical]  // auto-generated_required
        public static void Copy(double[]  source, int startIndex, IntPtr destination, int length) 
        {
            CopyToNative(source, startIndex, destination, length); 
        } 
        [System.Security.SecurityCritical]  // auto-generated_required
        public static void Copy(byte[] source, int startIndex, IntPtr destination, int length) 
        {
            CopyToNative(source, startIndex, destination, length);
        }
        [System.Security.SecurityCritical]  // auto-generated_required 
        public static void Copy(IntPtr[] source, int startIndex, IntPtr destination, int length)
        { 
            CopyToNative(source, startIndex, destination, length); 
        }
        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        private static extern void CopyToNative(Object source, int startIndex, IntPtr destination, int length);

        //=================================================================== 
        // Copy blocks from native memory to CLR arrays
        //==================================================================== 
        [System.Security.SecurityCritical]  // auto-generated_required 
        public static void Copy(IntPtr source, int[]     destination, int startIndex, int length)
        { 
            CopyToManaged(source, destination, startIndex, length);
        }
        [System.Security.SecurityCritical]  // auto-generated_required
        public static void Copy(IntPtr source, char[]    destination, int startIndex, int length) 
        {
            CopyToManaged(source, destination, startIndex, length); 
        } 
        [System.Security.SecurityCritical]  // auto-generated_required
        public static void Copy(IntPtr source, short[]   destination, int startIndex, int length) 
        {
            CopyToManaged(source, destination, startIndex, length);
        }
        [System.Security.SecurityCritical]  // auto-generated_required 
        public static void Copy(IntPtr source, long[]    destination, int startIndex, int length)
        { 
            CopyToManaged(source, destination, startIndex, length); 
        }
        [System.Security.SecurityCritical]  // auto-generated_required 
        public static void Copy(IntPtr source, float[]   destination, int startIndex, int length)
        {
            CopyToManaged(source, destination, startIndex, length);
        } 
        [System.Security.SecurityCritical]  // auto-generated_required
        public static void Copy(IntPtr source, double[]  destination, int startIndex, int length) 
        { 
            CopyToManaged(source, destination, startIndex, length);
        } 
        [System.Security.SecurityCritical]  // auto-generated_required
        public static void Copy(IntPtr source, byte[] destination, int startIndex, int length)
        {
            CopyToManaged(source, destination, startIndex, length); 
        }
        [System.Security.SecurityCritical]  // auto-generated_required 
        public static void Copy(IntPtr source, IntPtr[] destination, int startIndex, int length) 
        {
            CopyToManaged(source, destination, startIndex, length); 
        }
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        private static extern void CopyToManaged(IntPtr source, Object destination, int startIndex, int length); 

        //=================================================================== 
        // Read from memory 
        //====================================================================
        [DllImport(Win32Native.SHIM, EntryPoint="ND_RU1")] 
        [ResourceExposure(ResourceScope.None)]
        [SuppressUnmanagedCodeSecurity]
        [System.Security.SecurityCritical]  // auto-generated
        public static extern byte ReadByte([MarshalAs(UnmanagedType.AsAny), In] Object ptr, int ofs); 

        [System.Security.SecurityCritical]  // auto-generated_required 
        [ResourceExposure(ResourceScope.None)] 
        public static unsafe byte ReadByte(IntPtr ptr, int ofs)
        { 
            try
            {
                byte *addr = (byte *)ptr + ofs;
                return *addr; 
            }
            catch (NullReferenceException) 
            { 
                // this method is documented to throw AccessViolationException on any AV
                throw new AccessViolationException(); 
            }
        }

        [System.Security.SecurityCritical]  // auto-generated_required 
        public static byte ReadByte(IntPtr ptr)
        { 
            return ReadByte(ptr,0); 
        }
 
        [DllImport(Win32Native.SHIM, EntryPoint="ND_RI2")]
        [ResourceExposure(ResourceScope.None)]
        [SuppressUnmanagedCodeSecurity]
        [System.Security.SecurityCritical]  // auto-generated 
        public static extern short ReadInt16([MarshalAs(UnmanagedType.AsAny),In] Object ptr, int ofs);
 
        [System.Security.SecurityCritical]  // auto-generated_required 
        [ResourceExposure(ResourceScope.None)]
        public static unsafe short ReadInt16(IntPtr ptr, int ofs) 
        {
            try
            {
                byte *addr = (byte *)ptr + ofs; 
                if ((unchecked((int)addr) & 0x1) == 0)
                { 
                    // aligned read 
                    return *((short *)addr);
                } 
                else
                {
                    // unaligned read
                    short val; 
                    byte *valPtr = (byte *)&val;
                    valPtr[0] = addr[0]; 
                    valPtr[1] = addr[1]; 
                    return val;
                } 
            }
            catch (NullReferenceException)
            {
                // this method is documented to throw AccessViolationException on any AV 
                throw new AccessViolationException();
            } 
        } 

        [System.Security.SecurityCritical]  // auto-generated_required 
        public static short ReadInt16(IntPtr ptr)
        {
            return ReadInt16(ptr, 0);
        } 

        [DllImport(Win32Native.SHIM, EntryPoint="ND_RI4"), ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)] 
        [ResourceExposure(ResourceScope.None)] 
        [SuppressUnmanagedCodeSecurity]
        [System.Security.SecurityCritical]  // auto-generated 
        public static extern int ReadInt32([MarshalAs(UnmanagedType.AsAny),In] Object ptr, int ofs);

        [System.Security.SecurityCritical]  // auto-generated_required
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)] 
        [ResourceExposure(ResourceScope.None)]
        public static unsafe int ReadInt32(IntPtr ptr, int ofs) 
        { 
            try
            { 
                byte *addr = (byte *)ptr + ofs;
                if ((unchecked((int)addr) & 0x3) == 0)
                {
                    // aligned read 
                    return *((int *)addr);
                } 
                else 
                {
                    // unaligned read 
                    int val;
                    byte *valPtr = (byte *)&val;
                    valPtr[0] = addr[0];
                    valPtr[1] = addr[1]; 
                    valPtr[2] = addr[2];
                    valPtr[3] = addr[3]; 
                    return val; 
                }
            } 
            catch (NullReferenceException)
            {
                // this method is documented to throw AccessViolationException on any AV
                throw new AccessViolationException(); 
            }
        } 
 
        [System.Security.SecurityCritical]  // auto-generated_required
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)] 
        public static int ReadInt32(IntPtr ptr)
        {
            return ReadInt32(ptr,0);
        } 

        [System.Security.SecurityCritical]  // auto-generated_required 
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)] 
        public static IntPtr ReadIntPtr([MarshalAs(UnmanagedType.AsAny),In] Object ptr, int ofs)
        { 
            #if WIN32
                return (IntPtr) ReadInt32(ptr, ofs);
            #else
                return (IntPtr) ReadInt64(ptr, ofs); 
            #endif
        } 
 
        [System.Security.SecurityCritical]  // auto-generated_required
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)] 
        public static IntPtr ReadIntPtr(IntPtr ptr, int ofs)
        {
            #if WIN32
                return (IntPtr) ReadInt32(ptr, ofs); 
            #else
                return (IntPtr) ReadInt64(ptr, ofs); 
            #endif 
        }
 
        [System.Security.SecurityCritical]  // auto-generated_required
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        public static IntPtr ReadIntPtr(IntPtr ptr)
        { 
            #if WIN32
                return (IntPtr) ReadInt32(ptr, 0); 
            #else 
                return (IntPtr) ReadInt64(ptr, 0);
            #endif 
        }

        [DllImport(Win32Native.SHIM, EntryPoint="ND_RI8"), ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        [ResourceExposure(ResourceScope.None)] 
        [SuppressUnmanagedCodeSecurity]
        [System.Security.SecurityCritical]  // auto-generated 
        public static extern long ReadInt64([MarshalAs(UnmanagedType.AsAny),In] Object ptr, int ofs); 

        [System.Security.SecurityCritical]  // auto-generated_required 
        [ResourceExposure(ResourceScope.None)]
        public static unsafe long ReadInt64(IntPtr ptr, int ofs)
        {
            try 
            {
                byte *addr = (byte *)ptr + ofs; 
                if ((unchecked((int)addr) & 0x7) == 0) 
                {
                    // aligned read 
                    return *((long *)addr);
                }
                else
                { 
                    // unaligned read
                    long val; 
                    byte *valPtr = (byte *)&val; 
                    valPtr[0] = addr[0];
                    valPtr[1] = addr[1]; 
                    valPtr[2] = addr[2];
                    valPtr[3] = addr[3];
                    valPtr[4] = addr[4];
                    valPtr[5] = addr[5]; 
                    valPtr[6] = addr[6];
                    valPtr[7] = addr[7]; 
                    return val; 
                }
            } 
            catch (NullReferenceException)
            {
                // this method is documented to throw AccessViolationException on any AV
                throw new AccessViolationException(); 
            }
        } 
 
        [System.Security.SecurityCritical]  // auto-generated_required
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)] 
        public static long ReadInt64(IntPtr ptr)
        {
            return ReadInt64(ptr,0);
        } 

 
        //==================================================================== 
        // Write to memory
        //=================================================================== 
        [System.Security.SecurityCritical]  // auto-generated_required
        [ResourceExposure(ResourceScope.None)]
        public static unsafe void WriteByte(IntPtr ptr, int ofs, byte val)
        { 
            try
            { 
                byte *addr = (byte *)ptr + ofs; 
                *addr = val;
            } 
            catch (NullReferenceException)
            {
                // this method is documented to throw AccessViolationException on any AV
                throw new AccessViolationException(); 
            }
        } 
 
        [DllImport(Win32Native.SHIM, EntryPoint="ND_WU1")]
        [ResourceExposure(ResourceScope.None)] 
        [SuppressUnmanagedCodeSecurity]
        [System.Security.SecurityCritical]  // auto-generated
        public static extern void WriteByte([MarshalAs(UnmanagedType.AsAny),In,Out] Object ptr, int ofs, byte val);
 
        [System.Security.SecurityCritical]  // auto-generated_required
        public static void WriteByte(IntPtr ptr, byte val) 
        { 
            WriteByte(ptr, 0, val);
        } 

        [System.Security.SecurityCritical]  // auto-generated_required
        [ResourceExposure(ResourceScope.None)]
        public static unsafe void WriteInt16(IntPtr ptr, int ofs, short val) 
        {
            try 
            { 
                byte *addr = (byte *)ptr + ofs;
                if ((unchecked((int)addr) & 0x1) == 0) 
                {
                    // aligned write
                    *((short *)addr) = val;
                } 
                else
                { 
                    // unaligned write 
                    byte *valPtr = (byte *)&val;
                    addr[0] = valPtr[0]; 
                    addr[1] = valPtr[1];
                }
            }
            catch (NullReferenceException) 
            {
                // this method is documented to throw AccessViolationException on any AV 
                throw new AccessViolationException(); 
            }
        } 

        [DllImport(Win32Native.SHIM, EntryPoint="ND_WI2")]
        [ResourceExposure(ResourceScope.None)]
        [SuppressUnmanagedCodeSecurity] 
        [System.Security.SecurityCritical]  // auto-generated
        public static extern void WriteInt16([MarshalAs(UnmanagedType.AsAny),In,Out] Object ptr, int ofs, short val); 
 
        [System.Security.SecurityCritical]  // auto-generated_required
        public static void WriteInt16(IntPtr ptr, short val) 
        {
            WriteInt16(ptr, 0, val);
        }
 
        [System.Security.SecurityCritical]  // auto-generated_required
        public static void WriteInt16(IntPtr ptr, int ofs, char val) 
        { 
            WriteInt16(ptr, ofs, (short)val);
        } 

        [System.Security.SecurityCritical]  // auto-generated_required
        public static void WriteInt16([In,Out]Object ptr, int ofs, char val)
        { 
            WriteInt16(ptr, ofs, (short)val);
        } 
 
        [System.Security.SecurityCritical]  // auto-generated_required
        public static void WriteInt16(IntPtr ptr, char val) 
        {
            WriteInt16(ptr, 0, (short)val);
        }
 
        [System.Security.SecurityCritical]  // auto-generated_required
        [ResourceExposure(ResourceScope.None)] 
        public static unsafe void WriteInt32(IntPtr ptr, int ofs, int val) 
        {
            try 
            {
                byte *addr = (byte *)ptr + ofs;
                if ((unchecked((int)addr) & 0x3) == 0)
                { 
                    // aligned write
                    *((int *)addr) = val; 
                } 
                else
                { 
                    // unaligned write
                    byte *valPtr = (byte *)&val;
                    addr[0] = valPtr[0];
                    addr[1] = valPtr[1]; 
                    addr[2] = valPtr[2];
                    addr[3] = valPtr[3]; 
                } 
            }
            catch (NullReferenceException) 
            {
                // this method is documented to throw AccessViolationException on any AV
                throw new AccessViolationException();
            } 
        }
 
        [DllImport(Win32Native.SHIM, EntryPoint="ND_WI4")] 
        [ResourceExposure(ResourceScope.None)]
        [SuppressUnmanagedCodeSecurity] 
        [System.Security.SecurityCritical]  // auto-generated
        public static extern void WriteInt32([MarshalAs(UnmanagedType.AsAny),In,Out] Object ptr, int ofs, int val);

        [System.Security.SecurityCritical]  // auto-generated_required 
        public static void WriteInt32(IntPtr ptr, int val)
        { 
            WriteInt32(ptr,0,val); 
        }
 
        [System.Security.SecurityCritical]  // auto-generated_required
        public static void WriteIntPtr(IntPtr ptr, int ofs, IntPtr val)
        {
            #if WIN32 
                WriteInt32(ptr, ofs, (int)val);
            #else 
                WriteInt64(ptr, ofs, (long)val); 
            #endif
        } 

        [System.Security.SecurityCritical]  // auto-generated_required
        public static void WriteIntPtr([MarshalAs(UnmanagedType.AsAny),In,Out] Object ptr, int ofs, IntPtr val)
        { 
            #if WIN32
                WriteInt32(ptr, ofs, (int)val); 
            #else 
                WriteInt64(ptr, ofs, (long)val);
            #endif 
        }

        [System.Security.SecurityCritical]  // auto-generated_required
        public static void WriteIntPtr(IntPtr ptr, IntPtr val) 
        {
            #if WIN32 
                WriteInt32(ptr, 0, (int)val); 
            #else
                WriteInt64(ptr, 0, (long)val); 
            #endif
        }

        [System.Security.SecurityCritical]  // auto-generated_required 
        [ResourceExposure(ResourceScope.None)]
        public static unsafe void WriteInt64(IntPtr ptr, int ofs, long val) 
        { 
            try
            { 
                byte *addr = (byte *)ptr + ofs;
                if ((unchecked((int)addr) & 0x7) == 0)
                {
                    // aligned write 
                    *((long *)addr) = val;
                } 
                else 
                {
                    // unaligned write 
                    byte *valPtr = (byte *)&val;
                    addr[0] = valPtr[0];
                    addr[1] = valPtr[1];
                    addr[2] = valPtr[2]; 
                    addr[3] = valPtr[3];
                    addr[4] = valPtr[4]; 
                    addr[5] = valPtr[5]; 
                    addr[6] = valPtr[6];
                    addr[7] = valPtr[7]; 
                }
            }
            catch (NullReferenceException)
            { 
                // this method is documented to throw AccessViolationException on any AV
                throw new AccessViolationException(); 
            } 
        }
 
        [DllImport(Win32Native.SHIM, EntryPoint="ND_WI8")]
        [ResourceExposure(ResourceScope.None)]
        [SuppressUnmanagedCodeSecurity]
        [System.Security.SecurityCritical]  // auto-generated 
        public static extern void WriteInt64([MarshalAs(UnmanagedType.AsAny),In,Out] Object ptr, int ofs, long val);
 
        [System.Security.SecurityCritical]  // auto-generated_required 
        public static void WriteInt64(IntPtr ptr, long val)
        { 
            WriteInt64(ptr, 0, val);
        }

 
        //====================================================================
        // GetLastWin32Error 
        //=================================================================== 
        [System.Security.SecurityCritical]  // auto-generated_required
        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        public static extern int GetLastWin32Error();
 

        //=================================================================== 
        // SetLastWin32Error 
        //===================================================================
        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        internal static extern void SetLastWin32Error(int error);
 

        //==================================================================== 
        // GetHRForLastWin32Error 
        //===================================================================
        [System.Security.SecurityCritical]  // auto-generated_required 
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        public static int GetHRForLastWin32Error()
        {
            int dwLastError = GetLastWin32Error(); 
            if ((dwLastError & 0x80000000) == 0x80000000)
                return dwLastError; 
            else 
                return (dwLastError & 0x0000FFFF) | unchecked((int)0x80070000);
        } 


        //====================================================================
        // Prelink 
        //====================================================================
        [System.Security.SecurityCritical]  // auto-generated_required 
        public static void Prelink(MethodInfo m) 
        {
            if (m == null) 
                throw new ArgumentNullException("m");
            Contract.EndContractBlock();

            RuntimeMethodInfo rmi = m as RuntimeMethodInfo; 

            if (rmi == null) 
                throw new ArgumentException(Environment.GetResourceString("Argument_MustBeRuntimeMethodInfo")); 

            InternalPrelink(rmi); 
        }

        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        private static extern void InternalPrelink(IRuntimeMethodInfo m);
 
        [System.Security.SecurityCritical]  // auto-generated_required 
        public static void PrelinkAll(Type c)
        { 
            if (c == null)
                throw new ArgumentNullException("c");
            Contract.EndContractBlock();
 
            MethodInfo[] mi = c.GetMethods();
            if (mi != null) 
            { 
                for (int i = 0; i < mi.Length; i++)
                { 
                    Prelink(mi[i]);
                }
            }
        } 

        //=================================================================== 
        // NumParamBytes 
        //====================================================================
        [System.Security.SecurityCritical]  // auto-generated_required 
        public static int NumParamBytes(MethodInfo m)
        {
            if (m == null)
                throw new ArgumentNullException("m"); 
            Contract.EndContractBlock();
 
            RuntimeMethodInfo rmi = m as RuntimeMethodInfo; 
            if (rmi == null)
                throw new ArgumentException(Environment.GetResourceString("Argument_MustBeRuntimeMethodInfo")); 

            return InternalNumParamBytes(rmi);
        }
 
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        private static extern int InternalNumParamBytes(IRuntimeMethodInfo m); 

        //=================================================================== 
        // Win32 Exception stuff
        // These are mostly interesting for Structured exception handling,
        // but need to be exposed for all exceptions (not just SEHException).
        //=================================================================== 
        [System.Security.SecurityCritical]  // auto-generated_required
        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
[System.Runtime.InteropServices.ComVisible(true)]
        public static extern /* struct _EXCEPTION_POINTERS* */ IntPtr GetExceptionPointers(); 

        [System.Security.SecurityCritical]  // auto-generated_required
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        public static extern int GetExceptionCode();
 
 
        //===================================================================
        // Marshals data from a structure class to a native memory block. 
        // If the structure contains pointers to allocated blocks and
        // "fDeleteOld" is true, this routine will call DestroyStructure() first.
        //====================================================================
        [System.Security.SecurityCritical]  // auto-generated_required 
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall), ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)] 
[System.Runtime.InteropServices.ComVisible(true)] 
        public static extern void StructureToPtr(Object structure, IntPtr ptr, bool fDeleteOld);
 

        //===================================================================
        // Marshals data from a native memory block to a preallocated structure class.
        //==================================================================== 
        [System.Security.SecurityCritical]  // auto-generated_required
[System.Runtime.InteropServices.ComVisible(true)] 
        public static void PtrToStructure(IntPtr ptr, Object structure) 
        {
            PtrToStructureHelper(ptr, structure, false); 
        }


        //==================================================================== 
        // Creates a new instance of "structuretype" and marshals data from a
        // native memory block to it. 
        //=================================================================== 
        [System.Security.SecurityCritical]  // auto-generated_required
        [System.Runtime.InteropServices.ComVisible(true)] 
        public static Object PtrToStructure(IntPtr ptr, Type structureType)
        {
            if (ptr == IntPtr.Zero) return null;
 
            if (structureType == null)
                throw new ArgumentNullException("structureType"); 
 
            if (structureType.IsGenericType)
                throw new ArgumentException(Environment.GetResourceString("Argument_NeedNonGenericType"), "structureType"); 

            RuntimeType rt = structureType.UnderlyingSystemType as RuntimeType;

            if (rt == null) 
                throw new ArgumentException(Environment.GetResourceString("Arg_MustBeType"), "type");
 
            Object structure = rt.CreateInstanceDefaultCtor(false /*publicOnly*/, false /*skipVisibilityChecks*/, false /*skipCheckThis*/, false /*fillCache*/); 
            PtrToStructureHelper(ptr, structure, true);
            return structure; 
        }


        //==================================================================== 
        // Helper function to copy a pointer into a preallocated structure.
        //=================================================================== 
        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        private static extern void PtrToStructureHelper(IntPtr ptr, Object structure, bool allowValueClasses); 


        //===================================================================
        // Freeds all substructures pointed to by the native memory block. 
        // "structureclass" is used to provide layout information.
        //=================================================================== 
        [System.Security.SecurityCritical]  // auto-generated_required 
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
[System.Runtime.InteropServices.ComVisible(true)]
        public static extern void DestroyStructure(IntPtr ptr, Type structuretype);

 
        //====================================================================
        // Returns the HInstance for this module.  Returns -1 if the module 
        // doesn't have an HInstance.  In Memory (Dynamic) Modules won't have 
        // an HInstance.
        //=================================================================== 
        [System.Security.SecurityCritical]  // auto-generated_required
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        public static IntPtr GetHINSTANCE(Module m) 
        {
            if (m == null) 
                throw new ArgumentNullException("m"); 
            Contract.EndContractBlock();
 
            RuntimeModule rtModule = m as RuntimeModule;
            if (rtModule == null)
            {
                ModuleBuilder mb = m as ModuleBuilder; 
                if (mb != null)
                    rtModule = mb.InternalModule; 
            } 

            if (rtModule == null) 
                throw new ArgumentNullException(Environment.GetResourceString("Argument_MustBeRuntimeModule"));

            return GetHINSTANCE(rtModule.GetNativeHandle());
        } 

        [System.Security.SecurityCritical]  // auto-generated_required 
        [ResourceExposure(ResourceScope.Machine)] 
        [SuppressUnmanagedCodeSecurity]
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode), SuppressUnmanagedCodeSecurity] 
        private extern static IntPtr GetHINSTANCE(RuntimeModule m);

        //====================================================================
        // Throws a CLR exception based on the HRESULT. 
        //====================================================================
        [System.Security.SecurityCritical]  // auto-generated_required 
        public static void ThrowExceptionForHR(int errorCode) 
        {
            if (errorCode < 0) 
                ThrowExceptionForHRInternal(errorCode, Win32Native.NULL);
        }
        [System.Security.SecurityCritical]  // auto-generated_required
        public static void ThrowExceptionForHR(int errorCode, IntPtr errorInfo) 
        {
            if (errorCode < 0) 
                ThrowExceptionForHRInternal(errorCode, errorInfo); 
        }
 
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal static extern void ThrowExceptionForHRInternal(int errorCode, IntPtr errorInfo);
 

        //=================================================================== 
        // Converts the HRESULT to a CLR exception. 
        //====================================================================
        [System.Security.SecurityCritical]  // auto-generated_required 
        public static Exception GetExceptionForHR(int errorCode)
        {
            if (errorCode < 0)
                return GetExceptionForHRInternal(errorCode, Win32Native.NULL); 
            else
                return null; 
        } 
        [System.Security.SecurityCritical]  // auto-generated_required
        public static Exception GetExceptionForHR(int errorCode, IntPtr errorInfo) 
        {
            if (errorCode < 0)
                return GetExceptionForHRInternal(errorCode, errorInfo);
            else 
                return null;
        } 
 
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        internal static extern Exception GetExceptionForHRInternal(int errorCode, IntPtr errorInfo);


        //=================================================================== 
        // Converts the CLR exception to an HRESULT. This function also sets
        // up an IErrorInfo for the exception. 
        //=================================================================== 
        [System.Security.SecurityCritical]  // auto-generated_required
        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        public static extern int GetHRForException(Exception e);

#if !FEATURE_PAL 
        //===================================================================
        // This method is intended for compiler code generators rather 
        // than applications. 
        //====================================================================
        // 
        [System.Security.SecurityCritical]  // auto-generated_required
        [ObsoleteAttribute("The GetUnmanagedThunkForManagedMethodPtr method has been deprecated and will be removed in a future release.", false)]
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        public static extern IntPtr GetUnmanagedThunkForManagedMethodPtr(IntPtr pfnMethodToWrap, IntPtr pbSignature, int cbSignature);
 
        //=================================================================== 
        // This method is intended for compiler code generators rather
        // than applications. 
        //====================================================================
        //
        [System.Security.SecurityCritical]  // auto-generated_required
        [ObsoleteAttribute("The GetManagedThunkForUnmanagedMethodPtr method has been deprecated and will be removed in a future release.", false)] 
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        public static extern IntPtr GetManagedThunkForUnmanagedMethodPtr(IntPtr pfnMethodToWrap, IntPtr pbSignature, int cbSignature); 

        //==================================================================== 
        // The hosting APIs allow a sophisticated host to schedule fibers
        // onto OS threads, so long as they notify the runtime of this
        // activity.  A fiber cookie can be redeemed for its managed Thread
        // object by calling the following service. 
        //===================================================================
        // 
        [System.Security.SecurityCritical]  // auto-generated_required 
        [ObsoleteAttribute("The GetThreadFromFiberCookie method has been deprecated.  Use the hosting API to perform this operation.", false)]
        public static Thread GetThreadFromFiberCookie(int cookie) 
        {
            if (cookie == 0)
                throw new ArgumentException(Environment.GetResourceString("Argument_ArgumentZero"), "cookie");
            Contract.EndContractBlock(); 

            return InternalGetThreadFromFiberCookie(cookie); 
        } 

        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        private static extern Thread InternalGetThreadFromFiberCookie(int cookie);
#endif //!FEATURE_PAL
 

        //==================================================================== 
        // Memory allocation and dealocation. 
        //===================================================================
        [System.Security.SecurityCritical]  // auto-generated_required 
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        public static IntPtr AllocHGlobal(IntPtr cb)
        {
            IntPtr pNewMem = Win32Native.LocalAlloc_NoSafeHandle(LMEM_FIXED, cb); 

            if (pNewMem == Win32Native.NULL) { 
                throw new OutOfMemoryException(); 
            }
            return pNewMem; 
        }

        [System.Security.SecurityCritical]  // auto-generated_required
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)] 
        public static IntPtr AllocHGlobal(int cb)
        { 
            return AllocHGlobal((IntPtr)cb); 
        }
 
        [System.Security.SecurityCritical]  // auto-generated_required
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        public static void FreeHGlobal(IntPtr hglobal)
        { 
            if (IsNotWin32Atom(hglobal)) {
                if (Win32Native.NULL != Win32Native.LocalFree(hglobal)) { 
                    ThrowExceptionForHR(GetHRForLastWin32Error()); 
                }
            } 
        }

#if !FEATURE_PAL
        [System.Security.SecurityCritical]  // auto-generated_required 
        public static IntPtr ReAllocHGlobal(IntPtr pv, IntPtr cb)
        { 
            IntPtr pNewMem = Win32Native.LocalReAlloc(pv, cb, LMEM_MOVEABLE); 
            if (pNewMem == Win32Native.NULL) {
                throw new OutOfMemoryException(); 
            }
            return pNewMem;
        }
 

        //=================================================================== 
        // String convertions. 
        //===================================================================
        [System.Security.SecurityCritical]  // auto-generated_required 
        public static IntPtr StringToHGlobalAnsi(String s)
        {
            if (s == null)
            { 
                return Win32Native.NULL;
            } 
            else 
            {
                int nb = (s.Length + 1) * SystemMaxDBCSCharSize; 

                // Overflow checking
                if (nb < s.Length)
                    throw new ArgumentOutOfRangeException("s"); 

                IntPtr len = new IntPtr(nb); 
                IntPtr hglobal = Win32Native.LocalAlloc_NoSafeHandle(LMEM_FIXED, len); 

                if (hglobal == Win32Native.NULL) 
                {
                    throw new OutOfMemoryException();
                }
                else 
                {
                    // @ 
                    Win32Native.CopyMemoryAnsi(hglobal, new StringBuilder(s, nb), len); 
                    return hglobal;
                } 
            }
        }
#endif // !FEATURE_PAL
 
        [System.Security.SecurityCritical]  // auto-generated_required
        public static IntPtr StringToHGlobalUni(String s) 
        { 
            if (s == null)
            { 
                return Win32Native.NULL;
            }
            else
            { 
                int nb = (s.Length + 1) * 2;
 
                // Overflow checking 
                if (nb < s.Length)
                    throw new ArgumentOutOfRangeException("s"); 

                IntPtr len = new IntPtr(nb);
                IntPtr hglobal = Win32Native.LocalAlloc_NoSafeHandle(LMEM_FIXED, len);
 
                if (hglobal == Win32Native.NULL)
                { 
                    throw new OutOfMemoryException(); 
                }
                else 
                {
                    Win32Native.CopyMemoryUni(hglobal, s, len);
                    return hglobal;
                } 
            }
        } 
 
#if !FEATURE_PAL
        [System.Security.SecurityCritical]  // auto-generated_required 
        public static IntPtr StringToHGlobalAuto(String s)
        {
            // Ansi platforms are no longer supported
            return StringToHGlobalUni(s); 
        }
#endif //!FEATURE_PAL 
 
#if FEATURE_COMINTEROP
 
        [System.Security.SecurityCritical]  // auto-generated_required
        public static IntPtr StringToCoTaskMemAnsi(String s)
        {
            if (s == null) 
            {
                return Win32Native.NULL; 
            } 
            else
            { 
                int nb = (s.Length + 1) * SystemMaxDBCSCharSize;

                // Overflow checking
                if (nb < s.Length) 
                    throw new ArgumentOutOfRangeException("s");
 
                IntPtr hglobal = Win32Native.CoTaskMemAlloc(nb); 

                if (hglobal == Win32Native.NULL) 
                {
                    throw new OutOfMemoryException();
                }
                else 
                {
                    // @ 
                    Win32Native.CopyMemoryAnsi(hglobal, new StringBuilder(s, nb), new IntPtr(nb)); 
                    return hglobal;
                } 
            }
        }

 		internal static readonly Guid ManagedNameGuid = new Guid("{0F21F359-AB84-41E8-9A78-36D110E6D2F9}"); 

        //==================================================================== 
        // Given a managed object that wraps a UCOMITypeLib, return its name 
        //===================================================================
        [System.Security.SecurityCritical]  // auto-generated_required 
        [Obsolete("Use System.Runtime.InteropServices.Marshal.GetTypeLibName(ITypeLib pTLB) instead. http://go.microsoft.com/fwlink/?linkid=14202&ID=0000011.", false)]
        public static String GetTypeLibName(UCOMITypeLib pTLB)
        {
            return GetTypeLibName((ITypeLib)pTLB); 
        }
 
 
        //====================================================================
        // Given a managed object that wraps an ITypeLib, return its name 
        //====================================================================
        [System.Security.SecurityCritical]  // auto-generated_required
        public static String GetTypeLibName(ITypeLib typelib)
        { 
            if (typelib == null)
                throw new ArgumentNullException("typelib"); 
            Contract.EndContractBlock(); 

            String strTypeLibName = null; 
            String strDocString = null;
            int dwHelpContext = 0;
            String strHelpFile = null;
 
            typelib.GetDocumentation(-1, out strTypeLibName, out strDocString, out dwHelpContext, out strHelpFile);
 
            return strTypeLibName; 
        }
 
        //===================================================================
        // Internal version of GetTypeLibName
        // Support GUID_ManagedName which aligns with TlbImp
        //==================================================================== 
        [System.Security.SecurityCritical]  // auto-generated_required
        internal static String GetTypeLibNameInternal(ITypeLib typelib) 
        { 
            if (typelib == null)
                throw new ArgumentNullException("typelib"); 
            Contract.EndContractBlock();

            // Try GUID_ManagedName first
            ITypeLib2 typeLib2 = typelib as ITypeLib2; 
            if (typeLib2 != null)
            { 
                Guid guid = ManagedNameGuid; 
                object val;
 
                try
                {
                    typeLib2.GetCustData(ref guid, out val);
                } 
                catch(Exception)
                { 
                    val = null; 
                }
 
                if (val != null && val.GetType() == typeof(string))
                {
                    string customManagedNamespace = (string)val;
                    customManagedNamespace = customManagedNamespace.Trim(); 
                    if (customManagedNamespace.EndsWith(".DLL", StringComparison.OrdinalIgnoreCase))
                        customManagedNamespace = customManagedNamespace.Substring(0, customManagedNamespace.Length - 4); 
                    else if (customManagedNamespace.EndsWith(".EXE", StringComparison.OrdinalIgnoreCase)) 
                        customManagedNamespace = customManagedNamespace.Substring(0, customManagedNamespace.Length - 4);
                    return customManagedNamespace; 
                }
            }
			
            return GetTypeLibName(typelib); 
        }
 
 
        //===================================================================
        // Given an managed object that wraps an UCOMITypeLib, return its guid 
        //===================================================================
        [System.Security.SecurityCritical]  // auto-generated_required
        [Obsolete("Use System.Runtime.InteropServices.Marshal.GetTypeLibGuid(ITypeLib pTLB) instead. http://go.microsoft.com/fwlink/?linkid=14202&ID=0000011.", false)]
        public static Guid GetTypeLibGuid(UCOMITypeLib pTLB) 
        {
            return GetTypeLibGuid((ITypeLib)pTLB); 
        } 

        //=================================================================== 
        // Given an managed object that wraps an ITypeLib, return its guid
        //====================================================================
        [System.Security.SecurityCritical]  // auto-generated_required
        public static Guid GetTypeLibGuid(ITypeLib typelib) 
        {
            Guid result = new Guid (); 
            FCallGetTypeLibGuid (ref result, typelib); 
            return result;
        } 

        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        private static extern void FCallGetTypeLibGuid(ref Guid result, ITypeLib pTLB); 

        //=================================================================== 
        // Given a managed object that wraps a UCOMITypeLib, return its lcid 
        //====================================================================
        [System.Security.SecurityCritical]  // auto-generated_required 
        [Obsolete("Use System.Runtime.InteropServices.Marshal.GetTypeLibLcid(ITypeLib pTLB) instead. http://go.microsoft.com/fwlink/?linkid=14202&ID=0000011.", false)]
        public static int GetTypeLibLcid(UCOMITypeLib pTLB)
        {
            return GetTypeLibLcid((ITypeLib)pTLB); 
        }
 
        //==================================================================== 
        // Given a managed object that wraps an ITypeLib, return its lcid
        //=================================================================== 
        [System.Security.SecurityCritical]  // auto-generated_required
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        public static extern int GetTypeLibLcid(ITypeLib typelib); 

        //==================================================================== 
        // Given a managed object that wraps an ITypeLib, return it's 
        // version information.
        //=================================================================== 
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal static extern void GetTypeLibVersion(ITypeLib typeLibrary, out int major, out int minor);
 
        //===================================================================
        // Given a managed object that wraps an ITypeInfo, return its guid. 
        //=================================================================== 
        [System.Security.SecurityCritical]  // auto-generated
        internal static Guid GetTypeInfoGuid(ITypeInfo typeInfo) 
        {
            Guid result = new Guid ();
            FCallGetTypeInfoGuid (ref result, typeInfo);
            return result; 
        }
 
        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        private static extern void FCallGetTypeInfoGuid(ref Guid result, ITypeInfo typeInfo); 

        //====================================================================
        // Given a assembly, return the TLBID that will be generated for the
        // typelib exported from the assembly. 
        //===================================================================
        [System.Security.SecurityCritical]  // auto-generated_required 
        public static Guid GetTypeLibGuidForAssembly(Assembly asm) 
        {
            if (asm == null) 
                throw new ArgumentNullException("asm");
            Contract.EndContractBlock();

            RuntimeAssembly rtAssembly = asm as RuntimeAssembly; 
            if (rtAssembly == null)
                throw new ArgumentException(Environment.GetResourceString("Argument_MustBeRuntimeAssembly"), "asm"); 
 
            Guid result = new Guid();
            FCallGetTypeLibGuidForAssembly(ref result, rtAssembly); 
            return result;
        }

        [ResourceExposure(ResourceScope.None)]  // Scoped to assembly 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        private static extern void FCallGetTypeLibGuidForAssembly(ref Guid result, RuntimeAssembly asm); 
 
        //====================================================================
        // Given a assembly, return the version number of the type library 
        // that would be exported from the assembly.
        //====================================================================
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        private static extern void _GetTypeLibVersionForAssembly(RuntimeAssembly inputAssembly, out int majorVersion, out int minorVersion);
 
        [System.Security.SecurityCritical]  // auto-generated_required 
        [ResourceExposure(ResourceScope.None)]
        public static void GetTypeLibVersionForAssembly(Assembly inputAssembly, out int majorVersion, out int minorVersion) 
        {
            if (inputAssembly == null)
                throw new ArgumentNullException("inputAssembly");
            Contract.EndContractBlock(); 

            RuntimeAssembly rtAssembly = inputAssembly as RuntimeAssembly; 
            if (rtAssembly == null) 
                throw new ArgumentException(Environment.GetResourceString("Argument_MustBeRuntimeAssembly"), "inputAssembly");
 
            _GetTypeLibVersionForAssembly(rtAssembly, out majorVersion, out minorVersion);
        }

        //=================================================================== 
        // Given a managed object that wraps an UCOMITypeInfo, return its name
        //==================================================================== 
        [System.Security.SecurityCritical]  // auto-generated_required 
        [Obsolete("Use System.Runtime.InteropServices.Marshal.GetTypeInfoName(ITypeInfo pTLB) instead. http://go.microsoft.com/fwlink/?linkid=14202&ID=0000011.", false)]
        public static String GetTypeInfoName(UCOMITypeInfo pTI) 
        {
            return GetTypeInfoName((ITypeInfo)pTI);
        }
 
        //===================================================================
        // Given a managed object that wraps an ITypeInfo, return its name 
        //=================================================================== 
        [System.Security.SecurityCritical]  // auto-generated_required
        public static String GetTypeInfoName(ITypeInfo typeInfo) 
        {
            if (typeInfo == null)
                throw new ArgumentNullException("typeInfo");
            Contract.EndContractBlock(); 

            String strTypeLibName = null; 
            String strDocString = null; 
            int dwHelpContext = 0;
            String strHelpFile = null; 

            typeInfo.GetDocumentation(-1, out strTypeLibName, out strDocString, out dwHelpContext, out strHelpFile);

            return strTypeLibName; 
        }
 
        //=================================================================== 
        // Internal version of GetTypeInfoName
        // Support GUID_ManagedName which aligns with TlbImp 
        //====================================================================
        [System.Security.SecurityCritical]  // auto-generated_required
        internal static String GetTypeInfoNameInternal(ITypeInfo typeInfo, out bool hasManagedName)
        { 
            if (typeInfo == null)
                throw new ArgumentNullException("typeInfo"); 
            Contract.EndContractBlock(); 

            // Try ManagedNameGuid first 
            ITypeInfo2 typeInfo2 = typeInfo as ITypeInfo2;
            if (typeInfo2 != null)
            {
                Guid guid = ManagedNameGuid; 
                object val;
 
                try 
                {
                    typeInfo2.GetCustData(ref guid, out val); 
                }
                catch(Exception)
                {
                    val = null; 
                }
 
                if (val != null && val.GetType() == typeof(string)) 
                {
                    hasManagedName = true; 
                    return (string)val;
                }
            }
 
            hasManagedName = false;
            return GetTypeInfoName(typeInfo); 
        } 

        //=================================================================== 
        // Get the corresponding managed name as converted by TlbImp
        // Used to get the type using GetType() from imported assemblies
        //====================================================================
        [System.Security.SecurityCritical]  // auto-generated_required 
        internal static String GetManagedTypeInfoNameInternal(ITypeLib typeLib, ITypeInfo typeInfo)
        { 
            bool hasManagedName; 
            string name = GetTypeInfoNameInternal(typeInfo, out hasManagedName);
            if (hasManagedName) 
                return name;
            else
                return GetTypeLibNameInternal(typeLib) + "." + name;
        } 

        //==================================================================== 
        // If a type with the specified GUID is loaded, this method will 
        // return the reflection type that represents it. Otherwise it returns
        // NULL. 
        //===================================================================
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        private static extern Type GetLoadedTypeForGUID(ref Guid guid); 

#if !FEATURE_CORECLR // current implementation requires reflection only load 
        //==================================================================== 
        // map ITypeInfo* to Type
        //=================================================================== 
        [System.Security.SecurityCritical]  // auto-generated_required
        public static Type GetTypeForITypeInfo(IntPtr /* ITypeInfo* */ piTypeInfo)
        {
            ITypeInfo pTI = null; 
            ITypeLib pTLB = null;
            Type TypeObj = null; 
            Assembly AsmBldr = null; 
            TypeLibConverter TlbConverter = null;
            int Index = 0; 
            Guid clsid;

            // If the input ITypeInfo is NULL then return NULL.
            if (piTypeInfo == Win32Native.NULL) 
                return null;
 
            // Wrap the ITypeInfo in a CLR object. 
            pTI = (ITypeInfo)GetObjectForIUnknown(piTypeInfo);
 
            // Check to see if a class exists with the specified GUID.

            clsid = GetTypeInfoGuid(pTI);
            TypeObj = GetLoadedTypeForGUID(ref clsid); 

            // If we managed to find the type based on the GUID then return it. 
            if (TypeObj != null) 
                return TypeObj;
 
            // There is no type with the specified GUID in the app domain so lets
            // try and convert the containing typelib.
            try
            { 
                pTI.GetContainingTypeLib(out pTLB, out Index);
            } 
            catch(COMException) 
            {
                pTLB = null; 
            }

            // Check to see if we managed to get a containing typelib.
            if (pTLB != null) 
            {
                // Get the assembly name from the typelib. 
                AssemblyName AsmName = TypeLibConverter.GetAssemblyNameFromTypelib(pTLB, null, null, null, null, AssemblyNameFlags.None); 
                String AsmNameString = AsmName.FullName;
 
                // Check to see if the assembly that will contain the type already exists.
                Assembly[] aAssemblies = Thread.GetDomain().GetAssemblies();
                int NumAssemblies = aAssemblies.Length;
                for (int i = 0; i < NumAssemblies; i++) 
                {
                    if (String.Compare(aAssemblies[i].FullName, 
                                       AsmNameString,StringComparison.Ordinal) == 0) 
                        AsmBldr = aAssemblies[i];
                } 

                // If we haven't imported the assembly yet then import it.
                if (AsmBldr == null)
                { 
                    TlbConverter = new TypeLibConverter();
                    AsmBldr = TlbConverter.ConvertTypeLibToAssembly(pTLB, 
                        GetTypeLibName(pTLB) + ".dll", 0, new ImporterCallback(), null, null, null, null); 
                }
 
                // Load the type object from the imported typelib.
                // Call GetManagedTypeInfoNameInternal to align with TlbImp behavior
                TypeObj = AsmBldr.GetType(GetManagedTypeInfoNameInternal(pTLB, pTI), true, false);
                if (TypeObj != null && !TypeObj.IsVisible) 
                    TypeObj = null;
            } 
            else 
            {
                // If the ITypeInfo does not have a containing typelib then simply 
                // return Object as the type.
                TypeObj = typeof(Object);
            }
 
            return TypeObj;
        } 
#endif // #if !FEATURE_CORECLR 

        //=================================================================== 
        // map Type to ITypeInfo*
        //===================================================================
        [System.Security.SecurityCritical]  // auto-generated_required
        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        public static extern IntPtr /* ITypeInfo* */ GetITypeInfoForType(Type t); 
 
        //====================================================================
        // return the IUnknown* for an Object if the current context 
        // is the one where the RCW was first seen. Will return null
        // otherwise.
        //===================================================================
        [System.Security.SecurityCritical]  // auto-generated_required 
        public static IntPtr /* IUnknown* */ GetIUnknownForObject(Object o)
        { 
            return GetIUnknownForObjectNative(o, false); 
        }
 
        [System.Security.SecurityCritical]  // auto-generated_required
        public static IntPtr /* IUnknown* */ GetIUnknownForObjectInContext(Object o)
        {
            return GetIUnknownForObjectNative(o, true); 
        }
 
        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        private static extern IntPtr /* IUnknown* */ GetIUnknownForObjectNative(Object o, bool onlyInContext); 

        //====================================================================
        // return the IDispatch* for an Object
        //==================================================================== 
        [System.Security.SecurityCritical]  // auto-generated_required
        public static IntPtr /* IDispatch */ GetIDispatchForObject(Object o) 
        { 
            return GetIDispatchForObjectNative(o, false);
        } 

        //===================================================================
        // return the IDispatch* for an Object if the current context
        // is the one where the RCW was first seen. Will return null 
        // otherwise.
        //==================================================================== 
        [System.Security.SecurityCritical]  // auto-generated_required 
        public static IntPtr /* IUnknown* */ GetIDispatchForObjectInContext(Object o)
        { 
            return GetIDispatchForObjectNative(o, true);
        }

        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        private static extern IntPtr /* IUnknown* */ GetIDispatchForObjectNative(Object o, bool onlyInContext); 
 
        //===================================================================
        // return the IUnknown* representing the interface for the Object 
        // Object o should support Type T
        //===================================================================
        [System.Security.SecurityCritical]  // auto-generated_required
        public static IntPtr /* IUnknown* */ GetComInterfaceForObject(Object o, Type T) 
        {
            return GetComInterfaceForObjectNative(o, T, false, true); 
        } 

        //=================================================================== 
        // return the IUnknown* representing the interface for the Object
        // Object o should support Type T, it refer the value of mode to
        // invoke customized QueryInterface or not
        //==================================================================== 
        [System.Security.SecurityCritical]  // auto-generated_required
        public static IntPtr /* IUnknown* */ GetComInterfaceForObject(Object o, Type T, CustomQueryInterfaceMode mode) 
        { 
            bool bEnableCustomizedQueryInterface = ((mode == CustomQueryInterfaceMode.Allow) ? true : false);
            return GetComInterfaceForObjectNative(o, T, false, bEnableCustomizedQueryInterface); 
        }

        //===================================================================
        // return the IUnknown* representing the interface for the Object 
        // Object o should support Type T if the current context
        // is the one where the RCW was first seen. Will return null 
        // otherwise. 
        //====================================================================
        [System.Security.SecurityCritical]  // auto-generated_required 
        public static IntPtr /* IUnknown* */ GetComInterfaceForObjectInContext(Object o, Type t)
        {
            return GetComInterfaceForObjectNative(o, t, true, true);
        } 

        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        private static extern IntPtr /* IUnknown* */ GetComInterfaceForObjectNative(Object o, Type t, bool onlyInContext, bool fEnalbeCustomizedQueryInterface);
 
        //====================================================================
        // return an Object for IUnknown
        //===================================================================
        [System.Security.SecurityCritical]  // auto-generated_required 
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        public static extern Object GetObjectForIUnknown(IntPtr /* IUnknown* */ pUnk); 

        //==================================================================== 
        // Return a unique Object given an IUnknown.  This ensures that you
        //  receive a fresh object (we will not look in the cache to match up this
        //  IUnknown to an already existing object).  This is useful in cases
        //  where you want to be able to call ReleaseComObject on a RCW 
        //  and not worry about other active uses of said RCW.
        //=================================================================== 
        [System.Security.SecurityCritical]  // auto-generated_required 
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        public static extern Object GetUniqueObjectForIUnknown(IntPtr unknown);

        //===================================================================
        // return an Object for IUnknown, using the Type T, 
        //  NOTE:
        //  Type T should be either a COM imported Type or a sub-type of COM 
        //  imported Type 
        //===================================================================
        [System.Security.SecurityCritical]  // auto-generated_required 
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        public static extern Object GetTypedObjectForIUnknown(IntPtr /* IUnknown* */ pUnk, Type t);
 
        [System.Security.SecurityCritical]  // auto-generated_required
        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        public static extern IntPtr CreateAggregatedObject(IntPtr pOuter, Object o);
 
        [System.Security.SecurityCritical]  // auto-generated_required
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        public static extern void CleanupUnusedObjectsInCurrentContext(); 

        [System.Security.SecurityCritical] 
        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        public static extern bool AreComObjectsAvailableForCleanup(); 

#endif // FEATURE_COMINTEROP

 
        //====================================================================
        // check if the object is classic COM component 
        //=================================================================== 
#if FEATURE_COMINTEROP
        [System.Security.SecuritySafeCritical] 
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        public static extern bool IsComObject(Object o);
#else 
        [System.Security.SecuritySafeCritical]
        public static bool IsComObject(Object o) 
        { 
            return false;
        } 
#endif // FEATURE_COMINTEROP

        [System.Security.SecurityCritical]  // auto-generated_required
        public static IntPtr AllocCoTaskMem(int cb) 
        {
            IntPtr pNewMem = Win32Native.CoTaskMemAlloc(cb); 
            if (pNewMem == Win32Native.NULL) 
            {
                throw new OutOfMemoryException(); 
            }
            return pNewMem;
        }
 
#if FEATURE_COMINTEROP
        //==================================================================== 
        // release the COM component and if the reference hits 0 zombie this object 
        // further usage of this Object might throw an exception
        //==================================================================== 
        [System.Security.SecurityCritical]  // auto-generated_required
        public static int ReleaseComObject(Object o)
        {
            __ComObject co = null; 

            // Make sure the obj is an __ComObject. 
            try 
            {
                co = (__ComObject)o; 
            }
            catch (InvalidCastException)
            {
                throw new ArgumentException(Environment.GetResourceString("Argument_ObjNotComObject"), "o"); 
            }
 
            return co.ReleaseSelf(); 
        }
 
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal static extern int InternalReleaseComObject(Object o);
 

        //=================================================================== 
        // release the COM component and zombie this object 
        // further usage of this Object might throw an exception
        //==================================================================== 
        [System.Security.SecurityCritical]  // auto-generated_required
        public static Int32 FinalReleaseComObject(Object o)
        {
            if (o == null) 
                throw new ArgumentNullException("o");
            Contract.EndContractBlock(); 
 
            __ComObject co = null;
 
            // Make sure the obj is an __ComObject.
            try
            {
                co = (__ComObject)o; 
            }
            catch (InvalidCastException) 
            { 
                throw new ArgumentException(Environment.GetResourceString("Argument_ObjNotComObject"), "o");
            } 

            co.FinalReleaseSelf();

            return 0; 
        }
 
        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal static extern void InternalFinalReleaseComObject(Object o); 

        //===================================================================
        // This method retrieves data from the COM object.
        //=================================================================== 
        [System.Security.SecurityCritical]  // auto-generated_required
        public static Object GetComObjectData(Object obj, Object key) 
        { 
            // Validate that the arguments aren't null.
            if (obj == null) 
                throw new ArgumentNullException("obj");
            if (key == null)
                throw new ArgumentNullException("key");
            Contract.EndContractBlock(); 

            __ComObject comObj = null; 
 
            // Make sure the obj is an __ComObject.
            try 
            {
                comObj = (__ComObject)obj;
            }
            catch (InvalidCastException) 
            {
                throw new ArgumentException(Environment.GetResourceString("Argument_ObjNotComObject"), "obj"); 
            } 

            // Retrieve the data from the __ComObject. 
            return comObj.GetData(key);
        }

        //=================================================================== 
        // This method sets data on the COM object. The data can only be set
        // once for a given key and cannot be removed. This function returns 
        // true if the data has been added, false if the data could not be 
        // added because there already was data for the specified key.
        //==================================================================== 
        [System.Security.SecurityCritical]  // auto-generated_required
        public static bool SetComObjectData(Object obj, Object key, Object data)
        {
            // Validate that the arguments aren't null. The data can validly be null. 
            if (obj == null)
                throw new ArgumentNullException("obj"); 
            if (key == null) 
                throw new ArgumentNullException("key");
            Contract.EndContractBlock(); 

            __ComObject comObj = null;

            // Make sure the obj is an __ComObject. 
            try
            { 
                comObj = (__ComObject)obj; 
            }
            catch (InvalidCastException) 
            {
                throw new ArgumentException(Environment.GetResourceString("Argument_ObjNotComObject"), "obj");
            }
 
            // Retrieve the data from the __ComObject.
            return comObj.SetData(key, data); 
        } 

        //=================================================================== 
        // This method takes the given COM object and wraps it in an object
        // of the specified type. The type must be derived from __ComObject.
        //====================================================================
        [System.Security.SecurityCritical]  // auto-generated_required 
        public static Object CreateWrapperOfType(Object o, Type t)
        { 
            // Validate the arguments. 
            if (t == null)
                throw new ArgumentNullException("t"); 
            if (!t.IsCOMObject)
                throw new ArgumentException(Environment.GetResourceString("Argument_TypeNotComObject"), "t");
            if (t.IsGenericType)
                throw new ArgumentException(Environment.GetResourceString("Argument_NeedNonGenericType"), "t"); 
            Contract.EndContractBlock();
 
            // Check for the null case. 
            if (o == null)
                return null; 

            // Make sure the object is a COM object.
            if (!o.GetType().IsCOMObject)
                throw new ArgumentException(Environment.GetResourceString("Argument_ObjNotComObject"), "o"); 

            // Check to see if the type of the object is the requested type. 
            if (o.GetType() == t) 
                return o;
 
            // Check to see if we already have a cached wrapper for this type.
            Object Wrapper = GetComObjectData(o, t);
            if (Wrapper == null)
            { 
                // Create the wrapper for the specified type.
                Wrapper = InternalCreateWrapperOfType(o, t); 
 
                // Attempt to cache the wrapper on the object.
                if (!SetComObjectData(o, t, Wrapper)) 
                {
                    // Another thead already cached the wrapper so use that one instead.
                    Wrapper = GetComObjectData(o, t);
                } 
            }
 
            return Wrapper; 
        }
 
        //====================================================================
        // Helper method called from CreateWrapperOfType.
        //===================================================================
        [System.Security.SecurityCritical]  // auto-generated_required 
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        private static extern Object InternalCreateWrapperOfType(Object o, Type t); 

        //==================================================================== 
        // There may be a thread-based cache of COM components.  This service can
        // force the aggressive release of the current thread's cache.
        //===================================================================
        // 
        [System.Security.SecurityCritical]  // auto-generated_required
        [Obsolete("This API did not perform any operation and will be removed in future versions of the CLR.", false)] 
        public static void ReleaseThreadCache() 
        {
        } 

        //===================================================================
        // check if the type is visible from COM.
        //=================================================================== 
        [System.Security.SecuritySafeCritical]
        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        public static extern bool IsTypeVisibleFromCom(Type t);
 
        //====================================================================
        // IUnknown Helpers
        //===================================================================
        [System.Security.SecurityCritical]  // auto-generated_required 
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        public static extern int /* HRESULT */ QueryInterface(IntPtr /* IUnknown */ pUnk, ref Guid iid, out IntPtr ppv); 

        [System.Security.SecurityCritical]  // auto-generated_required 
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        public static extern int /* ULONG */ AddRef(IntPtr /* IUnknown */ pUnk );
        [System.Security.SecurityCritical]  // auto-generated_required 
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)] 
        public static extern int /* ULONG */ Release(IntPtr /* IUnknown */ pUnk );
 
        //====================================================================
        // BSTR allocation and dealocation.
        //====================================================================
        [System.Security.SecurityCritical]  // auto-generated_required 
        public static IntPtr ReAllocCoTaskMem(IntPtr pv, int cb)
        { 
            IntPtr pNewMem = Win32Native.CoTaskMemRealloc(pv, cb); 
            if (pNewMem == Win32Native.NULL && cb != 0) {
                throw new OutOfMemoryException(); 
            }
            return pNewMem;
        }
 
        [System.Security.SecurityCritical]  // auto-generated_required
        public static void FreeCoTaskMem(IntPtr ptr) 
        { 
            if (IsNotWin32Atom(ptr)) {
                Win32Native.CoTaskMemFree(ptr); 
            }
        }

        [System.Security.SecurityCritical]  // auto-generated_required 
        public static void FreeBSTR(IntPtr ptr)
        { 
            if (IsNotWin32Atom(ptr)) { 
                Win32Native.SysFreeString(ptr);
            } 
        }

        [System.Security.SecurityCritical]  // auto-generated_required
        public static IntPtr StringToCoTaskMemUni(String s) 
        {
            if (s == null) 
            { 
                return Win32Native.NULL;
            } 
            else
            {
                int nb = (s.Length + 1) * 2;
 
                // Overflow checking
                if (nb < s.Length) 
                    throw new ArgumentOutOfRangeException("s"); 

                IntPtr hglobal = Win32Native.CoTaskMemAlloc(nb); 

                if (hglobal == Win32Native.NULL)
                {
                    throw new OutOfMemoryException(); 
                }
                else 
                { 
                    Win32Native.CopyMemoryUni(hglobal, s, new IntPtr(nb));
                    return hglobal; 
                }
            }
        }
 
        [System.Security.SecurityCritical]  // auto-generated_required
        public static IntPtr StringToCoTaskMemAuto(String s) 
        { 
            // Ansi platforms are no longer supported
            return StringToCoTaskMemUni(s); 
        }

        [System.Security.SecurityCritical]  // auto-generated_required
        public static IntPtr StringToBSTR(String s) 
        {
            if (s == null) 
                return Win32Native.NULL; 

            // Overflow checking 
            if (s.Length+1 < s.Length)
                throw new ArgumentOutOfRangeException("s");

            IntPtr bstr = Win32Native.SysAllocStringLen(s, s.Length); 
            if (bstr == Win32Native.NULL)
                throw new OutOfMemoryException(); 
 
            return bstr;
        } 

        [System.Security.SecurityCritical]  // auto-generated_required
        public static String PtrToStringBSTR(IntPtr ptr)
        { 
            return PtrToStringUni(ptr, Win32Native.SysStringLen(ptr));
        } 
 
        [System.Security.SecurityCritical]  // auto-generated_required
        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        public static extern void GetNativeVariantForObject(Object obj, /* VARIANT * */ IntPtr pDstNativeVariant);

        [System.Security.SecurityCritical]  // auto-generated_required 
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        public static extern Object GetObjectForNativeVariant(/* VARIANT * */ IntPtr pSrcNativeVariant ); 

        [System.Security.SecurityCritical]  // auto-generated_required 
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        public static extern Object[] GetObjectsForNativeVariants(/* VARIANT * */ IntPtr aSrcNativeVariant, int cVars );
 
        /// <summary>
        /// <para>Returns the first valid COM slot that GetMethodInfoForSlot will work on 
        /// This will be 3 for IUnknown based interfaces and 7 for IDispatch based interfaces. </para> 
        /// </summary>
        [System.Security.SecurityCritical]  // auto-generated_required 
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        public static extern int GetStartComSlot(Type t);
 
        /// <summary>
        /// <para>Returns the last valid COM slot that GetMethodInfoForSlot will work on. </para> 
        /// </summary> 
        [System.Security.SecurityCritical]  // auto-generated_required
        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        public static extern int GetEndComSlot(Type t);

        /// <summary> 
        /// <para>Returns the MemberInfo that COM callers calling through the exposed
        /// vtable on the given slot will be calling. The slot should take into account 
        /// if the exposed interface is IUnknown based or IDispatch based. 
        /// For classes, the lookup is done on the default interface that will be
        /// exposed for the class. </para> 
        /// </summary>
        [System.Security.SecurityCritical]  // auto-generated_required
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        public static extern MemberInfo GetMethodInfoForComSlot(Type t, int slot, ref ComMemberType memberType);
 
        /// <summary> 
        /// <para>Returns the COM slot for a memeber info, taking into account whether
        /// the exposed interface is IUnknown based or IDispatch based</para> 
        /// </summary>
        [System.Security.SecurityCritical]  // auto-generated_required
        public static int GetComSlotForMethodInfo(MemberInfo m)
        { 
            if (m== null)
                throw new ArgumentNullException("m"); 
 
            if (!(m is RuntimeMethodInfo))
                throw new ArgumentException(Environment.GetResourceString("Argument_MustBeRuntimeMethodInfo"), "m"); 

            if (!m.DeclaringType.IsInterface)
                throw new ArgumentException(Environment.GetResourceString("Argument_MustBeInterfaceMethod"), "m");
            if (m.DeclaringType.IsGenericType) 
                throw new ArgumentException(Environment.GetResourceString("Argument_NeedNonGenericType"), "m");
            Contract.EndContractBlock(); 
 
            return InternalGetComSlotForMethodInfo((IRuntimeMethodInfo)m);
        } 

        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        private static extern int InternalGetComSlotForMethodInfo(IRuntimeMethodInfo m); 

        //=================================================================== 
        // This method generates a GUID for the specified type. If the type 
        // has a GUID in the metadata then it is returned otherwise a stable
        // guid GUID is generated based on the fully qualified name of the 
        // type.
        //====================================================================
        [System.Security.SecurityCritical]  // auto-generated_required
        public static Guid GenerateGuidForType(Type type) 
        {
            Guid result = new Guid (); 
            FCallGenerateGuidForType (ref result, type); 
            return result;
        } 

        // The full assembly name is used to compute the GUID, so this should be SxS-safe
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        private static extern void FCallGenerateGuidForType(ref Guid result, Type type);
 
        //=================================================================== 
        // This method generates a PROGID for the specified type. If the type
        // has a PROGID in the metadata then it is returned otherwise a stable 
        // PROGID is generated based on the fully qualified name of the
        // type.
        //===================================================================
        [System.Security.SecurityCritical]  // auto-generated_required 
        public static String GenerateProgIdForType(Type type)
        { 
            if (type == null) 
                throw new ArgumentNullException("type");
            if (type.IsImport) 
                throw new ArgumentException(Environment.GetResourceString("Argument_TypeMustNotBeComImport"), "type");
            if (type.IsGenericType)
                throw new ArgumentException(Environment.GetResourceString("Argument_NeedNonGenericType"), "type");
            Contract.EndContractBlock(); 

            if (!RegistrationServices.TypeRequiresRegistrationHelper(type)) 
                throw new ArgumentException(Environment.GetResourceString("Argument_TypeMustBeComCreatable"), "type"); 

            IList<CustomAttributeData> cas = CustomAttributeData.GetCustomAttributes(type); 
            for (int i = 0; i < cas.Count; i ++)
            {
                if (cas[i].Constructor.DeclaringType == typeof(ProgIdAttribute))
                { 
                    // Retrieve the PROGID string from the ProgIdAttribute.
                    IList<CustomAttributeTypedArgument> caConstructorArgs = cas[i].ConstructorArguments; 
                    Contract.Assert(caConstructorArgs.Count == 1, "caConstructorArgs.Count == 1"); 

                    CustomAttributeTypedArgument progIdConstructorArg = caConstructorArgs[0]; 
                    Contract.Assert(progIdConstructorArg.ArgumentType == typeof(String), "progIdConstructorArg.ArgumentType == typeof(String)");

                    String strProgId = (String)progIdConstructorArg.Value;
 
                    if (strProgId == null)
                        strProgId = String.Empty; 
 
                    return strProgId;
                } 
            }

            // If there is no prog ID attribute then use the full name of the type as the prog id.
            return type.FullName; 
        }
 
        //=================================================================== 
        // This method binds to the specified moniker.
        //==================================================================== 
        [System.Security.SecurityCritical]  // auto-generated_required
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        public static Object BindToMoniker(String monikerName) 
        {
            Object obj = null; 
            IBindCtx bindctx = null; 
            CreateBindCtx(0, out bindctx);
 
            UInt32 cbEaten;
            IMoniker pmoniker = null;
            MkParseDisplayName(bindctx, monikerName, out cbEaten, out pmoniker);
 
            BindMoniker(pmoniker, 0, ref IID_IUnknown, out obj);
            return obj; 
        } 

        //=================================================================== 
        // This method gets the currently running object.
        //====================================================================
        [System.Security.SecurityCritical]  // auto-generated_required
        public static Object GetActiveObject(String progID) 
        {
            Object obj = null; 
            Guid clsid; 

            // Call CLSIDFromProgIDEx first then fall back on CLSIDFromProgID if 
            // CLSIDFromProgIDEx doesn't exist.
            try
            {
                CLSIDFromProgIDEx(progID, out clsid); 
            }
//            catch 
            catch(Exception) 
            {
                CLSIDFromProgID(progID, out clsid); 
            }

            GetActiveObject(ref clsid, IntPtr.Zero, out obj);
            return obj; 
        }
 
        [DllImport(Microsoft.Win32.Win32Native.OLE32, PreserveSig = false)] 
        [ResourceExposure(ResourceScope.None)]
        [SuppressUnmanagedCodeSecurity] 
        [System.Security.SecurityCritical]  // auto-generated
        private static extern void CLSIDFromProgIDEx([MarshalAs(UnmanagedType.LPWStr)] String progId, out Guid clsid);

        [DllImport(Microsoft.Win32.Win32Native.OLE32, PreserveSig = false)] 
        [ResourceExposure(ResourceScope.None)]
        [SuppressUnmanagedCodeSecurity] 
        [System.Security.SecurityCritical]  // auto-generated 
        private static extern void CLSIDFromProgID([MarshalAs(UnmanagedType.LPWStr)] String progId, out Guid clsid);
 
        [DllImport(Microsoft.Win32.Win32Native.OLE32, PreserveSig = false)]
        [ResourceExposure(ResourceScope.None)]
        [SuppressUnmanagedCodeSecurity]
        [System.Security.SecurityCritical]  // auto-generated 
        private static extern void CreateBindCtx(UInt32 reserved, out IBindCtx ppbc);
 
        [DllImport(Microsoft.Win32.Win32Native.OLE32, PreserveSig = false)] 
        [ResourceExposure(ResourceScope.Machine)]
        [SuppressUnmanagedCodeSecurity] 
        [System.Security.SecurityCritical]  // auto-generated
        private static extern void MkParseDisplayName(IBindCtx pbc, [MarshalAs(UnmanagedType.LPWStr)] String szUserName, out UInt32 pchEaten, out IMoniker ppmk);

        [DllImport(Microsoft.Win32.Win32Native.OLE32, PreserveSig = false)] 
        [ResourceExposure(ResourceScope.None)]
        [SuppressUnmanagedCodeSecurity] 
        [System.Security.SecurityCritical]  // auto-generated 
        private static extern void BindMoniker(IMoniker pmk, UInt32 grfOpt, ref Guid iidResult, [MarshalAs(UnmanagedType.Interface)] out Object ppvResult);
 
        [DllImport(Microsoft.Win32.Win32Native.OLEAUT32, PreserveSig = false)]
        [ResourceExposure(ResourceScope.None)]
        [SuppressUnmanagedCodeSecurity]
        [System.Security.SecurityCritical]  // auto-generated 
        private static extern void GetActiveObject(ref Guid rclsid, IntPtr reserved, [MarshalAs(UnmanagedType.Interface)] out Object ppunk);
 
        //======================================================================== 
        // Private method called from remoting to support ServicedComponents.
        //======================================================================= 
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal static extern bool InternalSwitchCCW(Object oldtp, Object newtp);
 
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        internal static extern Object InternalWrapIUnknownWithComObject(IntPtr i); 

        //======================================================================== 
        // Private method called from EE upon use of license/ICF2 marshaling.
        //=======================================================================
        [SecurityCritical]
        private static IntPtr LoadLicenseManager() 
        {
            Assembly sys = Assembly.Load("System, Version="+ ThisAssembly.Version + 
                ", Culture=neutral, PublicKeyToken=" + AssemblyRef.EcmaPublicKeyToken); 
            Type t = sys.GetType("System.ComponentModel.LicenseManager");
            if (t == null || !t.IsVisible) 
                return IntPtr.Zero;
            return t.TypeHandle.Value;
        }
 
        [System.Security.SecurityCritical]  // auto-generated_required
        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        public static extern void ChangeWrapperHandleStrength(Object otp, bool fIsWeak);
#endif // FEATURE_COMINTEROP 

        [System.Security.SecurityCritical]  // auto-generated_required
        public static Delegate GetDelegateForFunctionPointer(IntPtr ptr, Type t)
        { 
            // Validate the parameters
            if (ptr == IntPtr.Zero) 
                throw new ArgumentNullException("ptr"); 

            if (t == null) 
                throw new ArgumentNullException("t");
            Contract.EndContractBlock();

            if ((t as RuntimeType) == null) 
                throw new ArgumentException(Environment.GetResourceString("Argument_MustBeRuntimeType"), "t");
 
            if (t.IsGenericType) 
                throw new ArgumentException(Environment.GetResourceString("Argument_NeedNonGenericType"), "t");
 
            Type c = t.BaseType;
            if (c == null || (c != typeof(Delegate) && c != typeof(MulticastDelegate)))
                throw new ArgumentException(Environment.GetResourceString("Arg_MustBeDelegate"), "t");
 
            return GetDelegateForFunctionPointerInternal(ptr, t);
        } 
 
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        internal static extern Delegate GetDelegateForFunctionPointerInternal(IntPtr ptr, Type t);

        [System.Security.SecurityCritical]  // auto-generated_required
        public static IntPtr GetFunctionPointerForDelegate(Delegate d) 
        {
            if (d == null) 
                throw new ArgumentNullException("d"); 
            Contract.EndContractBlock();
 
            return GetFunctionPointerForDelegateInternal(d);
        }

        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal static extern IntPtr GetFunctionPointerForDelegateInternal(Delegate d); 
 
#if FEATURE_COMINTEROP
        [System.Security.SecurityCritical]  // auto-generated_required 
        public static IntPtr SecureStringToBSTR(SecureString s) {
            if( s == null) {
                throw new ArgumentNullException("s");
            } 
            Contract.EndContractBlock();
 
            return s.ToBSTR(); 
        }
 
        [System.Security.SecurityCritical]  // auto-generated_required
        public static IntPtr SecureStringToCoTaskMemAnsi(SecureString s) {
            if( s == null) {
                throw new ArgumentNullException("s"); 
            }
            Contract.EndContractBlock(); 
 
            return s.ToAnsiStr(false);
        } 

        [System.Security.SecurityCritical]  // auto-generated_required
        public static IntPtr SecureStringToCoTaskMemUnicode(SecureString s)
        { 
            if (s == null)
            { 
                throw new ArgumentNullException("s"); 
            }
            Contract.EndContractBlock(); 

            return s.ToUniStr(false);
        }
 
        [System.Security.SecurityCritical]  // auto-generated_required
        public static void ZeroFreeBSTR(IntPtr s) 
        { 
            Win32Native.ZeroMemory(s, (uint)(Win32Native.SysStringLen(s) * 2));
            FreeBSTR(s); 
        }

        [System.Security.SecurityCritical]  // auto-generated_required
        public static void ZeroFreeCoTaskMemAnsi(IntPtr s) 
        {
            Win32Native.ZeroMemory(s, (uint)(Win32Native.lstrlenA(s))); 
            FreeCoTaskMem(s); 
        }
 
        [System.Security.SecurityCritical]  // auto-generated_required
        public static void ZeroFreeCoTaskMemUnicode(IntPtr s)
        {
            Win32Native.ZeroMemory(s, (uint)(Win32Native.lstrlenW(s) * 2)); 
            FreeCoTaskMem(s);
        } 
#endif // FEATURE_COMINTEROP 

#if FEATURE_CRYPTO || FEATURE_X509_SECURESTRINGS 
        [System.Security.SecurityCritical]  // auto-generated_required
        public static IntPtr SecureStringToGlobalAllocAnsi(SecureString s) {
            if( s == null) {
                throw new ArgumentNullException("s"); 
            }
            Contract.EndContractBlock(); 
 
            return s.ToAnsiStr(true);
        } 

        [System.Security.SecurityCritical]  // auto-generated_required
        public static IntPtr SecureStringToGlobalAllocUnicode(SecureString s) {
            if( s == null) { 
                throw new ArgumentNullException("s");
            } 
            Contract.EndContractBlock(); 

            return s.ToUniStr(true); 
        }
#endif // FEATURE_CRYPTO || FEATURE_X509_SECURESTRINGS

        [System.Security.SecurityCritical]  // auto-generated_required 
        public static void ZeroFreeGlobalAllocAnsi(IntPtr s) {
            Win32Native.ZeroMemory(s, (uint)(Win32Native.lstrlenA(s))); 
            FreeHGlobal(s); 
        }
 
        [System.Security.SecurityCritical]  // auto-generated_required
        public static void ZeroFreeGlobalAllocUnicode(IntPtr s) {
            Win32Native.ZeroMemory(s, (uint)(Win32Native.lstrlenW(s)*2));
            FreeHGlobal(s); 
        }
    } 
 
#if FEATURE_COMINTEROP && !FEATURE_CORECLR // current implementation requires reflection only load
    //======================================================================= 
    // Typelib importer callback implementation.
    //=======================================================================
    internal class ImporterCallback : ITypeLibImporterNotifySink
    { 
        public void ReportEvent(ImporterEventKind EventKind, int EventCode, String EventMsg)
        { 
        } 

        [System.Security.SecuritySafeCritical] // overrides transparent public member 
        public Assembly ResolveRef(Object TypeLib)
        {
            try
            { 
                // Create the TypeLibConverter.
                ITypeLibConverter TLBConv = new TypeLibConverter(); 
 
                // Convert the typelib.
                return TLBConv.ConvertTypeLibToAssembly(TypeLib, 
                                                        Marshal.GetTypeLibName((ITypeLib)TypeLib) + ".dll",
                                                        0,
                                                        new ImporterCallback(),
                                                        null, 
                                                        null,
                                                        null, 
                                                        null); 
            }
            catch(Exception) 
//            catch
            {
                return null;
            } 
        }
    } 
#endif // FEATURE_COMINTEROP && !FEATURE_CORECLR 
}
 



 

 

