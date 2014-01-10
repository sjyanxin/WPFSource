// ==++== 
//
//   Copyright (c) Microsoft Corporation.  All rights reserved.
//
/*============================================================ 
**
** Class:  SecurityContext 
** 
** <OWNER>[....]</OWNER>
** 
**
** Purpose: Capture security  context for a thread
**
** 
===========================================================*/
namespace System.Security 
{ 
    using Microsoft.Win32;
    using Microsoft.Win32.SafeHandles; 
    using System.Threading;
    using System.Runtime.Remoting;
    using System.Security.Principal;
    using System.Collections; 
    using System.Runtime.Serialization;
    using System.Security.Permissions; 
    using System.Runtime.InteropServices; 
    using System.Runtime.CompilerServices;
#if FEATURE_CORRUPTING_EXCEPTIONS 
    using System.Runtime.ExceptionServices;
#endif // FEATURE_CORRUPTING_EXCEPTIONS
    using System.Runtime.ConstrainedExecution;
    using System.Runtime.Versioning; 
    using System.Diagnostics.Contracts;
 
    // This enum must be kept in [....] with the SecurityContextSource enum in the VM 
    public enum SecurityContextSource
    { 
        CurrentAppDomain = 0,
        CurrentAssembly
    }
 
    internal enum SecurityContextDisableFlow
    { 
        Nothing = 0, 
        WI = 0x1,
        All = 0x3FFF 
    }

#if !FEATURE_PAL && FEATURE_IMPERSONATION
    internal enum WindowsImpersonationFlowMode { 
    IMP_FASTFLOW = 0,
       IMP_NOFLOW = 1, 
       IMP_ALWAYSFLOW = 2, 
       IMP_DEFAULT = IMP_FASTFLOW
    } 
#endif

#if FEATURE_COMPRESSEDSTACK
    internal struct SecurityContextSwitcher: IDisposable 
    {
        internal SecurityContext prevSC; // prev SC that we restore on an Undo 
        internal SecurityContext currSC; //current SC  - SetSecurityContext that created the switcher set this on the Thread 
        internal ExecutionContext currEC; // current ExecutionContext on Thread
        internal CompressedStackSwitcher cssw; 
#if !FEATURE_PAL && FEATURE_IMPERSONATION
        internal WindowsImpersonationContext wic;
#endif
 
        public override bool Equals(Object obj)
        { 
            if (obj == null || !(obj is SecurityContextSwitcher)) 
                return false;
            SecurityContextSwitcher sw = (SecurityContextSwitcher)obj; 
#if !FEATURE_PAL && FEATURE_IMPERSONATION
            return (this.prevSC == sw.prevSC && this.currSC == sw.currSC && this.currEC == sw.currEC && this.cssw == sw.cssw && this.wic == sw.wic);
#else
            return (this.prevSC == sw.prevSC && this.currSC == sw.currSC && this.currEC == sw.currEC && this.cssw == sw.cssw); 
#endif
 
        } 

        public override int GetHashCode() 
        {
            return ToString().GetHashCode();
        }
 
        public static bool operator ==(SecurityContextSwitcher c1, SecurityContextSwitcher c2)
        { 
            return c1.Equals(c2); 
        }
 
        public static bool operator !=(SecurityContextSwitcher c1, SecurityContextSwitcher c2)
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
        [ResourceExposure(ResourceScope.None)] 
        [ResourceConsumption(ResourceScope.Process, ResourceScope.Process)]  // FailFast 
#if FEATURE_CORRUPTING_EXCEPTIONS
        [HandleProcessCorruptedStateExceptions] // 
#endif // FEATURE_CORRUPTING_EXCEPTIONS
        public void Undo()
        {
            if (currEC  == null) 
            {
                return; // mutiple Undo()s called on this switcher object 
            } 

            if (currEC != Thread.CurrentThread.GetExecutionContextNoCreate()) 
                System.Environment.FailFast(Environment.GetResourceString("InvalidOperation_SwitcherCtxMismatch"));

            Contract.Assert(currEC != null, " ExecutionContext can't be null");
            Contract.Assert(currSC != null, " SecurityContext can't be null"); 
            if ( currSC != currEC.SecurityContext) {
                System.Environment.FailFast(Environment.GetResourceString("InvalidOperation_SwitcherCtxMismatch")); 
            } 

            // restore the saved security context 
            currEC.SecurityContext = prevSC;
            currEC = null; // this will prevent the switcher object being used again

            bool bNoException = true; 
#if !FEATURE_PAL && FEATURE_IMPERSONATION
            try 
            { 
                if (wic != null)
                    bNoException &= wic.UndoNoThrow(); 
            }
            catch
            {
                // Failfast since we can't continue safely... 
                bNoException &= cssw.UndoNoThrow();
                System.Environment.FailFast(Environment.GetResourceString("ExecutionContext_UndoFailed")); 
 
            }
#endif 
            bNoException &= cssw.UndoNoThrow();


            if (!bNoException) 
            {
                // Failfast since we can't continue safely... 
                System.Environment.FailFast(Environment.GetResourceString("ExecutionContext_UndoFailed")); 
            }
 
        }
    }

 
    public sealed class SecurityContext : IDisposable
    { 
#if !FEATURE_PAL && FEATURE_IMPERSONATION 
        // Note that only one of the following variables will be true. The way we set up the flow mode in the g_pConfig guarantees this.
        static bool _LegacyImpersonationPolicy = (GetImpersonationFlowMode() == WindowsImpersonationFlowMode.IMP_NOFLOW); 
        static bool _alwaysFlowImpersonationPolicy = (GetImpersonationFlowMode() == WindowsImpersonationFlowMode.IMP_ALWAYSFLOW);
#endif
        /*=========================================================================
        ** Data accessed from managed code that needs to be defined in 
        ** SecurityContextObject  to maintain alignment between the two classes.
        ** DON'T CHANGE THESE UNLESS YOU MODIFY SecurityContextObject in vm\object.h 
        =========================================================================*/ 

        private ExecutionContext            _executionContext; 
#if !FEATURE_PAL && FEATURE_IMPERSONATION
        private WindowsIdentity             _windowsIdentity;
#endif
        private CompressedStack          _compressedStack; 
        static private SecurityContext _fullTrustSC;
 
        internal bool isNewCapture = false; 
        internal SecurityContextDisableFlow _disableFlow = SecurityContextDisableFlow.Nothing;
 
        [System.Security.SecuritySafeCritical] // static constructors should be safe to call
        static SecurityContext()
        {
        } 

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)] 
        internal SecurityContext() 
        {
        } 

        static internal SecurityContext FullTrustSecurityContext
        {
            [System.Security.SecurityCritical]  // auto-generated 
            get
            { 
                if (_fullTrustSC == null) 
                    _fullTrustSC = CreateFullTrustSecurityContext();
                return _fullTrustSC; 
            }
        }

        // link the security context to an ExecutionContext 
        internal ExecutionContext ExecutionContext
        { 
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)] 
            set
            { 
                _executionContext = value;
            }
        }
 
#if !FEATURE_PAL && FEATURE_IMPERSONATION
 
 
        internal WindowsIdentity WindowsIdentity
        { 
            get
            {
                return _windowsIdentity;
            } 
            set
            { 
                // Note, we do not dispose of the existing windows identity, since some code such as remoting 
                // relies on reusing that identity.  If you are not going to reuse the existing identity, then
                // you should dispose of the existing identity before resetting it. 
                    _windowsIdentity = value;
            }
        }
#endif // !FEATURE_PAL && FEATURE_IMPERSONATION 

 
        internal CompressedStack CompressedStack 
        {
            get 
            {
                return _compressedStack;
            }
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)] 
            set
            { 
                _compressedStack =  value; 
            }
        } 

        [System.Security.SecuritySafeCritical]  // auto-generated
        public void Dispose()
        { 
#if !FEATURE_PAL && FEATURE_IMPERSONATION
            if (_windowsIdentity != null) 
                _windowsIdentity.Dispose(); 
#endif // !FEATURE_PAL
        } 

        [System.Security.SecurityCritical]  // auto-generated_required
        public static AsyncFlowControl SuppressFlow()
        { 
            return SuppressFlow(SecurityContextDisableFlow.All);
        } 
 
        [System.Security.SecurityCritical]  // auto-generated_required
        public static AsyncFlowControl SuppressFlowWindowsIdentity() 
        {
            return SuppressFlow(SecurityContextDisableFlow.WI);
        }
 
        internal static AsyncFlowControl SuppressFlow(SecurityContextDisableFlow flags)
        { 
            if (IsFlowSuppressed(flags)) 
            {
                throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_CannotSupressFlowMultipleTimes")); 
            }

            if (Thread.CurrentThread.ExecutionContext.SecurityContext == null)
                Thread.CurrentThread.ExecutionContext.SecurityContext = new SecurityContext(); 
            AsyncFlowControl afc = new AsyncFlowControl();
            afc.Setup(flags); 
            return afc; 
        }
 
        public static void RestoreFlow()
        {
            SecurityContext sc = GetCurrentSecurityContextNoCreate();
            if (sc == null || sc._disableFlow == SecurityContextDisableFlow.Nothing) 
            {
                throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_CannotRestoreUnsupressedFlow")); 
            } 
            sc._disableFlow = SecurityContextDisableFlow.Nothing;
        } 

        public static bool IsFlowSuppressed()
        {
            return SecurityContext.IsFlowSuppressed(SecurityContextDisableFlow.All); 
        }
#if !FEATURE_PAL && FEATURE_IMPERSONATION 
        public static bool IsWindowsIdentityFlowSuppressed() 
        {
            return (_LegacyImpersonationPolicy|| SecurityContext.IsFlowSuppressed(SecurityContextDisableFlow.WI)); 
        }
#endif
        internal static bool IsFlowSuppressed(SecurityContextDisableFlow flags)
        { 
                SecurityContext sc = GetCurrentSecurityContextNoCreate();
                return (sc == null) ? false : ((sc._disableFlow & flags) == flags); 
        } 

        // This method is special from a security perspective - the VM will not allow a stack walk to 
        // continue past the call to SecurityContext.Run.  If you change the signature to this method, or
        // provide an alternate way to do a SecurityContext.Run make sure to update
        // SecurityStackWalk::IsSpecialRunFrame in the VM to search for the new method.
        [System.Security.SecurityCritical]  // auto-generated_required 
        [DynamicSecurityMethodAttribute()]
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable 
        public static void Run(SecurityContext securityContext, ContextCallback callback, Object state) 
        {
            if (securityContext == null ) 
            {
                throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_NullContext"));
            }
            Contract.EndContractBlock(); 

            StackCrawlMark stackMark = StackCrawlMark.LookForMe; 
 
            if (!securityContext.isNewCapture)
            { 
                throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_NotNewCaptureContext"));
            }

            securityContext.isNewCapture = false; 

            ExecutionContext ec = Thread.CurrentThread.GetExecutionContextNoCreate(); 
 
            // Optimization: do the callback directly if both the current and target contexts are equal to the
            // default full-trust security context 
            if ( SecurityContext.CurrentlyInDefaultFTSecurityContext(ec)
                && securityContext.IsDefaultFTSecurityContext())
            {
                callback(state); 

                if (GetCurrentWI(Thread.CurrentThread.GetExecutionContextNoCreate()) != null) 
                { 
                    // If we enter here it means the callback did an impersonation
                    // that we need to revert. 
                    // We don't need to revert any other security state since it is stack-based
                    // and automatically goes away when the callback returns.
                    WindowsIdentity.SafeRevertToSelf(ref stackMark);
                    // Ensure we have reverted to the state we entered in. 
                    Contract.Assert(GetCurrentWI(Thread.CurrentThread.GetExecutionContextNoCreate()) == null);
                } 
            } 
            else
            { 
                RunInternal(securityContext, callback, state);
            }

        } 
        [System.Security.SecurityCritical]  // auto-generated
        internal static void RunInternal(SecurityContext securityContext, ContextCallback callBack, Object state) 
        { 
            if (cleanupCode == null)
            { 
                tryCode = new RuntimeHelpers.TryCode(runTryCode);
                cleanupCode = new RuntimeHelpers.CleanupCode(runFinallyCode);
            }
            SecurityContextRunData runData = new SecurityContextRunData(securityContext, callBack, state); 
            RuntimeHelpers.ExecuteCodeWithGuaranteedCleanup(tryCode, cleanupCode, runData);
 
        } 

        internal class SecurityContextRunData 
        {
            internal SecurityContext sc;
            internal ContextCallback callBack;
            internal Object state; 
            internal SecurityContextSwitcher scsw;
            internal SecurityContextRunData(SecurityContext securityContext, ContextCallback cb, Object state) 
            { 
                this.sc = securityContext;
                this.callBack = cb; 
                this.state = state;
                this.scsw = new SecurityContextSwitcher();
            }
        } 

        [System.Security.SecurityCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.Process)] 
        [ResourceConsumption(ResourceScope.Process)]
        static internal void runTryCode(Object userData) 
        {
            SecurityContextRunData rData = (SecurityContextRunData) userData;
            rData.scsw = SetSecurityContext(rData.sc, Thread.CurrentThread.ExecutionContext.SecurityContext);
            rData.callBack(rData.state); 

        } 
 
        [System.Security.SecurityCritical]  // auto-generated
        [PrePrepareMethod] 
        static internal void runFinallyCode(Object userData, bool exceptionThrown)
        {
            SecurityContextRunData rData = (SecurityContextRunData) userData;
            rData.scsw.Undo(); 
        }
 
        static internal RuntimeHelpers.TryCode tryCode; 
        static internal RuntimeHelpers.CleanupCode cleanupCode;
 


        // Internal API that gets called from public SetSecurityContext and from SetExecutionContext
        [System.Security.SecurityCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.Process)]
        [ResourceConsumption(ResourceScope.Process)] 
        [DynamicSecurityMethodAttribute()] 
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable
        internal static SecurityContextSwitcher SetSecurityContext(SecurityContext sc, SecurityContext prevSecurityContext) 
        {
            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller;
            return SetSecurityContext(sc, prevSecurityContext, ref stackMark);
        } 

        [System.Security.SecurityCritical]  // auto-generated 
#if FEATURE_CORRUPTING_EXCEPTIONS 
        [HandleProcessCorruptedStateExceptions] //
#endif // FEATURE_CORRUPTING_EXCEPTIONS 
        internal static SecurityContextSwitcher SetSecurityContext(SecurityContext sc, SecurityContext prevSecurityContext, ref StackCrawlMark stackMark)
        {

 
            // Save the flow state at capture and reset it in the SC.
            SecurityContextDisableFlow _capturedFlowState = sc._disableFlow; 
            sc._disableFlow = SecurityContextDisableFlow.Nothing; 

            //Set up the switcher object 
            SecurityContextSwitcher scsw = new SecurityContextSwitcher();
            scsw.currSC = sc;
            // save the current Execution Context
            ExecutionContext currEC = Thread.CurrentThread.ExecutionContext; 
            scsw.currEC = currEC;
            // save the prev security context 
            scsw.prevSC = prevSecurityContext; 

            // update the current security context to the new security context 
            currEC.SecurityContext = sc;


            if (sc != null) 
            {
                RuntimeHelpers.PrepareConstrainedRegions(); 
                try 
                {
#if !FEATURE_PAL && FEATURE_IMPERSONATION 
                    scsw.wic = null;
                    if (!_LegacyImpersonationPolicy)
                    {
                        if (sc.WindowsIdentity != null) 
                        {
                            scsw.wic = sc.WindowsIdentity.Impersonate(ref stackMark); 
                        } 
                        else if ( ((_capturedFlowState & SecurityContextDisableFlow.WI) == 0)
                            && prevSecurityContext != null && prevSecurityContext.WindowsIdentity != null) 
                        {
                            // revert impersonation if there was no WI flow supression at capture and we're currently impersonating
                            scsw.wic = WindowsIdentity.SafeRevertToSelf(ref stackMark);
                        } 
                    }
 
 
#endif
                    scsw.cssw = CompressedStack.SetCompressedStack(sc.CompressedStack, 
                                                                   (prevSecurityContext!=null?prevSecurityContext.CompressedStack:null));
                }
                catch
                { 
                    scsw.UndoNoThrow();
                    throw; 
                } 
            }
            return scsw; 
        }

        /// <internalonly/>
        [System.Security.SecuritySafeCritical]  // auto-generated 
        public SecurityContext CreateCopy()
        { 
            if (!isNewCapture) 
            {
                throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_NotNewCaptureContext")); 
            }

            SecurityContext sc = new SecurityContext();
            sc.isNewCapture = true; 
            sc._disableFlow = _disableFlow;
 
#if !FEATURE_PAL && FEATURE_IMPERSONATION 
            if (WindowsIdentity != null)
                sc._windowsIdentity = new WindowsIdentity(WindowsIdentity.TokenHandle); 
#endif //!FEATURE_PAL && FEATURE_IMPERSONATION

            if (_compressedStack != null)
                sc._compressedStack = _compressedStack.CreateCopy(); 

            return sc; 
        } 

        [System.Security.SecuritySafeCritical]  // auto-generated 
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable
        public static SecurityContext Capture( )
        {
            // check to see if Flow is suppressed 
            if (IsFlowSuppressed())
                return null; 
 
            StackCrawlMark stackMark= StackCrawlMark.LookForMyCaller;
            SecurityContext sc = SecurityContext.Capture(Thread.CurrentThread.GetExecutionContextNoCreate(), ref stackMark); 
            if (sc == null)
                sc = CreateFullTrustSecurityContext();
            return sc;
         } 

        // create a clone from a non-existing SecurityContext 
        [System.Security.SecurityCritical]  // auto-generated 
        static internal SecurityContext Capture(ExecutionContext currThreadEC, ref StackCrawlMark stackMark)
        { 
          // check to see if Flow is suppressed
        if (IsFlowSuppressed()) return null;

        // If we're in FT right now, return null 
        if (CurrentlyInDefaultFTSecurityContext(currThreadEC))
            return null; 
 
        SecurityContext sc = new SecurityContext();
        sc.isNewCapture = true; 

#if !FEATURE_PAL && FEATURE_IMPERSONATION
            // Force create WindowsIdentity
        if (!IsWindowsIdentityFlowSuppressed()) 
        {
            WindowsIdentity currentIdentity = GetCurrentWI(currThreadEC); 
            if (currentIdentity != null) 
                sc._windowsIdentity = new WindowsIdentity(currentIdentity.TokenHandle);
        } 
        else
        {
            sc._disableFlow = SecurityContextDisableFlow.WI;
        } 
#endif // !FEATURE_PAL && FEATURE_IMPERSONATION
 
        // Force create CompressedStack 
        sc.CompressedStack = CompressedStack.GetCompressedStack(ref stackMark);
        return sc; 
    }
    [System.Security.SecurityCritical]  // auto-generated
    static internal SecurityContext CreateFullTrustSecurityContext()
    { 
        SecurityContext sc = new SecurityContext();
        sc.isNewCapture = true; 
 
#if !FEATURE_PAL && FEATURE_IMPERSONATION
        if (IsWindowsIdentityFlowSuppressed()) 
        {
            sc._disableFlow = SecurityContextDisableFlow.WI;
        }
#endif // !FEATURE_PAL && FEATURE_IMPERSONATION 

 
        // Force create CompressedStack 
        sc.CompressedStack = new CompressedStack(null);
        return sc; 
    }
    // Check to see if we have a security context and return if we do
    static internal SecurityContext GetCurrentSecurityContextNoCreate()
    { 
        ExecutionContext ec = Thread.CurrentThread.GetExecutionContextNoCreate();
        return  (ec == null) ? null : ec.SecurityContext; 
    } 
#if !FEATURE_PAL && FEATURE_IMPERSONATION
    // Check to see if we have a WI on the thread and return if we do 
    [System.Security.SecurityCritical]  // auto-generated
    [ResourceExposure(ResourceScope.None)]
    [ResourceConsumption(ResourceScope.Process, ResourceScope.Process)]
    static internal WindowsIdentity GetCurrentWI(ExecutionContext threadEC) 
    {
            if (_alwaysFlowImpersonationPolicy) 
            { 
                // Examine the threadtoken at the cost of a kernel call if the user has set the IMP_ALWAYSFLOW mode
                return WindowsIdentity.GetCurrentInternal(TokenAccessLevels.MaximumAllowed, true); 
            }

            SecurityContext sc = (threadEC == null) ? null : threadEC.SecurityContext;
            return (sc == null) ? null : sc.WindowsIdentity; 
    }
 
    [System.Security.SecurityCritical]  // auto-generated 
    internal bool IsDefaultFTSecurityContext()
    { 
        return (WindowsIdentity == null && (CompressedStack == null || CompressedStack.CompressedStackHandle == null));
    }
    [System.Security.SecurityCritical]  // auto-generated
    static internal bool CurrentlyInDefaultFTSecurityContext(ExecutionContext threadEC) 
    {
        return (IsDefaultThreadSecurityInfo() && GetCurrentWI(threadEC) == null); 
    } 
#else
 
        internal bool IsDefaultFTSecurityContext()
        {
            return (CompressedStack == null || CompressedStack.CompressedStackHandle == null);
        } 
        static internal bool CurrentlyInDefaultFTSecurityContext(ExecutionContext threadEC)
        { 
            return (IsDefaultThreadSecurityInfo()); 
        }
#endif 
#if !FEATURE_PAL && FEATURE_IMPERSONATION
        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall), ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)] 
        internal extern static WindowsImpersonationFlowMode GetImpersonationFlowMode();
#endif 
        [System.Security.SecurityCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall), ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)] 
        internal extern static bool IsDefaultThreadSecurityInfo();

    }
#endif // FEATURE_COMPRESSEDSTACK 
}

