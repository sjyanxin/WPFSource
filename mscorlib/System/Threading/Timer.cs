// ==++== 
//
//   Copyright (c) Microsoft Corporation.  All rights reserved.
//
// ==--== 
//
// <OWNER>[....]</OWNER> 
/*============================================================================== 
**
** Class: TimerQueue 
**
**
** Purpose: Class for creating and managing a threadpool
** 
**
=============================================================================*/ 
 
namespace System.Threading {
    using System.Threading; 
    using System;
    using System.Security;
    using System.Security.Permissions;
    using Microsoft.Win32; 
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices; 
    using System.Runtime.ConstrainedExecution; 
    using System.Runtime.Versioning;
    using System.Diagnostics.Contracts; 

    internal class _TimerCallback
    {
        TimerCallback _timerCallback; 
        ExecutionContext _executionContext;
        Object _state; 
        static internal ContextCallback _ccb = new ContextCallback(TimerCallback_Context); 
        static internal void TimerCallback_Context(Object state)
        { 
            _TimerCallback helper = (_TimerCallback) state;
            helper._timerCallback(helper._state);

        } 

        [System.Security.SecurityCritical]  // auto-generated 
        internal _TimerCallback(TimerCallback timerCallback, Object state, ref StackCrawlMark stackMark) 
        {
            _timerCallback = timerCallback; 
            _state = state;
            if (!ExecutionContext.IsFlowSuppressed())
            {
                _executionContext = ExecutionContext.Capture( 
                    ref stackMark,
                    ExecutionContext.CaptureOptions.IgnoreSyncCtx | ExecutionContext.CaptureOptions.OptimizeDefaultCase); 
            } 
        }
 
        // call back helper
        [System.Security.SecurityCritical]  // auto-generated
        static internal void PerformTimerCallback(Object state)
        { 
            _TimerCallback helper = (_TimerCallback)state;
 
            Contract.Assert(helper != null, "Null state passed to PerformTimerCallback!"); 
            // call directly if EC flow is suppressed
            if (helper._executionContext == null) 
            {
                TimerCallback callback = helper._timerCallback;
                callback(helper._state);
            } 
            else
            { 
                // From this point on we can use useExecutionContext for this callback 
                using (ExecutionContext executionContext = helper._executionContext.CreateCopy())
                    ExecutionContext.Run(executionContext, _ccb, helper, true); 
            }
        }
    }
 
[System.Runtime.InteropServices.ComVisible(true)]
    public delegate void TimerCallback(Object state); 
 
    [HostProtection(Synchronization=true, ExternalThreading=true)]
    internal sealed class TimerBase : CriticalFinalizerObject, IDisposable 
    {
#pragma warning disable 169
        private IntPtr     timerHandle;
        private IntPtr     delegateInfo; 
#pragma warning restore 169
        private int        timerDeleted; 
        private int        m_lock = 0; 
#if FEATURE_CORECLR
        // Adding an empty default ctor for annotation purposes 
        internal TimerBase(){}
#endif // FEATURE_CORECLR
        [System.Security.SecuritySafeCritical]  // auto-generated
        ~TimerBase() 
        {
            // lock(this) cannot be used reliably in Cer since thin lock could be 
            // promoted to syncblock and that is not a guaranteed operation 
            bool bLockTaken = false;
            do 
            {
                if (Interlocked.CompareExchange(ref m_lock, 1, 0) == 0)
                {
                    bLockTaken = true; 
                    try
                    { 
                        DeleteTimerNative(null); 
                    }
                    finally 
                    {
                        m_lock = 0;
                    }
                } 
                Thread.SpinWait(1);     // yield to processor
            } 
            while (!bLockTaken); 
        }
 
        [System.Security.SecurityCritical]  // auto-generated
        internal void AddTimer(TimerCallback   callback,
                                           Object          state,
                                           UInt32      dueTime, 
                                           UInt32          period,
                                           ref StackCrawlMark  stackMark 
                                           ) 
        {
            if (callback != null) 
            {
                _TimerCallback callbackHelper = new _TimerCallback(callback, state, ref stackMark);
                state = (Object)callbackHelper;
                AddTimerNative(state, dueTime, period, ref stackMark); 
                timerDeleted = 0;
            } 
            else 
            {
                throw new ArgumentNullException("TimerCallback"); 
            }
        }

        [System.Security.SecurityCritical]  // auto-generated 
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        internal bool ChangeTimer(UInt32 dueTime,UInt32 period) 
        { 
            bool status = false;
            bool bLockTaken = false; 

            // prepare here to prevent threadabort from occuring which could
            // destroy m_lock state.  lock(this) can't be used due to critical
            // finalizer and thinlock/syncblock escalation. 
            RuntimeHelpers.PrepareConstrainedRegions();
            try 
            { 
            }
            finally 
            {
                do
                {
                    if (Interlocked.CompareExchange(ref m_lock, 1, 0) == 0) 
                    {
                        bLockTaken = true; 
                        try 
                        {
                            if (timerDeleted != 0) 
                                throw new ObjectDisposedException(null, Environment.GetResourceString("ObjectDisposed_Generic"));
                            status = ChangeTimerNative(dueTime,period);
                        }
                        finally 
                        {
                            m_lock = 0; 
                        } 
                    }
                    Thread.SpinWait(1);     // yield to processor 
                }
                while (!bLockTaken);
            }
            return status; 

        } 
 
        [System.Security.SecurityCritical]  // auto-generated
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)] 
        [ResourceExposure(ResourceScope.None)]
        [ResourceConsumption(ResourceScope.Machine, ResourceScope.Machine)]
        internal bool Dispose(WaitHandle notifyObject)
        { 
            bool status = false;
            bool bLockTaken = false; 
            RuntimeHelpers.PrepareConstrainedRegions(); 
            try
            { 
            }
            finally
            {
                do 
                {
                    if (Interlocked.CompareExchange(ref m_lock, 1, 0) == 0) 
                    { 
                        bLockTaken = true;
                        try 
                        {
                            status = DeleteTimerNative(notifyObject.SafeWaitHandle);
                        }
                        finally 
                        {
                            m_lock = 0; 
                        } 
                    }
                    Thread.SpinWait(1);     // yield to processor 
                }
                while (!bLockTaken);
                GC.SuppressFinalize(this);
            } 

            return status; 
        } 

        [System.Security.SecuritySafeCritical]  // auto-generated 
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        public void Dispose()
        {
            bool bLockTaken = false; 
            RuntimeHelpers.PrepareConstrainedRegions();
            try 
            { 
            }
            finally 
            {
                do
                {
                    if (Interlocked.CompareExchange(ref m_lock, 1, 0) == 0) 
                    {
                        bLockTaken = true; 
                        try 
                        {
                            DeleteTimerNative(null); 
                        }
                        finally
                        {
                            m_lock = 0; 
                        }
                    } 
                    Thread.SpinWait(1);     // yield to processor 
                }
                while (!bLockTaken); 
                GC.SuppressFinalize(this);
            }
        }
 
        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        private extern void AddTimerNative(Object   state,
                                           UInt32      dueTime, 
                                           UInt32          period,
                                           ref StackCrawlMark  stackMark
                                           );
 
        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        private  extern bool ChangeTimerNative(UInt32 dueTime,UInt32 period);
 
        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        private  extern bool DeleteTimerNative(SafeHandle notifyObject); 

    } 
 
    [HostProtection(Synchronization=true, ExternalThreading=true)]
[System.Runtime.InteropServices.ComVisible(true)] 
#if FEATURE_REMOTING
    public sealed class Timer : MarshalByRefObject, IDisposable {
#if false
    } 
#endif // false
#else // FEATURE_REMOTING 
    public sealed class Timer : IDisposable { 
#endif // FEATURE_REMOTING
        private const UInt32 MAX_SUPPORTED_TIMEOUT = (uint)0xfffffffe; 
        private TimerBase timerBase;

        [System.Security.SecuritySafeCritical]  // auto-generated
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable 
        public Timer(TimerCallback callback,
                     Object        state, 
                     int           dueTime, 
                     int           period)
        { 
            if (dueTime < -1)
                throw new ArgumentOutOfRangeException("dueTime", Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegOrNegative1"));
            if (period < -1 )
                throw new ArgumentOutOfRangeException("period", Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegOrNegative1")); 
            Contract.EndContractBlock();
            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller; 
 
            TimerSetup(callback,state,(UInt32)dueTime,(UInt32)period,ref stackMark);
        } 

        [System.Security.SecuritySafeCritical]  // auto-generated
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable
        public Timer(TimerCallback callback, 
                     Object        state,
                     TimeSpan      dueTime, 
                     TimeSpan      period) 
        {
            long dueTm = (long)dueTime.TotalMilliseconds; 
            if (dueTm < -1)
                throw new ArgumentOutOfRangeException("dueTm",Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegOrNegative1"));
            if (dueTm > MAX_SUPPORTED_TIMEOUT)
                throw new ArgumentOutOfRangeException("dueTm",Environment.GetResourceString("ArgumentOutOfRange_TimeoutTooLarge")); 

            long periodTm = (long)period.TotalMilliseconds; 
            if (periodTm < -1) 
                throw new ArgumentOutOfRangeException("periodTm",Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegOrNegative1"));
            if (periodTm > MAX_SUPPORTED_TIMEOUT) 
                throw new ArgumentOutOfRangeException("periodTm",Environment.GetResourceString("ArgumentOutOfRange_PeriodTooLarge"));

            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller;
            TimerSetup(callback,state,(UInt32)dueTm,(UInt32)periodTm,ref stackMark); 
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated 
        [CLSCompliant(false)]
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable 
        public Timer(TimerCallback callback,
                     Object        state,
                     UInt32        dueTime,
                     UInt32        period) 
        {
            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller; 
            TimerSetup(callback,state,dueTime,period,ref stackMark); 
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable
        public Timer(TimerCallback callback,
                     Object        state, 
                     long          dueTime,
                     long          period) 
        { 
            if (dueTime < -1)
                throw new ArgumentOutOfRangeException("dueTime",Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegOrNegative1")); 
            if (period < -1)
                throw new ArgumentOutOfRangeException("period",Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegOrNegative1"));
            if (dueTime > MAX_SUPPORTED_TIMEOUT)
                throw new ArgumentOutOfRangeException("dueTime",Environment.GetResourceString("ArgumentOutOfRange_TimeoutTooLarge")); 
            if (period > MAX_SUPPORTED_TIMEOUT)
                throw new ArgumentOutOfRangeException("period",Environment.GetResourceString("ArgumentOutOfRange_PeriodTooLarge")); 
            Contract.EndContractBlock(); 
            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller;
            TimerSetup(callback,state,(UInt32) dueTime, (UInt32) period,ref stackMark); 
        }

        [System.Security.SecuritySafeCritical]  // auto-generated
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable 
        public Timer(TimerCallback callback)
        { 
            int dueTime = -1;    // we want timer to be registered, but not activated.  Requires caller to call 
            int period = -1;    // Change after a timer instance is created.  This is to avoid the potential
                                // for a timer to be fired before the returned value is assigned to the variable, 
                                // potentially causing the callback to reference a bogus value (if passing the timer to the callback).

            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller;
            TimerSetup(callback, this, (UInt32)dueTime, (UInt32)period, ref stackMark); 
        }
 
        [System.Security.SecurityCritical]  // auto-generated 
        private void TimerSetup(TimerCallback   callback,
                                                      Object          state, 
                                                      UInt32      dueTime,
                                                      UInt32          period,
                                                      ref StackCrawlMark  stackMark
                                                      ) 
        {
            timerBase = new TimerBase(); 
            timerBase.AddTimer(callback, state,(UInt32) dueTime, (UInt32) period, ref stackMark); 
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated
        public bool Change(int dueTime, int period)
        {
            if (dueTime < -1 ) 
                throw new ArgumentOutOfRangeException("dueTime",Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegOrNegative1"));
            if (period < -1) 
                throw new ArgumentOutOfRangeException("period",Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegOrNegative1")); 
            Contract.EndContractBlock();
 
            return timerBase.ChangeTimer((UInt32)dueTime,(UInt32)period);
        }

        public bool Change(TimeSpan dueTime, TimeSpan period) 
        {
            return Change((long) dueTime.TotalMilliseconds, (long) period.TotalMilliseconds); 
        } 

        [System.Security.SecuritySafeCritical]  // auto-generated 
        [CLSCompliant(false)]
        public bool Change(UInt32 dueTime, UInt32 period)
        {
            return timerBase.ChangeTimer(dueTime,period); 
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated 
        public bool Change(long dueTime, long period)
        { 
            if (dueTime < -1 )
                throw new ArgumentOutOfRangeException("dueTime", Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegOrNegative1"));
            if (period < -1)
                throw new ArgumentOutOfRangeException("period", Environment.GetResourceString("ArgumentOutOfRange_NeedNonNegOrNegative1")); 
            if (dueTime > MAX_SUPPORTED_TIMEOUT)
                throw new ArgumentOutOfRangeException("dueTime", Environment.GetResourceString("ArgumentOutOfRange_TimeoutTooLarge")); 
            if (period > MAX_SUPPORTED_TIMEOUT) 
                throw new ArgumentOutOfRangeException("period", Environment.GetResourceString("ArgumentOutOfRange_PeriodTooLarge"));
            Contract.EndContractBlock(); 

            return timerBase.ChangeTimer((UInt32)dueTime,(UInt32)period);
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated
        public bool Dispose(WaitHandle notifyObject) 
        { 
            if (notifyObject==null)
                throw new ArgumentNullException("notifyObject"); 
            Contract.EndContractBlock();
            return timerBase.Dispose(notifyObject);
        }
 

        public void Dispose() 
        { 
            timerBase.Dispose();
        } 
    }
}

