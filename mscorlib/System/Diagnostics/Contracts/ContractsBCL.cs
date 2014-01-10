// ==++== 
//
//   Copyright (c) Microsoft Corporation.  All rights reserved.
//
// ==--== 
/*============================================================
** 
** Class:  Contract 
**
** <OWNER>maf,mbarnett,[....]</OWNER> 
**
** Implementation details of CLR Contracts.
**
===========================================================*/ 
#define DEBUG // The behavior of this contract library should be consistent regardless of build type.
 
#if SILVERLIGHT 
#define FEATURE_UNTRUSTED_CALLERS
 
#elif REDHAWK_RUNTIME

#elif BARTOK_RUNTIME
 
#else // CLR
#define FEATURE_UNTRUSTED_CALLERS 
#define FEATURE_RELIABILITY_CONTRACTS 
#define FEATURE_SERIALIZATION
#endif 

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis; 

#if FEATURE_RELIABILITY_CONTRACTS 
using System.Runtime.ConstrainedExecution; 
#endif
#if FEATURE_UNTRUSTED_CALLERS 
using System.Security;
using System.Security.Permissions;
#endif
 
namespace System.Diagnostics.Contracts {
 
    public static partial class Contract 
    {
        #region Private Methods 

        private static bool _assertingMustUseRewriter;

        /// <summary> 
        /// This method is used internally to trigger a failure indicating to the "programmer" that he is using the interface incorrectly.
        /// It is NEVER used to indicate failure of actual contracts at runtime. 
        /// </summary> 
        static partial void AssertMustUseRewriter(ContractFailureKind kind, String contractKind)
        { 
            if (_assertingMustUseRewriter)
                System.Diagnostics.Assert.Fail("Asserting that we must use the rewriter went reentrant.", "Didn't rewrite this mscorlib?");
            _assertingMustUseRewriter = true;
 
            // @
            Internal.ContractHelper.TriggerFailure(kind, "Must use the rewriter when using Contract." + contractKind, null, null, null); 
 
            _assertingMustUseRewriter = false;
        } 

        #endregion Private Methods

        #region Failure Behavior 

        /// <summary> 
        /// Without contract rewriting, failing Assert/Assumes end up calling this method. 
        /// Code going through the contract rewriter never calls this method. Instead, the rewriter produced failures call
        /// Internal.ContractHelper.RaiseContractFailedEvent, followed by Internal.ContractHelper.TriggerFailure. 
        /// </summary>
        [SuppressMessage("Microsoft.Portability", "CA1903:UseOnlyApiFromTargetedFramework", MessageId = "System.Security.SecuritySafeCriticalAttribute")]
#if FEATURE_UNTRUSTED_CALLERS
        [SecuritySafeCritical] 
#endif
        [System.Diagnostics.DebuggerNonUserCode] 
#if FEATURE_RELIABILITY_CONTRACTS 
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
#endif 
        static partial void ReportFailure(ContractFailureKind failureKind, String userMessage, String conditionText, Exception innerException)
        {
            if (failureKind < ContractFailureKind.Precondition || failureKind > ContractFailureKind.Assume)
                throw new ArgumentException(Environment.GetResourceString("Arg_EnumIllegalVal", failureKind), "failureKind"); 
            Contract.EndContractBlock();
 
            // displayMessage == null means: yes we handled it. Otherwise it is the localized failure message 
            var displayMessage = Internal.ContractHelper.RaiseContractFailedEvent(failureKind, userMessage, conditionText, innerException);
 
            if (displayMessage == null) return;

            Internal.ContractHelper.TriggerFailure(failureKind, displayMessage, userMessage, conditionText, innerException);
        } 

        /// <summary> 
        /// Allows a managed application environment such as an interactive interpreter (IronPython) or a 
        /// web browser host (Jolt hosting Silverlight in IE) to be notified of contract failures and
        /// potentially "handle" them, either by throwing a particular exception type, etc.  If any of the 
        /// event handlers sets the Cancel flag in the ContractFailedEventArgs, then the Contract class will
        /// not pop up an assert dialog box or trigger escalation policy.  Hooking this event requires
        /// full trust.
        /// </summary> 
        public static event EventHandler<ContractFailedEventArgs> ContractFailed {
#if FEATURE_UNTRUSTED_CALLERS 
            [SecurityCritical] 
#if FEATURE_LINK_DEMAND
            [SecurityPermission(SecurityAction.LinkDemand, Unrestricted = true)] 
#endif
#endif
            add {
                Internal.ContractHelper.InternalContractFailed += value; 
            }
#if FEATURE_UNTRUSTED_CALLERS 
            [SecurityCritical] 
#if FEATURE_LINK_DEMAND
            [SecurityPermission(SecurityAction.LinkDemand, Unrestricted = true)] 
#endif
#endif
            remove {
                Internal.ContractHelper.InternalContractFailed -= value; 
            }
        } 
 
        #endregion FailureBehavior
    } 

    public sealed class ContractFailedEventArgs : EventArgs
    {
        private ContractFailureKind _failureKind; 
        private String _message;
        private String _condition; 
        private Exception _originalException; 
        private bool _handled;
        private bool _unwind; 

        internal Exception thrownDuringHandler;

#if FEATURE_RELIABILITY_CONTRACTS 
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
#endif 
        public ContractFailedEventArgs(ContractFailureKind failureKind, String message, String condition, Exception originalException) 
        {
            Contract.Requires(originalException == null || failureKind == ContractFailureKind.PostconditionOnException); 
            _failureKind = failureKind;
            _message = message;
            _condition = condition;
            _originalException = originalException; 
        }
 
        public String Message { get { return _message; } } 
        public String Condition { get { return _condition; } }
        public ContractFailureKind FailureKind { get { return _failureKind; } } 
        public Exception OriginalException { get { return _originalException; } }

        // Whether the event handler "handles" this contract failure, or to fail via escalation policy.
        public bool Handled { 
            get { return _handled; }
        } 
 
#if FEATURE_UNTRUSTED_CALLERS
        [SecurityCritical] 
#if FEATURE_LINK_DEMAND
        [SecurityPermission(SecurityAction.LinkDemand, Unrestricted = true)]
#endif
#endif 
        public void SetHandled()
        { 
            _handled = true; 
        }
 
        public bool Unwind {
            get { return _unwind; }
        }
 
#if FEATURE_UNTRUSTED_CALLERS
        [SecurityCritical] 
#if FEATURE_LINK_DEMAND 
        [SecurityPermission(SecurityAction.LinkDemand, Unrestricted = true)]
#endif 
#endif
        public void SetUnwind()
        {
            _unwind = true; 
        }
    } 
 
#if FEATURE_SERIALIZATION
    [Serializable] 
#else
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1032:ImplementStandardExceptionConstructors")]
#endif
    [SuppressMessage("Microsoft.Design", "CA1064:ExceptionsShouldBePublic")] 
    internal sealed class ContractException : Exception
    { 
        readonly ContractFailureKind _Kind; 
        readonly string _UserMessage;
        readonly string _Condition; 

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        public ContractFailureKind Kind { get { return _Kind; } }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")] 
        public string Failure { get { return this.Message; } }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")] 
        public string UserMessage { get { return _UserMessage; } } 
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        public string Condition { get { return _Condition; } } 

        public ContractException() { }

#if false 
        public ContractException(string msg) : base(msg)
        { 
            _Kind = ContractFailureKind.Precondition; 
        }
 
        public ContractException(string msg, Exception inner)
            : base(msg, inner)
        {
        } 
#endif
 
        public ContractException(ContractFailureKind kind, string failure, string userMessage, string condition, Exception innerException) 
            : base(failure, innerException)
        { 
            this._Kind = kind;
            this._UserMessage = userMessage;
            this._Condition = condition;
        } 

#if FEATURE_SERIALIZATION 
        private ContractException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) 
            : base(info, context)
        { 
            _Kind = (ContractFailureKind)info.GetInt32("Kind");
            _UserMessage = info.GetString("UserMessage");
            _Condition = info.GetString("Condition");
        } 
#endif // FEATURE_SERIALIZATION
 
        [System.Runtime.InteropServices.ComVisible(false)]  // Hack: This is not necessary, but the BCL's TrimSrc tool 
            // for createBclSmall needs ANY attribute to correctly parse this file.  #ifdefs are treated
            // as whitespace - they're occasionally preserved. 
#if FEATURE_UNTRUSTED_CALLERS && FEATURE_SERIALIZATION
        [SecurityCritical]
#if FEATURE_LINK_DEMAND && FEATURE_SERIALIZATION
        [SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.SerializationFormatter)] 
#endif // FEATURE_LINK_DEMAND
#endif // FEATURE_UNTRUSTED_CALLERS 
        public override void GetObjectData(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) 
        {
            base.GetObjectData(info, context); 

            info.AddValue("Kind", _Kind);
            info.AddValue("UserMessage", _UserMessage);
            info.AddValue("Condition", _Condition); 
        }
    } 
} 

 
namespace System.Diagnostics.Contracts.Internal
{
    public static partial class ContractHelper
    { 
        #region Private fields
 
        private static EventHandler<ContractFailedEventArgs> contractFailedEvent; 
        private static readonly Object lockObject = new Object();
 
        #endregion

        /// <summary>
        /// Allows a managed application environment such as an interactive interpreter (IronPython) or a 
        /// web browser host (Jolt hosting Silverlight in IE) to be notified of contract failures and
        /// potentially "handle" them, either by throwing a particular exception type, etc.  If any of the 
        /// event handlers sets the Cancel flag in the ContractFailedEventArgs, then the Contract class will 
        /// not pop up an assert dialog box or trigger escalation policy.  Hooking this event requires
        /// full trust. 
        /// </summary>
        internal static event EventHandler<ContractFailedEventArgs> InternalContractFailed
        {
#if FEATURE_UNTRUSTED_CALLERS 
            [SecurityCritical]
#endif 
            add { 
                // Eagerly prepare each event handler _marked with a reliability contract_, to
                // attempt to reduce out of memory exceptions while reporting contract violations. 
                // This only works if the new handler obeys the constraints placed on
                // constrained execution regions.  Eagerly preparing non-reliable event handlers
                // would be a perf hit and wouldn't significantly improve reliability.
                // UE: Please mention reliable event handlers should also be marked with the 
                // PrePrepareMethodAttribute to avoid CER eager preparation work when ngen'ed.
#if !FEATURE_CORECLR 
                System.Runtime.CompilerServices.RuntimeHelpers.PrepareContractedDelegate(value); 
#endif
                lock (lockObject) 
                {
                    contractFailedEvent += value;
                }
            } 
#if FEATURE_UNTRUSTED_CALLERS
            [SecurityCritical] 
#endif 
            remove {
                lock (lockObject) 
                {
                    contractFailedEvent -= value;
                }
            } 
        }
 
        /// <summary> 
        /// Rewriter will call this method on a contract failure to allow listeners to be notified.
        /// The method should not perform any failure (assert/throw) itself. 
        /// This method has 3 functions:
        /// 1. Call any contract hooks (such as listeners to Contract failed events)
        /// 2. Determine if the listeneres deem the failure as handled (then resultFailureMessage should be set to null)
        /// 3. Produce a localized resultFailureMessage used in advertising the failure subsequently. 
        /// </summary>
        /// <param name="resultFailureMessage">Should really be out (or the return value), but partial methods are not flexible enough. 
        /// On exit: null if the event was handled and should not trigger a failure. 
        ///          Otherwise, returns the localized failure message</param>
        [SuppressMessage("Microsoft.Design", "CA1030:UseEventsWhereAppropriate")] 
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        [System.Diagnostics.DebuggerNonUserCode]
#if FEATURE_RELIABILITY_CONTRACTS
        [SecuritySafeCritical] 
#endif
        static partial void RaiseContractFailedEventImplementation(ContractFailureKind failureKind, String userMessage, String conditionText, Exception innerException, ref string resultFailureMessage) 
        { 
            if (failureKind < ContractFailureKind.Precondition || failureKind > ContractFailureKind.Assume)
                throw new ArgumentException(Environment.GetResourceString("Arg_EnumIllegalVal", failureKind), "failureKind"); 
            Contract.EndContractBlock();

            String displayMessage = "contract failed.";  // Incomplete, but in case of OOM during resource lookup...
            ContractFailedEventArgs eventArgs = null;  // In case of OOM. 
            string returnValue;
#if FEATURE_RELIABILITY_CONTRACTS 
            System.Runtime.CompilerServices.RuntimeHelpers.PrepareConstrainedRegions(); 
#endif
            try 
            {
                displayMessage = GetDisplayMessage(failureKind, userMessage, conditionText);
                if (contractFailedEvent != null)
                { 
                    eventArgs = new ContractFailedEventArgs(failureKind, displayMessage, conditionText, innerException);
                    foreach (EventHandler<ContractFailedEventArgs> handler in contractFailedEvent.GetInvocationList()) 
                    { 
                        try
                        { 
                            handler(null, eventArgs);
                        }
                        catch (Exception e)
                        { 
                            eventArgs.thrownDuringHandler = e;
                            eventArgs.SetUnwind(); 
                        } 
                    }
                    if (eventArgs.Unwind) 
                    {
                        if (Environment.IsCLRHosted)
                            TriggerCodeContractEscalationPolicy(failureKind, displayMessage, conditionText, innerException);
 
                        // unwind
                        if (innerException == null) { innerException = eventArgs.thrownDuringHandler; } 
                        throw new ContractException(failureKind, displayMessage, userMessage, conditionText, innerException); 
                    }
                } 
            }
            finally
            {
                if (eventArgs != null && eventArgs.Handled) 
                {
                    returnValue = null; // handled 
                } 
                else
                { 
                    returnValue = displayMessage;
                }
            }
            resultFailureMessage = returnValue; 
        }
 
        /// <summary> 
        /// Rewriter calls this method to get the default failure behavior.
        /// </summary> 
        [SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "conditionText")]
        [SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "userMessage")]
        [SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "kind")]
        [SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "innerException")] 
        [System.Diagnostics.DebuggerNonUserCode]
#if FEATURE_UNTRUSTED_CALLERS 
        [SecuritySafeCritical] 
#endif
        static partial void TriggerFailureImplementation(ContractFailureKind kind, String displayMessage, String userMessage, String conditionText, Exception innerException) 
        {
            if (Environment.IsCLRHosted)
            {
                TriggerCodeContractEscalationPolicy(kind, displayMessage, conditionText, innerException); 
            }
#if !FEATURE_PAL 
            if (!Environment.UserInteractive) 
            {
                Environment.FailFast(displayMessage); 
            }
#endif
            System.Diagnostics.Assert.Check(false, conditionText, displayMessage);
        } 

#if FEATURE_RELIABILITY_CONTRACTS 
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)] 
#endif
        private static String GetDisplayMessage(ContractFailureKind failureKind, String userMessage, String conditionText) 
        {
            String resourceName = null;
            switch (failureKind)
            { 
                case ContractFailureKind.Assert:
                    resourceName = "AssertionFailed"; 
                    break; 

                case ContractFailureKind.Assume: 
                    resourceName = "AssumptionFailed";
                    break;

                case ContractFailureKind.Precondition: 
                    resourceName = "PreconditionFailed";
                    break; 
 
                case ContractFailureKind.Postcondition:
                    resourceName = "PostconditionFailed"; 
                    break;

                case ContractFailureKind.Invariant:
                    resourceName = "InvariantFailed"; 
                    break;
 
                case ContractFailureKind.PostconditionOnException: 
                    resourceName = "PostconditionOnExceptionFailed";
                    break; 

                default:
                    Contract.Assume(false, "Unreachable code");
                    resourceName = "AssumptionFailed"; 
                    break;
            } 
            // Well-formatted English messages will take one of four forms.  A sentence ending in 
            // either a period or a colon, the condition string, then the message tacked
            // on to the end with two spaces in front. 
            if (!String.IsNullOrEmpty(conditionText))
                resourceName += "_Cnd";
            String failureMessage = Environment.GetRuntimeResourceString(resourceName);
            // Now format based on presence of condition/userProvidedMessage 
            if (!String.IsNullOrEmpty(conditionText))
            { 
                if (!String.IsNullOrEmpty(userMessage)) 
                {
                    // both != null 
                    return String.Format(System.Globalization.CultureInfo.CurrentUICulture, failureMessage, conditionText) + "  " + userMessage;
                }
                else
                { 
                    // condition != null, userProvidedMessage == null
                    return String.Format(System.Globalization.CultureInfo.CurrentUICulture, failureMessage, conditionText); 
                } 
            }
            else 
            {
                if (!String.IsNullOrEmpty(userMessage))
                {
                    // condition null, userProvidedMessage != null 
                    return failureMessage + "  " + userMessage;
                } 
                else 
                {
                    // both null 
                    return failureMessage;
                }
            }
        } 

 
        // Will trigger escalation policy, if hosted.  Otherwise, exits the process. 
        // We must call through this method before calling the method on the Environment class
        // because our security team does not yet support SecuritySafeCritical on P/Invoke methods. 
        // Note this can be called in the context of throwing another exception (EnsuresOnThrow).
        [SecuritySafeCritical]
        [DebuggerNonUserCode]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)] 
        private static void TriggerCodeContractEscalationPolicy(ContractFailureKind failureKind, String message, String conditionText, Exception innerException)
        { 
            String exceptionAsString = null; 
            if (innerException != null)
                exceptionAsString = innerException.ToString(); 
            Environment.TriggerCodeContractFailure(failureKind, message, conditionText, exceptionAsString);
        }
    }
}  // namespace System.Diagnostics.Contracts.Internal 


