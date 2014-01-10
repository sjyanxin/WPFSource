// ==++== 
//
//   Copyright (c) Microsoft Corporation.  All rights reserved.
//
// 
// <OWNER>[....]</OWNER>
/*============================================================ 
** 
** Class:  SynchronizationContext
** 
**
** Purpose: Capture synchronization semantics for asynchronous callbacks
**
** 
===========================================================*/
 
namespace System.Threading 
{
    using Microsoft.Win32.SafeHandles; 
    using System.Security.Permissions;
    using System.Runtime.InteropServices;
    using System.Runtime.CompilerServices;
#if FEATURE_CORRUPTING_EXCEPTIONS 
    using System.Runtime.ExceptionServices;
#endif // FEATURE_CORRUPTING_EXCEPTIONS 
    using System.Runtime; 
    using System.Runtime.Versioning;
    using System.Runtime.ConstrainedExecution; 
    using System.Reflection;
    using System.Diagnostics.Contracts;

 
    internal struct SynchronizationContextSwitcher : IDisposable
    { 
        internal SynchronizationContext savedSC; 
        internal SynchronizationContext currSC;
        internal ExecutionContext _ec; 

        public override bool Equals(Object obj)
        {
            if (obj == null || !(obj is SynchronizationContextSwitcher)) 
                return false;
            SynchronizationContextSwitcher sw = (SynchronizationContextSwitcher)obj; 
            return (this.savedSC == sw.savedSC && this.currSC == sw.currSC && this._ec == sw._ec); 
        }
 
        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        } 

        public static bool operator ==(SynchronizationContextSwitcher c1, SynchronizationContextSwitcher c2) 
        { 
            return c1.Equals(c2);
        } 

        public static bool operator !=(SynchronizationContextSwitcher c1, SynchronizationContextSwitcher c2)
        {
            return !c1.Equals(c2); 
        }
 
 

 
        public void Dispose()
        {
            Undo();
        } 

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)] 
#if FEATURE_CORRUPTING_EXCEPTIONS 
        [System.Security.SecuritySafeCritical]
        [HandleProcessCorruptedStateExceptions] // 
#endif // FEATURE_CORRUPTING_EXCEPTIONS
        internal bool UndoNoThrow()
        {
            if (_ec  == null) 
            {
                return true; 
            } 

            try 
            {
                Undo();
            }
            catch 
            {
                return false; 
            } 
            return true;
        } 
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        public void Undo()
        {
            if (_ec  == null) 
            {
                return; 
            } 

            ExecutionContext  executionContext = Thread.CurrentThread.GetExecutionContextNoCreate(); 
            if (_ec != executionContext)
            {
                throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_SwitcherCtxMismatch"));
            } 
            if (currSC != _ec.SynchronizationContext)
            { 
                throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_SwitcherCtxMismatch")); 
            }
            Contract.Assert(executionContext != null, " ExecutionContext can't be null"); 
            // restore the Saved [....] context as current
            executionContext.SynchronizationContext = savedSC;
            // can't reuse this anymore
            _ec = null; 
        }
    } 
 

#if FEATURE_SYNCHRONIZATIONCONTEXT_WAIT 
    [Flags]
    enum SynchronizationContextProperties
    {
        None = 0, 
        RequireWaitNotification = 0x1
    }; 
#endif 

    [SecurityPermissionAttribute(SecurityAction.InheritanceDemand, Flags =SecurityPermissionFlag.ControlPolicy|SecurityPermissionFlag.ControlEvidence)] 
    public class SynchronizationContext
    {
#if FEATURE_SYNCHRONIZATIONCONTEXT_WAIT
        SynchronizationContextProperties _props = SynchronizationContextProperties.None; 
#endif
 
        public SynchronizationContext() 
        {
        } 

#if FEATURE_SYNCHRONIZATIONCONTEXT_WAIT
        // protected so that only the derived [....] context class can enable these flags
        [System.Security.SecuritySafeCritical]  // auto-generated 
        protected void SetWaitNotificationRequired()
        { 
            // Prepare the method so that it can be called in a reliable fashion when a wait is needed. 
            // This will obviously only make the Wait reliable if the Wait method is itself reliable. The only thing
            // preparing the method here does is to ensure there is no failure point before the method execution begins. 

            RuntimeHelpers.PrepareDelegate(new WaitDelegate(this.Wait));
            _props |= SynchronizationContextProperties.RequireWaitNotification;
        } 

        public bool IsWaitNotificationRequired() 
        { 
            return ((_props & SynchronizationContextProperties.RequireWaitNotification) != 0);
        } 
#endif


        public virtual void Send(SendOrPostCallback d, Object state) 
        {
            d(state); 
        } 

        public virtual void Post(SendOrPostCallback d, Object state) 
        {
            ThreadPool.QueueUserWorkItem(new WaitCallback(d), state);
        }
 

        /// <summary> 
        ///     Optional override for subclasses, for responding to notification that operation is starting. 
        /// </summary>
        public virtual void OperationStarted() 
        {
        }

        /// <summary> 
        ///     Optional override for subclasses, for responding to notification that operation has completed.
        /// </summary> 
        public virtual void OperationCompleted() 
        {
        } 

#if FEATURE_SYNCHRONIZATIONCONTEXT_WAIT
        // Method called when the CLR does a wait operation
        [System.Security.SecurityCritical]  // auto-generated_required 
        [CLSCompliant(false)]
        [PrePrepareMethod] 
        public virtual int Wait(IntPtr[] waitHandles, bool waitAll, int millisecondsTimeout) 
        {
            if (waitHandles == null) 
            {
                throw new ArgumentNullException("waitHandles");
            }
            Contract.EndContractBlock(); 
            return WaitHelper(waitHandles, waitAll, millisecondsTimeout);
        } 
 
        // Static helper to which the above method can delegate to in order to get the default
        // COM behavior. 
        [System.Security.SecurityCritical]  // auto-generated_required
        [CLSCompliant(false)]
        [PrePrepareMethod]
        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)] 
        protected static extern int WaitHelper(IntPtr[] waitHandles, bool waitAll, int millisecondsTimeout); 
#endif
 
        // set SynchronizationContext on the current thread
        [System.Security.SecurityCritical]  // auto-generated_required
#if !FEATURE_CORECLR
        [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")] 
#endif
        public static void SetSynchronizationContext(SynchronizationContext syncContext) 
        { 
            SetSynchronizationContext(syncContext, Thread.CurrentThread.ExecutionContext.SynchronizationContext);
        } 

        [System.Security.SecurityCritical]  // auto-generated
#if FEATURE_CORRUPTING_EXCEPTIONS
        [HandleProcessCorruptedStateExceptions] // 
#endif // FEATURE_CORRUPTING_EXCEPTIONS
        internal static SynchronizationContextSwitcher SetSynchronizationContext(SynchronizationContext syncContext, SynchronizationContext prevSyncContext) 
        { 
            // get current execution context
            ExecutionContext ec = Thread.CurrentThread.ExecutionContext; 
            // create a swticher
            SynchronizationContextSwitcher scsw = new SynchronizationContextSwitcher();

            RuntimeHelpers.PrepareConstrainedRegions(); 
            try
            { 
                // attach the switcher to the exec context 
                scsw._ec = ec;
                // save the current [....] context using the passed in value 
                scsw.savedSC = prevSyncContext;
                // save the new [....] context also
                scsw.currSC = syncContext;
                // update the current [....] context to the new context 
                ec.SynchronizationContext = syncContext;
            } 
            catch 
            {
                // Any exception means we just restore the old SyncCtx 
                scsw.UndoNoThrow(); //No exception will be thrown in this Undo()
                throw;
            }
            // return switcher 
            return scsw;
        } 
 
#if FEATURE_CORECLR
        // 
        // This is a framework-internal method for Jolt's use.  The problem is that SynchronizationContexts set inside of a reverse p/invoke
        // into an AppDomain are not persisted in that AppDomain; the next time the same thread calls into the same AppDomain,
        // the [....] context will be null.  For Silverlight, this means that it's impossible to persist a [....] context on the UI thread,
        // since Jolt is constantly transitioning in and out of each control's AppDomain on that thread. 
        //
        // So for Jolt we will track a special thread-static context, which *will* persist across calls from Jolt, and if the thread does not 
        // have a [....] context set in its execution context we'll use the thread-static context instead. 
        //
        // This will break any future work that requires SynchronizationContext.Current to be in [....] with the value 
        // stored in a thread's ExecutionContext (wait notifications being one such example).  If that becomes a problem, we will
        // need to rework this mechanism (which is one reason it's not being exposed publically).
        //
 
        [ThreadStatic]
        private static SynchronizationContext s_threadStaticContext; 
 
        [System.Security.SecurityCritical]
        internal static void SetThreadStaticContext(SynchronizationContext syncContext) 
        {
            s_threadStaticContext = syncContext;
        }
#endif 

        // Get the current SynchronizationContext on the current thread 
        public static SynchronizationContext Current 
        {
#if !FEATURE_CORECLR 
            [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
#endif
            get
            { 
                SynchronizationContext context = null;
                ExecutionContext ec = Thread.CurrentThread.GetExecutionContextNoCreate(); 
                if (ec != null) 
                {
                    context = ec.SynchronizationContext; 
                }

#if FEATURE_CORECLR
                // hack for Silverlight 2 beta 2.  See comments on SetThreadStaticContext 
                if (context == null)
                { 
                    context = s_threadStaticContext; 
                }
#endif 

                return context;
            }
        } 

 
        // helper to Clone this SynchronizationContext, 
        public virtual SynchronizationContext CreateCopy()
        { 
            // the CLR dummy has an empty clone function - no member data
            return new SynchronizationContext();
        }
 
#if FEATURE_SYNCHRONIZATIONCONTEXT_WAIT
        [System.Security.SecurityCritical]  // auto-generated 
        private static int InvokeWaitMethodHelper(SynchronizationContext syncContext, IntPtr[] waitHandles, bool waitAll, int millisecondsTimeout) 
        {
            return syncContext.Wait(waitHandles, waitAll, millisecondsTimeout); 
        }
#endif
    }
} 

