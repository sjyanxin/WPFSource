// ==++== 
//
//   Copyright (c) Microsoft Corporation.  All rights reserved.
//
// ==--== 
////////////////////////////////////////////////////////////////////////////////
//////////////////////////////////////////////////////////////////////////////// 
// 
// RuntimeHelpers
//    This class defines a set of static methods that provide support for compilers. 
//
// Date: April 2000
//
namespace System.Runtime.CompilerServices { 

    using System; 
    using System.Runtime; 
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices; 
    using System.Runtime.ConstrainedExecution;
    using System.Security.Permissions;
    using System.Threading;
    using System.Runtime.Versioning; 
    using System.Diagnostics.Contracts;
 
    public static class RuntimeHelpers 
    {
        [System.Security.SecuritySafeCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        public static extern void InitializeArray(Array array,RuntimeFieldHandle fldHandle);
 
        // GetObjectValue is intended to allow value classes to be manipulated as 'Object'
        // but have aliasing behavior of a value class.  The intent is that you would use 
        // this function just before an assignment to a variable of type 'Object'.  If the 
        // value being assigned is a mutable value class, then a shallow copy is returned
        // (because value classes have copy semantics), but otherwise the object itself 
        // is returned.
        //
        // Note: VB calls this method when they're about to assign to an Object
        // or pass it as a parameter.  The goal is to make sure that boxed 
        // value types work identical to unboxed value types - ie, they get
        // cloned when you pass them around, and are always passed by value. 
        // Of course, reference types are not cloned. 
        //
        [System.Security.SecuritySafeCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        public static extern Object GetObjectValue(Object obj);
 
        // RunClassConstructor causes the class constructor for the given type to be triggered
        // in the current domain.  After this call returns, the class constructor is guaranteed to 
        // have at least been started by some thread.  In the absence of class constructor 
        // deadlock conditions, the call is further guaranteed to have completed.
        // 
        // This call will generate an exception if the specified class constructor threw an
        // exception when it ran.

        [System.Security.SecuritySafeCritical] 
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        private static extern void _RunClassConstructor(RuntimeType type); 

#if !FEATURE_CORECLR 
        [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
#endif
        public static void RunClassConstructor(RuntimeTypeHandle type)
        { 
            _RunClassConstructor(type.GetRuntimeType());
        } 
 
        // RunModuleConstructor causes the module constructor for the given type to be triggered
        // in the current domain.  After this call returns, the module constructor is guaranteed to 
        // have at least been started by some thread.  In the absence of module constructor
        // deadlock conditions, the call is further guaranteed to have completed.
        //
        // This call will generate an exception if the specified module constructor threw an 
        // exception when it ran.
 
        [System.Security.SecuritySafeCritical] 
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        private static extern void _RunModuleConstructor(System.Reflection.RuntimeModule module);

        public static void RunModuleConstructor(ModuleHandle module)
        { 
           _RunModuleConstructor(module.GetRuntimeModule());
        } 
 
        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        private static unsafe extern void _PrepareMethod(IRuntimeMethodInfo method, IntPtr* pInstantiation, int cInstantiation);

        [System.Security.SecurityCritical]  // auto-generated 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal static extern void _CompileMethod(IRuntimeMethodInfo method); 
 
        // Simple (instantiation not required) method.
        [System.Security.SecurityCritical]  // auto-generated_required 
        public static void PrepareMethod(RuntimeMethodHandle method)
        {
            unsafe
            { 
                _PrepareMethod(method.GetMethodInfo(), null, 0);
            } 
        } 

        // Generic method or method with generic class with specific instantiation. 
        [System.Security.SecurityCritical]  // auto-generated_required
        public static void PrepareMethod(RuntimeMethodHandle method, RuntimeTypeHandle[] instantiation)
        {
            unsafe 
            {
                int length; 
                IntPtr[] instantiationHandles = RuntimeTypeHandle.CopyRuntimeTypeHandles(instantiation, out length); 
                fixed (IntPtr* pInstantiation = instantiationHandles)
                { 
                    _PrepareMethod(method.GetMethodInfo(), pInstantiation, length);
                    GC.KeepAlive(instantiation);
                }
            } 
        }
 
        // This method triggers a given delegate to be prepared.  This involves preparing the 
        // delegate's Invoke method and preparing the target of that Invoke.  In the case of
        // a multi-cast delegate, we rely on the fact that each individual component was prepared 
        // prior to the Combine.  In other words, this service does not navigate through the
        // entire multicasting list.
        // If our own reliable event sinks perform the Combine (for example AppDomain.DomainUnload),
        // then the result is fully prepared.  But if a client calls Combine himself and then 
        // then adds that combination to e.g. AppDomain.DomainUnload, then the client is responsible
        // for his own preparation. 
        [System.Security.SecurityCritical]  // auto-generated_required 
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        public static extern void PrepareDelegate(Delegate d);

        // See comment above for PrepareDelegate
        // 
        // PrepareContractedDelegate weakens this a bit by only assuring that we prepare
        // delegates which also have a ReliabilityContract. This is useful for services that 
        // want to provide opt-in reliability, generally some random event sink providing 
        // always reliable semantics to random event handlers that are likely to have not
        // been written with relability in mind is a lost cause anyway. 
        //
        // NOTE: that for the NGen case you can sidestep the required ReliabilityContract
        // by using the [PrePrepareMethod] attribute.
        [System.Security.SecurityCritical]  // auto-generated_required 
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        public static extern void PrepareContractedDelegate(Delegate d); 

        [System.Security.SecuritySafeCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        public static extern int GetHashCode(Object o);
 
        [System.Security.SecuritySafeCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        public new static extern bool Equals(Object o1, Object o2);
 
        public static int OffsetToStringData
        {
#if !FEATURE_CORECLR
            [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")] 
#endif
            get { 
                // Number of bytes from the address pointed to by a reference to 
                // a String to the first 16-bit character in the String.  Skip
                // over the MethodTable pointer, & String 
                // length.  Of course, the String reference points to the memory
                // after the [....] block, so don't count that.
                // This property allows C#'s fixed statement to work on Strings.
                // On 64 bit platforms, this should be 12 (8+4) and on 32 bit 8 (4+4). 
#if WIN32
                return 8; 
#else 
                return 12;
#endif // WIN32 
            }
        }

        // This method ensures that there is sufficient stack to execute the average Framework function. 
        // If there is not enough stack, then it throws System.InsufficientExecutionStackException.
        // Note: this method is not part of the CER support, and is not to be confused with ProbeForSufficientStack 
        // below. 
        [System.Security.SecurityCritical]
        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        public static extern void EnsureSufficientExecutionStack();
 
        [System.Security.SecurityCritical]  // auto-generated_required
        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        public static extern void ProbeForSufficientStack(); 

        // This method is a marker placed immediately before a try clause to mark the corresponding catch and finally blocks as
        // constrained. There's no code here other than the probe because most of the work is done at JIT time when we spot a call to this routine.
        [System.Security.SecurityCritical]  // auto-generated_required 
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        public static void PrepareConstrainedRegions() 
        { 
            ProbeForSufficientStack();
        } 

        // When we detect a CER with no calls, we can point the JIT to this non-probing version instead
        // as we don't need to probe.
        [System.Security.SecurityCritical]  // auto-generated_required 
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
#if !FEATURE_CORECLR 
        [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")] 
#endif
        public static void PrepareConstrainedRegionsNoOP() 
        {
        }

        public delegate void TryCode(Object userData); 

        public delegate void CleanupCode(Object userData, bool exceptionThrown); 
 
        [System.Security.SecurityCritical]  // auto-generated_required
        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        public static extern void ExecuteCodeWithGuaranteedCleanup(TryCode code, CleanupCode backoutCode, Object userData);

        [PrePrepareMethod] 
#if !FEATURE_CORECLR
        [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")] 
#endif 
        internal static void ExecuteBackoutCodeHelper(Object backoutCode, Object userData, bool exceptionThrown)
        { 
            ((CleanupCode)backoutCode)(userData, exceptionThrown);
        }

        // Roughly equivalent to a CER try/finally that will take a lock, run 
        // the try code, and in the finally block, release the lock.  Calls
        // ExecuteCodeWithGuaranteedCleanup to ensure this will work w.r.t. 
        // stack overflows. 
        // We should consider making this public.
        [System.Security.SecurityCritical]  // auto-generated_required 
        [HostProtection(Synchronization=true)]
        internal static void ExecuteCodeWithLock(Object lockObject, TryCode code, object userState)
        {
            ExecuteWithLockHelper execHelper = new ExecuteWithLockHelper(lockObject, code, userState); 
            ExecuteCodeWithGuaranteedCleanup(s_EnterMonitor, s_ExitMonitor, execHelper);
        } 
 
        private static TryCode s_EnterMonitor = new TryCode(EnterMonitorAndTryCode);
        private static CleanupCode s_ExitMonitor = new CleanupCode(ExitMonitorOnBackout); 

        [System.Security.SecuritySafeCritical]  // auto-generated
        private static void EnterMonitorAndTryCode(Object helper)
        { 
            ExecuteWithLockHelper execHelper = (ExecuteWithLockHelper) helper;
            Contract.Assert(execHelper != null, "ExecuteWithLockHelper is null"); 
            Contract.Assert(execHelper.m_lockObject != null, "LockObject is null"); 
            Contract.Assert(execHelper.m_userCode != null, "UserCode is null");
 
            Monitor.Enter(execHelper.m_lockObject, ref execHelper.m_tookLock);
            execHelper.m_userCode(execHelper.m_userState);
        }
 
        [PrePrepareMethod]
        private static void ExitMonitorOnBackout(Object helper, bool exceptionThrown) 
        { 
            ExecuteWithLockHelper execHelper = (ExecuteWithLockHelper) helper;
            Contract.Assert(execHelper != null, "ExecuteWithLockHelper is null"); 
            Contract.Assert(execHelper.m_lockObject != null, "LockObject is null");

            if (execHelper.m_tookLock)
                Monitor.Exit(execHelper.m_lockObject); 
        }
 
        class ExecuteWithLockHelper 
        {
            internal Object m_lockObject; 
            internal bool   m_tookLock;
            internal TryCode m_userCode;
            internal object m_userState;
 
            internal ExecuteWithLockHelper(Object lockObject, TryCode userCode, object userState)
            { 
                m_lockObject = lockObject; 
                m_userCode = userCode;
                m_userState = userState; 
            }

        }
    } 
}
 

