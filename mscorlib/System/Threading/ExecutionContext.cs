// ==++== 
//
//   Copyright (c) Microsoft Corporation.  All rights reserved.
//
// 
// <OWNER>[....]</OWNER>
/*============================================================ 
** 
** Class:  ExecutionContext
** 
**
** Purpose: Capture execution  context for a thread
**
** 
===========================================================*/
namespace System.Threading 
{ 
    using System;
    using System.Security; 
    using System.Runtime.Remoting;
    using System.Security.Principal;
    using System.Collections;
    using System.Reflection; 
    using System.Runtime.Serialization;
    using System.Security.Permissions; 
    using System.Runtime.Remoting.Messaging; 
    using System.Runtime.InteropServices;
    using System.Runtime.CompilerServices; 
#if FEATURE_CORRUPTING_EXCEPTIONS
    using System.Runtime.ExceptionServices;
#endif // FEATURE_CORRUPTING_EXCEPTIONS
    using System.Runtime.ConstrainedExecution; 
    using System.Diagnostics.Contracts;
 
    internal enum ExceptionType 
    {
        InvalidOperation = 0, 
        Security = 1,
        EE = 2,
        Generic = 3
    } 
    // helper delegate to statically bind to Wait method
    internal delegate int WaitDelegate(IntPtr[] waitHandles, bool waitAll, int millisecondsTimeout); 
 

    internal struct ExecutionContextSwitcher: IDisposable 
    {
        internal ExecutionContext prevEC; // previous EC we need to restore on Undo
        internal ExecutionContext currEC; // current EC that we store for checking correctness
#if FEATURE_IMPERSONATION || FEATURE_COMPRESSEDSTACK 
        internal SecurityContextSwitcher scsw;
#endif // #if FEATURE_IMPERSONATION || FEATURE_COMPRESSEDSTACK 
#if FEATURE_SYNCHRONIZATIONCONTEXT 
        internal SynchronizationContextSwitcher sysw;
#endif // #if FEATURE_SYNCHRONIZATIONCONTEXT 
        internal Object hecsw;
        internal Thread thread;

        public override bool Equals(Object obj) 
        {
            if (obj == null || !(obj is ExecutionContextSwitcher)) 
                return false; 
            ExecutionContextSwitcher sw = (ExecutionContextSwitcher)obj;
            return (this.prevEC == sw.prevEC && this.currEC == sw.currEC && 
#if FEATURE_IMPERSONATION || FEATURE_COMPRESSEDSTACK
                this.scsw == sw.scsw &&
#endif // #if FEATURE_IMPERSONATION || FEATURE_COMPRESSEDSTACK
#if FEATURE_SYNCHRONIZATIONCONTEXT 
                this.sysw == sw.sysw &&
#endif // #if FEATURE_SYNCHRONIZATIONCONTEXT 
#if FEATURE_CAS_POLICY 
                this.hecsw == sw.hecsw &&
#endif // #if FEATURE_CAS_POLICY 
                this.thread == sw.thread);
        }

        public override int GetHashCode() 
        {
            return ToString().GetHashCode(); 
        } 

        public static bool operator ==(ExecutionContextSwitcher c1, ExecutionContextSwitcher c2) 
        {
            return c1.Equals(c2);
        }
 
        public static bool operator !=(ExecutionContextSwitcher c1, ExecutionContextSwitcher c2)
        { 
            return !c1.Equals(c2); 
        }
 
        [System.Security.SecuritySafeCritical] // overrides public transparent member
        public void Dispose()
        {
            Undo(); 
        }
 
        [System.Security.SecurityCritical]  // auto-generated 
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
#if FEATURE_CORRUPTING_EXCEPTIONS 
        [HandleProcessCorruptedStateExceptions] //
#endif // FEATURE_CORRUPTING_EXCEPTIONS
        internal bool UndoNoThrow()
        { 
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
 
        [System.Security.SecurityCritical]  // auto-generated 
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        public void Undo() 
        {
            if (thread == null)
            {
                return; // Don't do anything 
            }
            if (thread != Thread.CurrentThread) 
            { 
                throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_CannotUseSwitcherOtherThread"));
            } 
            if ( currEC != Thread.CurrentThread.GetExecutionContextNoCreate())
            {
                throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_SwitcherCtxMismatch"));
            } 
            Contract.Assert(currEC != null, " ExecutionContext can't be null");
 
#if FEATURE_IMPERSONATION || FEATURE_COMPRESSEDSTACK 
            // Any critical failure inside scsw will cause FailFast
            scsw.Undo(); 
#endif // #if FEATURE_IMPERSONATION || FEATURE_COMPRESSEDSTACK

            try
            { 
#if FEATURE_CAS_POLICY
                HostExecutionContextSwitcher.Undo(hecsw); 
#endif // FEATURE_CAS_POLICY 
            }
            finally 
            {
#if FEATURE_SYNCHRONIZATIONCONTEXT
                // Even if HostExecutionContextSwitcher.Undo(hecsw) throws, we need to revert
                // synchronizationContext. If that throws, we'll be throwing an ex during an exception 
                // unwind. That's OK - we'll just have nested exceptions.
                sysw.Undo(); 
#endif // #if FEATURE_SYNCHRONIZATIONCONTEXT 
            }
 
            // restore the saved Execution Context
            Thread.CurrentThread.SetExecutionContext(prevEC);
            thread = null; // this will prevent the switcher object being used again
 

        } 
    } 

 
    public struct AsyncFlowControl: IDisposable
    {
        private bool useEC;
        private ExecutionContext _ec; 
#if FEATURE_IMPERSONATION || FEATURE_COMPRESSEDSTACK
        private SecurityContext _sc; 
#endif // #if FEATURE_IMPERSONATION || FEATURE_COMPRESSEDSTACK 
        private Thread _thread;
#if FEATURE_IMPERSONATION || FEATURE_COMPRESSEDSTACK 
        internal void Setup(SecurityContextDisableFlow flags)
        {
            useEC = false;
            _sc = Thread.CurrentThread.ExecutionContext.SecurityContext; 
            _sc._disableFlow = flags;
            _thread = Thread.CurrentThread; 
        } 
#endif // #if FEATURE_IMPERSONATION || FEATURE_COMPRESSEDSTACK
        internal void Setup() 
        {
            useEC = true;
            _ec = Thread.CurrentThread.ExecutionContext;
            _ec.isFlowSuppressed = true; 
            _thread = Thread.CurrentThread;
        } 
 
        public void Dispose()
        { 
            Undo();
        }

        public void Undo() 
        {
            if (_thread == null) 
            { 
                throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_CannotUseAFCMultiple"));
            } 
            if (_thread != Thread.CurrentThread)
            {
                throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_CannotUseAFCOtherThread"));
            } 
            if (useEC)
            { 
                if (Thread.CurrentThread.ExecutionContext != _ec) 
                {
                    throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_AsyncFlowCtrlCtxMismatch")); 
                }
                ExecutionContext.RestoreFlow();
            }
#if FEATURE_IMPERSONATION || FEATURE_COMPRESSEDSTACK 
            else
            { 
                if (Thread.CurrentThread.ExecutionContext.SecurityContext != _sc) 
                {
                    throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_AsyncFlowCtrlCtxMismatch")); 
                }
                SecurityContext.RestoreFlow();
            }
#endif // #if FEATURE_IMPERSONATION || FEATURE_COMPRESSEDSTACK 
            _thread = null;
        } 
 
        public override int GetHashCode()
        { 
            // review - [....]
            return _thread == null ? ToString().GetHashCode() : _thread.GetHashCode();
        }
 
        public override bool Equals(Object obj)
        { 
            if (obj is AsyncFlowControl) 
                return Equals((AsyncFlowControl)obj);
            else 
                return false;
        }

        public bool Equals(AsyncFlowControl obj) 
        {
            return obj.useEC == useEC && obj._ec == _ec && 
#if FEATURE_IMPERSONATION || FEATURE_COMPRESSEDSTACK 
                obj._sc == _sc &&
#endif // #if FEATURE_IMPERSONATION || FEATURE_COMPRESSEDSTACK 
                obj._thread == _thread;
        }

        public static bool operator ==(AsyncFlowControl a, AsyncFlowControl b) 
        {
            return a.Equals(b); 
        } 

        public static bool operator !=(AsyncFlowControl a, AsyncFlowControl b) 
        {
            return !(a == b);
        }
 
    }
 
    [System.Runtime.InteropServices.ComVisible(true)] 
    public delegate void ContextCallback(Object state);
 

    [Serializable]
    public sealed class ExecutionContext : IDisposable, ISerializable
    { 
        /*=========================================================================
        ** Data accessed from managed code that needs to be defined in 
        ** ExecutionContextObject  to maintain alignment between the two classes. 
        ** DON'T CHANGE THESE UNLESS YOU MODIFY ExecutionContextObject in vm\object.h
        =========================================================================*/ 
#if FEATURE_CAS_POLICY
        private HostExecutionContext _hostExecutionContext;
#endif // FEATURE_CAS_POLICY
#if FEATURE_SYNCHRONIZATIONCONTEXT 
        private SynchronizationContext _syncContext;
#endif // FEATURE_SYNCHRONIZATIONCONTEXT 
#if FEATURE_IMPERSONATION || FEATURE_COMPRESSEDSTACK 
        private SecurityContext     _securityContext;
#endif // #if FEATURE_IMPERSONATION || FEATURE_COMPRESSEDSTACK 
        [System.Security.SecurityCritical /*auto-generated*/]
        private LogicalCallContext  _logicalCallContext;
        private IllogicalCallContext _illogicalCallContext;  // this call context follows the physical thread
        private Thread          _thread; 
        internal bool isNewCapture = false;
        internal bool isFlowSuppressed = false; 
 
        private static readonly ExecutionContext s_dummyDefaultEC = new ExecutionContext();
 

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        internal ExecutionContext()
        { 
        }
 
        internal LogicalCallContext LogicalCallContext 
        {
            [System.Security.SecurityCritical]  // auto-generated 
            get
            {
                if (_logicalCallContext == null)
                { 
                _logicalCallContext = new LogicalCallContext();
                } 
                return _logicalCallContext; 
            }
            [System.Security.SecurityCritical]  // auto-generated 
            set
            {
                Contract.Assert(this != s_dummyDefaultEC);
                _logicalCallContext = value; 
            }
        } 
 
        internal IllogicalCallContext IllogicalCallContext
        { 
            get
            {
                if (_illogicalCallContext == null)
                { 
                _illogicalCallContext = new IllogicalCallContext();
                } 
                return _illogicalCallContext; 
            }
            set 
            {
                Contract.Assert(this != s_dummyDefaultEC);
                _illogicalCallContext = value;
            } 
        }
        internal Thread Thread 
        { 
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
            set 
            {
                Contract.Assert(this != s_dummyDefaultEC);
                _thread = value;
            } 
        }
 
#if FEATURE_SYNCHRONIZATIONCONTEXT 
        internal SynchronizationContext SynchronizationContext
        { 
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
            get
            {
                return _syncContext; 
            }
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)] 
            set 
            {
                Contract.Assert(this != s_dummyDefaultEC); 
                _syncContext = value;
            }
        }
#endif // #if FEATURE_SYNCHRONIZATIONCONTEXT 
#if FEATURE_CAS_POLICY
    internal HostExecutionContext HostExecutionContext 
    { 
            get
            { 
                return _hostExecutionContext;
            }
            set
            { 
                Contract.Assert(this != s_dummyDefaultEC);
                _hostExecutionContext = value; 
            } 
    }
#endif // FEATURE_CAS_POLICY 
#if FEATURE_IMPERSONATION || FEATURE_COMPRESSEDSTACK
        internal  SecurityContext SecurityContext
        {
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)] 
            get
            { 
                return _securityContext; 
            }
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)] 
            set
            {
                Contract.Assert(this != s_dummyDefaultEC);
                        // store the new security context 
                        _securityContext = value;
                        // perform the reverse link too 
                        if (value != null) 
                            _securityContext.ExecutionContext = this;
            } 
        }
#endif // #if FEATURE_IMPERSONATION || FEATURE_COMPRESSEDSTACK

 
        [System.Security.SecuritySafeCritical]  // auto-generated
        public void Dispose() 
        { 
            Contract.Assert(this != s_dummyDefaultEC);
#if FEATURE_CAS_POLICY 
            if (_hostExecutionContext != null)
                _hostExecutionContext.Dispose();
#endif // FEATURE_CAS_POLICY
#if FEATURE_IMPERSONATION || FEATURE_COMPRESSEDSTACK 
            if (_securityContext != null)
                _securityContext.Dispose(); 
#endif //FEATURE_IMPERSONATION || FEATURE_COMPRESSEDSTACK 
        }
 
        [DynamicSecurityMethod]
        [System.Security.SecurityCritical]  // auto-generated_required
        public static void Run(ExecutionContext executionContext, ContextCallback callback, Object state)
        { 
            Run(executionContext, callback, state, false);
        } 
 
        // This method is special from a security perspective - the VM will not allow a stack walk to
        // continue past the call to ExecutionContext.Run.  If you change the signature to this method, make 
        // sure to update SecurityStackWalk::IsSpecialRunFrame in the VM to search for the new signature.
        [DynamicSecurityMethodAttribute()]
        [System.Security.SecurityCritical]  // auto-generated_required
        internal static void Run(ExecutionContext executionContext, ContextCallback callback,  Object state, bool ignoreSyncCtx) 
        {
            Contract.Assert(executionContext != s_dummyDefaultEC || executionContext.IsDefaultFTContext(ignoreSyncCtx)); 
            if (executionContext == null) 
            {
                throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_NullContext")); 
            }
            if (!executionContext.isNewCapture && executionContext != s_dummyDefaultEC)
            {
                throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_NotNewCaptureContext")); 
            }
 
            if (executionContext != s_dummyDefaultEC) 
                executionContext.isNewCapture = false;
 
            ExecutionContext ec = Thread.CurrentThread.GetExecutionContextNoCreate();
            if ( (ec == null || ec.IsDefaultFTContext(ignoreSyncCtx)) &&
#if FEATURE_IMPERSONATION || FEATURE_COMPRESSEDSTACK
                SecurityContext.CurrentlyInDefaultFTSecurityContext(ec) && 
#endif // #if FEATURE_IMPERSONATION || FEATURE_COMPRESSEDSTACK
                executionContext.IsDefaultFTContext(ignoreSyncCtx)) 
            { 
                callback(state);
            } 
            else
            {
                if (executionContext == s_dummyDefaultEC)
                    executionContext = s_dummyDefaultEC.CreateCopy(); 
                RunInternal(executionContext, callback, state);
            } 
        } 

        [System.Security.SecurityCritical]  // auto-generated 
        internal static void RunInternal(ExecutionContext executionContext, ContextCallback callback,  Object state)
        {
            if (cleanupCode == null)
            { 
                tryCode = new RuntimeHelpers.TryCode(runTryCode);
                cleanupCode = new RuntimeHelpers.CleanupCode(runFinallyCode); 
            } 

            ExecutionContextRunData runData = new ExecutionContextRunData(executionContext, callback, state); 
            RuntimeHelpers.ExecuteCodeWithGuaranteedCleanup(tryCode, cleanupCode, runData);
        }

        internal class ExecutionContextRunData 
        {
            internal ExecutionContext ec; 
            internal ContextCallback callBack; 
            internal Object state;
            internal ExecutionContextSwitcher ecsw; 
            internal ExecutionContextRunData(ExecutionContext executionContext, ContextCallback cb, Object state)
            {
                this.ec = executionContext;
                this.callBack = cb; 
                this.state = state;
                ecsw = new ExecutionContextSwitcher(); 
            } 
        }
 
        [System.Security.SecurityCritical]  // auto-generated
        static internal void runTryCode(Object userData)
        {
            ExecutionContextRunData rData = (ExecutionContextRunData) userData; 
            rData.ecsw = SetExecutionContext(rData.ec);
            rData.callBack(rData.state); 
 
        }
 
        [System.Security.SecurityCritical]  // auto-generated
        [PrePrepareMethod]
        static internal void runFinallyCode(Object userData, bool exceptionThrown)
        { 
            ExecutionContextRunData rData = (ExecutionContextRunData) userData;
            rData.ecsw.Undo(); 
        } 

        static internal RuntimeHelpers.TryCode tryCode; 
        static internal RuntimeHelpers.CleanupCode cleanupCode;


        // Sets the given execution context object on the thread. 
        // Returns the previous one.
        [System.Security.SecurityCritical]  // auto-generated 
        [DynamicSecurityMethodAttribute()] 
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable
#if FEATURE_CORRUPTING_EXCEPTIONS 
        [HandleProcessCorruptedStateExceptions] //
#endif // FEATURE_CORRUPTING_EXCEPTIONS
        internal  static ExecutionContextSwitcher SetExecutionContext(ExecutionContext executionContext)
        { 
            Contract.Assert(executionContext != s_dummyDefaultEC);
 
#if FEATURE_IMPERSONATION || FEATURE_COMPRESSEDSTACK 
            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller;
#endif // #if FEATURE_IMPERSONATION || FEATURE_COMPRESSEDSTACK 
            Contract.Assert(executionContext != null, "ExecutionContext cannot be null here.");


            // Set up the switcher object to return; 
            ExecutionContextSwitcher ecsw = new ExecutionContextSwitcher();
 
            ecsw.thread = Thread.CurrentThread; 
            ecsw.prevEC = Thread.CurrentThread.GetExecutionContextNoCreate(); // prev
            ecsw.currEC = executionContext; //current 

            // Update the EC on thread
            Thread.CurrentThread.SetExecutionContext(executionContext);
 
            RuntimeHelpers.PrepareConstrainedRegions();
            try 
            { 
                if (executionContext != null)
                { 
#if FEATURE_IMPERSONATION || FEATURE_COMPRESSEDSTACK
                    //set the security context
                    SecurityContext sc = executionContext.SecurityContext;
                    if (sc != null) 
                    {
                        // non-null SC: needs to be set 
                        SecurityContext prevSeC = (ecsw.prevEC != null) ? ecsw.prevEC.SecurityContext : null; 
                        ecsw.scsw = SecurityContext.SetSecurityContext(sc, prevSeC, ref stackMark);
                    } 
                    else if (!SecurityContext.CurrentlyInDefaultFTSecurityContext(ecsw.prevEC))
                    {
                        // null incoming SC, but we're currently not in FT: use static FTSC to set
                        SecurityContext prevSeC = (ecsw.prevEC != null) ? ecsw.prevEC.SecurityContext : null; 
                        ecsw.scsw = SecurityContext.SetSecurityContext(SecurityContext.FullTrustSecurityContext, prevSeC, ref stackMark);
                    } 
#endif // #if FEATURE_IMPERSONATION || FEATURE_COMPRESSEDSTACK 
#if FEATURE_SYNCHRONIZATIONCONTEXT
                    // set the [....] context 
                    SynchronizationContext syncContext = executionContext.SynchronizationContext;
                    if (syncContext != null)
                    {
                            SynchronizationContext prevSyC = (ecsw.prevEC != null) ? ecsw.prevEC.SynchronizationContext : null; 
                            ecsw.sysw = SynchronizationContext.SetSynchronizationContext(syncContext, prevSyC);
                    } 
#endif // #if FEATURE_SYNCHRONIZATIONCONTEXT 
#if FEATURE_CAS_POLICY
                    // set the Host Context 
                    HostExecutionContext hostContext = executionContext.HostExecutionContext;
                    if (hostContext != null)
                    {
                        ecsw.hecsw = HostExecutionContextManager.SetHostExecutionContextInternal(hostContext); 
                    }
#endif // FEATURE_CAS_POLICY 
 
                }
            } 
            catch
            {
                ecsw.UndoNoThrow();
                throw; 
            }
            return ecsw; 
        } 

        [System.Security.SecuritySafeCritical]  // auto-generated 
        public ExecutionContext CreateCopy()
        {
            if (!isNewCapture && this != s_dummyDefaultEC)
            { 
                throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_CannotCopyUsedContext"));
            } 
            ExecutionContext ec = new ExecutionContext(); 
            ec.isNewCapture = true;
#if FEATURE_SYNCHRONIZATIONCONTEXT 
            ec._syncContext = _syncContext == null?null:_syncContext.CreateCopy();
#endif // #if FEATURE_SYNCHRONIZATIONCONTEXT
#if FEATURE_CAS_POLICY
            // capture the host execution context 
            ec._hostExecutionContext = _hostExecutionContext == null ? null : _hostExecutionContext.CreateCopy();
#endif // FEATURE_CAS_POLICY 
#if FEATURE_IMPERSONATION || FEATURE_COMPRESSEDSTACK 
            if (_securityContext != null)
            { 
                ec._securityContext = _securityContext.CreateCopy();
                ec._securityContext.ExecutionContext = ec;
            }
#endif // #if FEATURE_IMPERSONATION || FEATURE_COMPRESSEDSTACK 
            if (this._logicalCallContext != null)
            { 
                LogicalCallContext lc = (LogicalCallContext)this.LogicalCallContext; 
                ec.LogicalCallContext = (LogicalCallContext)lc.Clone();
            } 
            if (this._illogicalCallContext != null)
            {
                IllogicalCallContext ilcc = (IllogicalCallContext)this.IllogicalCallContext;
                ec.IllogicalCallContext = (IllogicalCallContext)ilcc.Clone(); 
            }
 
            return ec; 
        }
 
        [System.Security.SecurityCritical]  // auto-generated_required
        public static AsyncFlowControl SuppressFlow()
        {
            if (IsFlowSuppressed()) 
            {
                throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_CannotSupressFlowMultipleTimes")); 
            } 
            Contract.EndContractBlock();
            AsyncFlowControl afc = new AsyncFlowControl(); 
            afc.Setup();
            return afc;
        }
 
        public static void RestoreFlow()
        { 
            ExecutionContext ec = Thread.CurrentThread.GetExecutionContextNoCreate(); 
            if (ec == null || !ec.isFlowSuppressed)
            { 
                throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_CannotRestoreUnsupressedFlow"));
            }
            ec.isFlowSuppressed = false;
        } 

        [Pure] 
        public static bool IsFlowSuppressed() 
        {
            ExecutionContext ec = Thread.CurrentThread.GetExecutionContextNoCreate(); 
            if (ec == null)
                return false;
            else
                return ec.isFlowSuppressed; 
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated 
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable
        public static ExecutionContext Capture() 
        {
            // set up a stack mark for finding the caller
            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller;
            return ExecutionContext.Capture(ref stackMark, CaptureOptions.None); 
        }
 
        [Flags] 
        internal enum CaptureOptions
        { 
            None = 0x00,

            IgnoreSyncCtx = 0x01,       //Don't flow SynchronizationContext
 
            OptimizeDefaultCase = 0x02, //Faster in the typical case, but can't show the result to users
                                        // because they could modify the shared default EC. 
                                        // Use this only if you won't be exposing the captured EC to users. 
        }
 
    // internal helper to capture the current execution context using a passed in stack mark
        [System.Security.SecurityCritical]  // auto-generated
        static internal ExecutionContext Capture(ref StackCrawlMark stackMark, CaptureOptions options)
        { 
            // check to see if Flow is suppressed
            if (IsFlowSuppressed()) 
                return null; 

            bool ignoreSyncCtx = 0 != (options & CaptureOptions.IgnoreSyncCtx); 
            bool optimizeDefaultCase = 0 != (options & CaptureOptions.OptimizeDefaultCase);

            //
            // Attempt to capture context.  There may be nothing to capture... 
            //
            ExecutionContext ecCurrent = Thread.CurrentThread.GetExecutionContextNoCreate(); 
 
#if FEATURE_IMPERSONATION || FEATURE_COMPRESSEDSTACK
            // capture the security context 
            SecurityContext secCtxNew = SecurityContext.Capture(ecCurrent, ref stackMark);
#endif // #if FEATURE_IMPERSONATION || FEATURE_COMPRESSEDSTACK
#if FEATURE_CAS_POLICY
             // capture the host execution context 
            HostExecutionContext hostCtxNew = HostExecutionContextManager.CaptureHostExecutionContext();    		
#endif // FEATURE_CAS_POLICY 
 
#if FEATURE_SYNCHRONIZATIONCONTEXT
            // capture the [....] context 
            SynchronizationContext syncCtxNew = null;
            if (ecCurrent != null && !ignoreSyncCtx)
                syncCtxNew = (ecCurrent._syncContext == null) ?null: ecCurrent._syncContext.CreateCopy();
#endif // #if FEATURE_SYNCHRONIZATIONCONTEXT 

            // copy over the Logical Call Context 
            LogicalCallContext logCtxNew = null; 
            if (ecCurrent != null && ecCurrent._logicalCallContext != null && ecCurrent.LogicalCallContext.HasInfo)
            { 
                logCtxNew = (LogicalCallContext)(ecCurrent.LogicalCallContext.Clone());
            }

            // 
            // If we didn't get anything but defaults, and we're allowed to return the
            // dummy default EC, don't bother allocating a new context. 
            // 
            if (optimizeDefaultCase &&
#if FEATURE_IMPERSONATION || FEATURE_COMPRESSEDSTACK 
                secCtxNew == null &&
#endif
#if FEATURE_CAS_POLICY
                hostCtxNew == null && 
#endif // FEATURE_CAS_POLICY
#if FEATURE_SYNCHRONIZATIONCONTEXT 
                syncCtxNew == null && 
#endif // #if FEATURE_SYNCHRONIZATIONCONTEXT
                (logCtxNew == null || !logCtxNew.HasInfo)) 
            {
                return s_dummyDefaultEC;
            }
 
            //
            // Allocate the new context, and fill it in. 
            // 
            ExecutionContext ecNew = new ExecutionContext();
#if FEATURE_IMPERSONATION || FEATURE_COMPRESSEDSTACK 
            ecNew.SecurityContext = secCtxNew;
            if (ecNew.SecurityContext != null)
                ecNew.SecurityContext.ExecutionContext = ecNew;
#endif 
#if FEATURE_CAS_POLICY
            ecNew._hostExecutionContext = hostCtxNew; 
#endif // FEATURE_CAS_POLICY 
#if FEATURE_SYNCHRONIZATIONCONTEXT
            ecNew._syncContext = syncCtxNew; 
#endif // #if FEATURE_SYNCHRONIZATIONCONTEXT
            ecNew.LogicalCallContext = logCtxNew;
            ecNew.isNewCapture = true;
 
            return ecNew;
        } 
 
        //
        // Implementation of ISerializable 
        //

        [System.Security.SecurityCritical]  // auto-generated_required
        public void GetObjectData(SerializationInfo info, StreamingContext context) 
        {
            if (info==null) 
                throw new ArgumentNullException("info"); 
            Contract.EndContractBlock();
 
            if (_logicalCallContext != null)
            {
                info.AddValue("LogicalCallContext", _logicalCallContext, typeof(LogicalCallContext));
            } 
        }
 
        [System.Security.SecurityCritical]  // auto-generated 
        private ExecutionContext(SerializationInfo info, StreamingContext context)
        { 
            SerializationInfoEnumerator e = info.GetEnumerator();
            while (e.MoveNext())
            {
                if (e.Name.Equals("LogicalCallContext")) 
                {
                    _logicalCallContext = (LogicalCallContext) e.Value; 
                } 
            }
            this.Thread = Thread.CurrentThread; 
        } // ObjRef .ctor


        [System.Security.SecurityCritical]  // auto-generated 
        internal bool IsDefaultFTContext(bool ignoreSyncCtx)
        { 
#if FEATURE_CAS_POLICY 
            if (_hostExecutionContext != null)
                return false; 
#endif // FEATURE_CAS_POLICY
#if FEATURE_SYNCHRONIZATIONCONTEXT
            if (!ignoreSyncCtx && _syncContext != null)
                return false; 
#endif // #if FEATURE_SYNCHRONIZATIONCONTEXT
#if FEATURE_IMPERSONATION || FEATURE_COMPRESSEDSTACK 
            if (_securityContext != null && !_securityContext.IsDefaultFTSecurityContext()) 
                return false;
#endif //#if FEATURE_IMPERSONATION || FEATURE_COMPRESSEDSTACK 
            if (_logicalCallContext != null && _logicalCallContext.HasInfo)
                return false;
            if (_illogicalCallContext != null && _illogicalCallContext.HasUserData)
                return false; 
            return true;
        } 
    } // class ExecutionContext 
}
 


