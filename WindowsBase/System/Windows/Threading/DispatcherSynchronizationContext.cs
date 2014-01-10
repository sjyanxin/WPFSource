using System; 
using System.Threading;
using System.Diagnostics;
using System.ComponentModel;
using System.Security;                       // CAS 
using System.Security.Permissions;           // Registry permissions
 
namespace System.Windows.Threading 
{
    /// <summary> 
    ///     SynchronizationContext subclass used by the Dispatcher.
    /// </summary>
    public sealed class DispatcherSynchronizationContext : SynchronizationContext
    { 
        /// <summary>
        ///     Constructs a new instance of the DispatcherSynchroniazationContext 
        ///     using the current Dispatcher. 
        /// </summary>
        public DispatcherSynchronizationContext() : this(Dispatcher.CurrentDispatcher) 
        {
        }

        /// <summary> 
        ///     Constructs a new instance of the DispatcherSynchroniazationContext
        ///     using the specified Dispatcher. 
        /// </summary> 
        public DispatcherSynchronizationContext(Dispatcher dispatcher)
        { 
            if(dispatcher == null)
            {
                throw new ArgumentNullException("dispatcher");
            } 

            _dispatcher = dispatcher; 
 
            // Tell the CLR to call us when blocking.
            SetWaitNotificationRequired(); 
        }

        /// <summary>
        ///     Synchronously invoke the callback in the SynchronizationContext. 
        /// </summary>
        public override void Send(SendOrPostCallback d, Object state) 
        { 
            _dispatcher.Invoke(DispatcherPriority.Normal, d, state);
        } 

        /// <summary>
        ///     Asynchronously invoke the callback in the SynchronizationContext.
        /// </summary> 
        public override void Post(SendOrPostCallback d, Object state)
        { 
            _dispatcher.BeginInvoke(DispatcherPriority.Normal, d, state); 
        }
 
        /// <summary>
        ///     Wait for a set of handles.
        /// </summary>
        /// <SecurityNote> 
        ///     Critical - Calls WaitForMultipleObjectsEx which has a SUC.
        /// </SecurityNote> 
        [SecurityCritical] 
        [SecurityPermissionAttribute(SecurityAction.LinkDemand, Flags=SecurityPermissionFlag.ControlPolicy|SecurityPermissionFlag.ControlEvidence)]
        public override int Wait(IntPtr[] waitHandles, bool waitAll, int millisecondsTimeout) 
        {
            if(_dispatcher._disableProcessingCount > 0)
            {
                // Call into native code directly in order to avoid the default 
                // CLR locking behavior which pumps messages under contention.
                // Even though they try to pump only the COM messages, any 
                // messages that have been SENT to the window are also 
                // dispatched.  This can lead to unpredictable reentrancy.
                return MS.Win32.UnsafeNativeMethods.WaitForMultipleObjectsEx(waitHandles.Length, waitHandles, waitAll, millisecondsTimeout, false); 
            }
            else
            {
                return SynchronizationContext.WaitHelper(waitHandles, waitAll, millisecondsTimeout); 
            }
        } 
 
        /// <summary>
        ///     Create a copy of this SynchronizationContext. 
        /// </summary>
        public override SynchronizationContext CreateCopy()
        {
            // Because we do not contain any state that we want to preserve 
            // in seperate instances, we just return the same synchronization
            // context.  The CLR team assures us this is OK. 
            return this; 
        }
 
        internal Dispatcher _dispatcher;
    }
}
 

