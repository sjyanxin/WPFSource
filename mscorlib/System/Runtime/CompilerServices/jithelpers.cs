// ==++== 
//
//   Copyright (c) Microsoft Corporation.  All rights reserved.
//
// ==--== 
////////////////////////////////////////////////////////////////////////////////
// JitHelpers 
//    Low-level Jit Helpers 
////////////////////////////////////////////////////////////////////////////////
 
using System;
using System.Threading;
using System.Runtime;
using System.Runtime.Versioning; 
using System.Diagnostics.Contracts;
using System.Runtime.InteropServices; 
 
namespace System.Runtime.CompilerServices {
 
    // Wrapper for address of a string variable on stack
    internal struct StringHandleOnStack
    {
        private IntPtr m_ptr; 

        internal StringHandleOnStack(IntPtr pString) 
        { 
            m_ptr = pString;
        } 
    }

    // Wrapper for address of a object variable on stack
    internal struct ObjectHandleOnStack 
    {
        private IntPtr m_ptr; 
 
        internal ObjectHandleOnStack(IntPtr pObject)
        { 
            m_ptr = pObject;
        }
    }
 
    // Wrapper for StackCrawlMark
    internal struct StackCrawlMarkHandle 
    { 
        private IntPtr m_ptr;
 
        internal StackCrawlMarkHandle(IntPtr stackMark)
        {
            m_ptr = stackMark;
        } 
    }
 
    // Helper class to assist with unsafe pinning of arbitrary objects. The typical usage pattern is: 
    // fixed (byte * pData = &JitHelpers.GetPinningHelper(value).m_data)
    // { 
    //    ... pData is what Object::GetData() returns in VM ...
    // }
    internal class PinningHelper
    { 
        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization] 
        #endif //!FEATURE_CORECLR 
        public byte m_data;
    } 

    internal static class JitHelpers
    {
        // The special dll name to be used for DllImport of QCalls 
        internal const string QCall = "QCall";
 
        // Wraps object variable into a handle. Used to return managed strings from QCalls. 
        // s has to be a local variable on the stack.
        [System.Security.SecurityCritical]  // auto-generated 
#if !FEATURE_CORECLR
        [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
#endif
        static internal StringHandleOnStack GetStringHandleOnStack(ref string s) 
        {
            return new StringHandleOnStack(__makeref(s).GetPointerOnStack()); 
        } 

        // Wraps object variable into a handle. Used to pass managed object references in and out of QCalls. 
        // o has to be a local variable on the stack.
        [System.Security.SecurityCritical]  // auto-generated
#if !FEATURE_CORECLR
        [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")] 
#endif
        static internal ObjectHandleOnStack GetObjectHandleOnStack<T>(ref T o) where T : class 
        { 
            return new ObjectHandleOnStack(__makeref(o).GetPointerOnStack());
        } 

        // Wraps StackCrawlMark into a handle. Used to pass StackCrawlMark to QCalls.
        // stackMark has to be a local variable on the stack.
        [System.Security.SecurityCritical]  // auto-generated 
#if !FEATURE_CORECLR
        [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")] 
#endif 
        static internal StackCrawlMarkHandle GetStackCrawlMarkHandle(ref StackCrawlMark stackMark)
        { 
            return new StackCrawlMarkHandle(__makeref(stackMark).GetPointerOnStack());
        }

 

#if _DEBUG 
        [System.Security.SecuritySafeCritical]  // auto-generated 
        static internal T UnsafeCast<T>(Object o) where T : class
        { 
            // Make sure that both intrinsic and slow versions return same value
            T ret = UnsafeCastInternal<T>(o);
            Contract.Assert(ret == (o as T), "Invalid use of JitHelpers.UnsafeCast!");
            return ret; 
        }
 
        [System.Security.SecurityCritical] 
        static private T UnsafeCastInternal<T>(Object o) where T : class
        { 
            // The body of this function will be replaced by the EE with unsafe code that just returns o!!!
            // See getILIntrinsicImplementation for how this happens.
            return o as T;
        } 

        [System.Security.SecuritySafeCritical]  // auto-generated 
        static internal int UnsafeEnumCast<T>(T val) where T : struct		// Actually T must be 4 byte enum 
        {
            Contract.Assert(typeof(T).IsEnum && Enum.GetUnderlyingType(typeof(T)) == typeof(int), "Error, T must be an enum JitHelpers.UnsafeEnumCast!"); 
            return UnsafeEnumCastInternal<T>(val);
        }

        [System.Security.SecuritySafeCritical]  // auto-generated 
        static private int UnsafeEnumCastInternal<T>(T val) where T : struct		// Actually T must be 4 byte enum
        { 
            // should be return (int) val; but C# does not allow, runtime does this magically 
            // See getILIntrinsicImplementation for how this happens.
            throw new InvalidOperationException(); 
        }

#else // _DEBUG
        [System.Security.SecuritySafeCritical]  // auto-generated 
#if !FEATURE_CORECLR
        [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")] 
#endif 
        static internal T UnsafeCast<T>(Object o) where T : class
        { 
            // The body of this function will be replaced by the EE with unsafe code that just returns o!!!
            // See getILIntrinsicImplementation for how this happens.
            return o as T;
        } 

        [System.Security.SecuritySafeCritical]  // auto-generated 
#if !FEATURE_CORECLR 
        [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
#endif 
        static internal int UnsafeEnumCast<T>(T val) where T : struct		// Actually T must be 4 byte enum
        {
            // should be return (int) val; but C# does not allow, runtime does this magically
            // See getILIntrinsicImplementation for how this happens. 
            throw new InvalidOperationException();
        } 
#endif // _DEBUG 

 
        // Set the given element in the array without any type or range checks
        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        extern static internal void UnsafeSetArrayElement(Object[] target, int index, Object element);
 
        // Used for unsafe pinning of arbitrary objects. 
        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        extern static internal PinningHelper GetPinningHelper(Object o);
    }
} 

