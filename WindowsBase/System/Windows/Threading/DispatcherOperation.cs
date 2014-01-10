using System; 
using System.Security;
using System.Security.Permissions;
using System.Threading;
using MS.Internal.WindowsBase; 

namespace System.Windows.Threading 
{ 
    /// <summary>
    ///     DispatcherOperation represents a delegate that has been 
    ///     posted to the Dispatcher queue.
    /// </summary>
    public sealed class DispatcherOperation
    { 
        /// <SecurityNote>
        ///    Critical: This code calls into InvokeInSecurityContext which can be used to process input 
        ///    TreatAsSafe: This is ok to expose since the operation itself is ok . But this will 
        ///                 `blow up if something untowards is done in the operation that violates trust boundaries
        ///                 .Essentially although this lets you execute arbitrary code that code cannot be doing 
        ///                 any untrusted operation
        /// </SecurityNote>
        [SecurityCritical,SecurityTreatAsSafe]
        static DispatcherOperation() 
        {
            _invokeInSecurityContext = new ContextCallback(InvokeInSecurityContext); 
        } 

        /// <SecurityNote> 
        ///     Critical: accesses _executionContext
        /// </SecurityNote>
        [SecurityCritical]
        internal DispatcherOperation( 
            Dispatcher dispatcher,
            Delegate method, 
            DispatcherPriority priority, // NOTE: should be Priority 
            object args,
            int numArgs) 
        {
            _dispatcher = dispatcher;
            _method = method;
            _priority = priority; 
            _numArgs = numArgs;
            _args = args; 
 
            _executionContext = ExecutionContext.Capture();
        } 


        /// <summary>
        /// Internal constructor used to generate a Dispatcher operation 
        /// when the dispatcher has shut down
        /// </summary> 
        /// <param name="dispatcher"></param> 
        /// <param name="method"></param>
        /// <param name="priority"></param> 
        internal DispatcherOperation(
            Dispatcher dispatcher,
            Delegate method,
            DispatcherPriority priority 
            )
        { 
            _dispatcher = dispatcher; 
            _method = method;
            _priority = priority; 
            _status = DispatcherOperationStatus.Aborted;
        }

        /// <summary> 
        ///     Returns the Dispatcher that this operation was posted to.
        /// </summary> 
        public Dispatcher Dispatcher 
        {
            get 
            {
                return _dispatcher;
            }
        } 

        /// <summary> 
        ///     Gets or sets the priority of this operation within the 
        ///     Dispatcher queue.
        /// </summary> 
        public DispatcherPriority Priority // NOTE: should be Priority
        {
            get
            { 
                return _priority;
            } 
 
            set
            { 
                Dispatcher.ValidatePriority(value, "value");

                if(value != _priority && _dispatcher.SetPriority(this, value))
                { 
                    _priority = value;
                } 
            } 
        }
 
        /// <summary>
        ///     The status of this operation.
        /// </summary>
        public DispatcherOperationStatus Status 
        {
            get 
            { 
                return _status;
            } 
        }

        /// <summary>
        ///     Waits for this operation to complete. 
        /// </summary>
        /// <returns> 
        ///     The status of the operation.  To obtain the return value 
        ///     of the invoked delegate, use the the Result property.
        /// </returns> 
        public DispatcherOperationStatus Wait()
        {
            return Wait(TimeSpan.FromMilliseconds(-1));
        } 

        /// <summary> 
        ///     Waits for this operation to complete. 
        /// </summary>
        /// <param name="timeout"> 
        ///     The maximum amount of time to wait.
        /// </param>
        /// <returns>
        ///     The status of the operation.  To obtain the return value 
        ///     of the invoked delegate, use the the Result property.
        /// </returns> 
        /// <SecurityNote> 
        ///    Critical: This code calls into PushFrame which has a link demand
        ///    PublicOk: The act of setting a timeline for an operation to complete is a safe one 
        /// </SecurityNote>
        [SecurityCritical]
        public DispatcherOperationStatus Wait(TimeSpan timeout)
        { 
            if((_status == DispatcherOperationStatus.Pending || _status == DispatcherOperationStatus.Executing) &&
                timeout.TotalMilliseconds != 0) 
            { 
                if(_dispatcher.Thread == Thread.CurrentThread)
                { 
                    if(_status == DispatcherOperationStatus.Executing)
                    {
                        // We are the dispatching thread, and the current operation state is
                        // executing, which means that the operation is in the middle of 
                        // executing (on this thread) and is trying to wait for the execution
                        // to complete.  Unfortunately, the thread will now deadlock, so 
                        // we throw an exception instead. 
                        throw new InvalidOperationException(SR.Get(SRID.ThreadMayNotWaitOnOperationsAlreadyExecutingOnTheSameThread));
                    } 

                    // We are the dispatching thread for this operation, so
                    // we can't block.  We will push a frame instead.
                    DispatcherOperationFrame frame = new DispatcherOperationFrame(this, timeout); 
                    Dispatcher.PushFrame(frame);
                } 
                else 
                {
                    // We are some external thread, so we can just block.  Of 
                    // course this means that the Dispatcher (queue)for this
                    // thread (if any) is now blocked.  The COM STA model
                    // suggests that we should pump certain messages so that
                    // back-communication can happen.  Underneath us, the CLR 
                    // will pump the STA apartment for us, and we will allow
                    // the UI thread for a context to call 
                    // Invoke(Priority.Max, ...) without going through the 
                    // blocked queue.
                    DispatcherOperationEvent wait = new DispatcherOperationEvent(this, timeout); 
                    wait.WaitOne();
                }
            }
 
            return _status;
        } 
 
        /// <summary>
        ///     Aborts this operation. 
        /// </summary>
        /// <returns>
        ///     False if the operation could not be aborted (because the
        ///     operation was already in  progress) 
        /// </returns>
        public bool Abort() 
        { 
            bool removed = false;
 
            if (_dispatcher != null)
            {
                removed = _dispatcher.Abort(this);
 
                if (removed)
                { 
                    // Raise the Aborted so anyone who is waiting will wake up. 
                    EventHandler aborted = _aborted;
                    if (aborted != null) 
                    {
                        aborted(this, EventArgs.Empty);
                    }
                } 
            }
 
            return removed; 
        }
 
        /// <summary>
        ///     Name of this operation.
        /// </summary>
        /// <returns> 
        ///     Returns a string representation of the operation to be invoked.
        /// </returns> 
        internal String Name 
        {
            get 
            {
                return _method.Method.DeclaringType + "." + _method.Method.Name;
            }
        } 

        /// <summary> 
        ///     Returns the result of the operation if it has completed. 
        /// </summary>
        public object Result 
        {
            get
            {
                return _result; 
            }
        } 
 
        /// <summary>
        ///     An event that is raised when the operation is aborted. 
        /// </summary>
        public event EventHandler Aborted
        {
            add 
            {
                lock (DispatcherLock) 
                { 
                    _aborted = (EventHandler) Delegate.Combine(_aborted, value);
                } 
            }

            remove
            { 
                lock(DispatcherLock)
                { 
                    _aborted = (EventHandler) Delegate.Remove(_aborted, value); 
                }
            } 
        }

        /// <summary>
        ///     An event that is raised when the operation completes. 
        /// </summary>
        public event EventHandler Completed 
        { 
            add
            { 
                lock (DispatcherLock)
                {
                    _completed = (EventHandler) Delegate.Combine(_completed, value);
                } 
            }
 
            remove 
            {
                lock(DispatcherLock) 
                {
                    _completed = (EventHandler) Delegate.Remove(_completed, value);
                }
            } 
        }
 
        // Note: this is called by the Dispatcher to actually invoke the operation. 
        // Invoke --> InvokeInSecurityContext --> InvokeImpl
        /// <SecurityNote> 
        ///    Critical: This code calls into ExecutionContext.Run which is link demand protected
        ///              accesses _executionContext
        /// </SecurityNote>
        [SecurityCritical] 
        internal object Invoke()
        { 
            // Mark this operation as executing. 
            _status = DispatcherOperationStatus.Executing;
 
            // Continue using the execution context that was active when the operation
            // was begun.
            if(_executionContext != null)
            { 
                ExecutionContext.Run(_executionContext, _invokeInSecurityContext, this);
            } 
            else 
            {
                // _executionContext can be null if someone called 
                // ExecutionContext.SupressFlow before calling BeginInvoke/Invoke.
                // In this case we'll just call the invokation directly.
                // SupressFlow is a privileged operation, so this is not a
                // security hole. 
                _invokeInSecurityContext(this);
            } 
 
            // This block of code needs to be synchronized with the constructor of
            // DispatcherOperationEvent which may be called through 
            // Dispatcher.Invoke on a separate thread.

            EventHandler completed;
            lock(DispatcherLock) 
            {
                // Mark this operation as completed. 
                _status = DispatcherOperationStatus.Completed; 

                // Read the Completed event handler. 
                completed = _completed;
            }

            // Raise the Completed so anyone who is waiting will wake up. 
            if(completed != null)
            { 
                completed(this, EventArgs.Empty); 
            }
 
            return _result;
        }

        // Invoke --> InvokeInSecurityContext --> InvokeImpl 
        /// <SecurityNote>
        ///     Critical: This code can execute arbitrary code 
        /// </SecurityNote> 
        [SecurityCritical]
        private static void InvokeInSecurityContext(Object state) 
        {
            DispatcherOperation operation = (DispatcherOperation) state;
            operation.InvokeImpl();
        } 

        // Invoke --> InvokeInSecurityContext --> InvokeImpl 
        /// <SecurityNote> 
        ///     Critical: This code calls into SynchronizationContext.SetSynchronizationContext which link demands
        /// </SecurityNote> 
        [SecurityCritical]
        private void InvokeImpl()
        {
            SynchronizationContext oldSynchronizationContext = SynchronizationContext.Current; 
            bool setSynchronizationContext = oldSynchronizationContext != _dispatcher._dispatcherSynchronizationContext;
 
            try 
            {
                // We are executing under the "foreign" execution context, but the 
                // SynchronizationContext must be for the correct dispatcher.
                if(setSynchronizationContext)
                {
                    SynchronizationContext.SetSynchronizationContext(_dispatcher._dispatcherSynchronizationContext); 
                }
 
                // Invoke the delegate that does the work for this operation. 
                _result = _dispatcher.WrappedInvoke(_method, _args, _numArgs);
            } 
            finally
            {
                if(setSynchronizationContext)
                { 
                    SynchronizationContext.SetSynchronizationContext(oldSynchronizationContext);
                } 
            } 
        }
 
        private class DispatcherOperationFrame : DispatcherFrame
        {
            // Note: we pass "exitWhenRequested=false" to the base
            // DispatcherFrame construsctor because we do not want to exit 
            // this frame if the dispatcher is shutting down. This is
            // because we may need to invoke operations during the shutdown process. 
            public DispatcherOperationFrame(DispatcherOperation op, TimeSpan timeout) : base(false) 
            {
                _operation = op; 

                // We will exit this frame once the operation is completed or aborted.
                _operation.Aborted += new EventHandler(OnCompletedOrAborted);
                _operation.Completed += new EventHandler(OnCompletedOrAborted); 

                // We will exit the frame if the operation is not completed within 
                // the requested timeout. 
                if(timeout.TotalMilliseconds > 0)
                { 
                    _waitTimer = new Timer(new TimerCallback(OnTimeout),
                                           null,
                                           timeout,
                                           TimeSpan.FromMilliseconds(-1)); 
                }
 
                // Some other thread could have aborted the operation while we were 
                // setting up the handlers.  We check the state again and mark the
                // frame as "should not continue" if this happened. 
                if(_operation._status != DispatcherOperationStatus.Pending)
                {
                    Exit();
                } 

            } 
 
            private void OnCompletedOrAborted(object sender, EventArgs e)
            { 
                Exit();
            }

            private void OnTimeout(object arg) 
            {
                Exit(); 
            } 

            private void Exit() 
            {
                Continue = false;

                if(_waitTimer != null) 
                {
                    _waitTimer.Dispose(); 
                } 

                _operation.Aborted -= new EventHandler(OnCompletedOrAborted); 
                _operation.Completed -= new EventHandler(OnCompletedOrAborted);
            }

            private DispatcherOperation _operation; 
            private Timer _waitTimer;
        } 
 
        private class DispatcherOperationEvent
        { 
            public DispatcherOperationEvent(DispatcherOperation op, TimeSpan timeout)
            {
                _operation = op;
                _timeout = timeout; 
                _event = new ManualResetEvent(false);
                _eventClosed = false; 
 
                lock(DispatcherLock)
                { 
                    // We will set our event once the operation is completed or aborted.
                    _operation.Aborted += new EventHandler(OnCompletedOrAborted);
                    _operation.Completed += new EventHandler(OnCompletedOrAborted);
 
                    // Since some other thread is dispatching this operation, it could
                    // have been dispatched while we were setting up the handlers. 
                    // We check the state again and set the event ourselves if this 
                    // happened.
                    if(_operation._status != DispatcherOperationStatus.Pending && _operation._status != DispatcherOperationStatus.Executing) 
                    {
                        _event.Set();
                    }
                } 
            }
 
            private void OnCompletedOrAborted(object sender, EventArgs e) 
            {
                lock(DispatcherLock) 
                {
                    if(!_eventClosed)
                    {
                        _event.Set(); 
                    }
                } 
            } 

            public void WaitOne() 
            {
                _event.WaitOne(_timeout, false);

                lock(DispatcherLock) 
                {
                    if(!_eventClosed) 
                    { 
                        // Cleanup the events.
                        _operation.Aborted -= new EventHandler(OnCompletedOrAborted); 
                        _operation.Completed -= new EventHandler(OnCompletedOrAborted);

                        // Close the event immediately instead of waiting for a GC
                        // because the Dispatcher is a a high-activity component and 
                        // we could run out of events.
                        _event.Close(); 
 
                        _eventClosed = true;
                    } 
                }
            }

            private object DispatcherLock 
            {
                get { return _operation.DispatcherLock; } 
            } 

            private DispatcherOperation _operation; 
            private TimeSpan _timeout;
            private ManualResetEvent _event;
            private bool _eventClosed;
        } 

        private object DispatcherLock 
        { 
            get { return _dispatcher._instanceLock; }
        } 

        /// <SecurityNote>
        ///     Obtained under an elevation.
        /// </SecurityNote> 
        [SecurityCritical]
        private ExecutionContext _executionContext; 
        private static ContextCallback _invokeInSecurityContext; 

        private Dispatcher _dispatcher; 
        private DispatcherPriority _priority;
        private Delegate _method;
        private object _args;
        private int _numArgs; 

        internal DispatcherOperationStatus _status; // set from Dispatcher 
        private object _result; 

        internal PriorityItem<DispatcherOperation> _item; // The Dispatcher sets this when it enques/deques the item. 

        EventHandler _aborted;
        EventHandler _completed;
    } 

    /// <summary> 
    ///     A convenient delegate to use for dispatcher operations. 
    /// </summary>
    public delegate object DispatcherOperationCallback(object arg); 
}


