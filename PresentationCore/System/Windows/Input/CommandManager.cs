//---------------------------------------------------------------------------- 
//
//    Copyright (C) Microsoft Corporation.  All rights reserved.
//
//--------------------------------------------------------------------------- 

using System; 
using System.Collections; 
using System.Collections.Generic;
using System.Collections.Specialized; 
using System.Diagnostics;
using System.Security;
using System.Security.Permissions;
using System.Threading; 
using System.Windows;
using System.Windows.Input; 
using System.Windows.Threading; 
using MS.Internal;
 
using SR=MS.Internal.PresentationCore.SR;
using SRID=MS.Internal.PresentationCore.SRID;

namespace System.Windows.Input 
{
    /// <summary> 
    ///     The CommandManager class defines the Commanding Execute/CanExecute Events 
    ///     and also its RoutedEventHandlers which delegates them to corresponding
    ///     ComamndBinding.Execute/CanExecute EventHandlers. 
    /// </summary>
    public sealed class CommandManager
    {
        #region Static Features 

        #region Public Events 
 
        /// <summary>
        ///     Raised when CanExecute should be requeried on commands. 
        ///     Since this event is static, it will only hold onto the handler as a weak reference.
        ///     Users of this event should keep a strong reference to their event handler to avoid
        ///     it being garbage collected. This can be accomplished by having a private field
        ///     and assigning the handler as the value before or after attaching to this event. 
        /// </summary>
        public static event EventHandler RequerySuggested 
        { 
            add { CommandManager.AddWeakReferenceHandler(ref CommandManager.Current._requerySuggestedHandlers, value); }
            remove { CommandManager.RemoveWeakReferenceHandler(CommandManager.Current._requerySuggestedHandlers, value); } 
        }

        /// <summary>
        ///     Event before a command is executed 
        /// </summary>
        public static readonly RoutedEvent PreviewExecutedEvent = 
               EventManager.RegisterRoutedEvent("PreviewExecuted", 
                                                RoutingStrategy.Tunnel,
                                                typeof(ExecutedRoutedEventHandler), 
                                                typeof(CommandManager));

        /// <summary>
        ///     Attaches the handler on the element. 
        /// </summary>
        /// <param name="element">The element on which to attach the handler.</param> 
        /// <param name="handler">The handler to attach.</param> 
        public static void AddPreviewExecutedHandler(UIElement element, ExecutedRoutedEventHandler handler)
        { 
            if (element == null)
            {
                throw new ArgumentNullException("element");
            } 
            if (handler == null)
            { 
                throw new ArgumentNullException("handler"); 
            }
 
            element.AddHandler(PreviewExecutedEvent, handler);
        }

        /// <summary> 
        ///     Removes the handler from the element.
        /// </summary> 
        /// <param name="element">The element from which to remove the handler.</param> 
        /// <param name="handler">The handler to remove.</param>
        public static void RemovePreviewExecutedHandler(UIElement element, ExecutedRoutedEventHandler handler) 
        {
            if (element == null)
            {
                throw new ArgumentNullException("element"); 
            }
            if (handler == null) 
            { 
                throw new ArgumentNullException("handler");
            } 

            element.RemoveHandler(PreviewExecutedEvent, handler);
        }
 
        /// <summary>
        ///     Event to execute a command 
        /// </summary> 
        public static readonly RoutedEvent ExecutedEvent =
                EventManager.RegisterRoutedEvent("Executed", 
                                                 RoutingStrategy.Bubble,
                                                 typeof(ExecutedRoutedEventHandler),
                                                 typeof(CommandManager));
 
        /// <summary>
        ///     Attaches the handler on the element. 
        /// </summary> 
        /// <param name="element">The element on which to attach the handler.</param>
        /// <param name="handler">The handler to attach.</param> 
        public static void AddExecutedHandler(UIElement element, ExecutedRoutedEventHandler handler)
        {
            if (element == null)
            { 
                throw new ArgumentNullException("element");
            } 
            if (handler == null) 
            {
                throw new ArgumentNullException("handler"); 
            }

            element.AddHandler(ExecutedEvent, handler);
        } 

        /// <summary> 
        ///     Removes the handler from the element. 
        /// </summary>
        /// <param name="element">The element from which to remove the handler.</param> 
        /// <param name="handler">The handler to remove.</param>
        public static void RemoveExecutedHandler(UIElement element, ExecutedRoutedEventHandler handler)
        {
            if (element == null) 
            {
                throw new ArgumentNullException("element"); 
            } 
            if (handler == null)
            { 
                throw new ArgumentNullException("handler");
            }

            element.RemoveHandler(ExecutedEvent, handler); 
        }
 
        /// <summary> 
        ///     Event to determine if a command can be executed
        /// </summary> 
        public static readonly RoutedEvent PreviewCanExecuteEvent =
                EventManager.RegisterRoutedEvent("PreviewCanExecute",
                                                 RoutingStrategy.Tunnel,
                                                 typeof(CanExecuteRoutedEventHandler), 
                                                 typeof(CommandManager));
 
        /// <summary> 
        ///     Attaches the handler on the element.
        /// </summary> 
        /// <param name="element">The element on which to attach the handler.</param>
        /// <param name="handler">The handler to attach.</param>
        public static void AddPreviewCanExecuteHandler(UIElement element, CanExecuteRoutedEventHandler handler)
        { 
            if (element == null)
            { 
                throw new ArgumentNullException("element"); 
            }
            if (handler == null) 
            {
                throw new ArgumentNullException("handler");
            }
 
            element.AddHandler(PreviewCanExecuteEvent, handler);
        } 
 
        /// <summary>
        ///     Removes the handler from the element. 
        /// </summary>
        /// <param name="element">The element from which to remove the handler.</param>
        /// <param name="handler">The handler to remove.</param>
        public static void RemovePreviewCanExecuteHandler(UIElement element, CanExecuteRoutedEventHandler handler) 
        {
            if (element == null) 
            { 
                throw new ArgumentNullException("element");
            } 
            if (handler == null)
            {
                throw new ArgumentNullException("handler");
            } 

            element.RemoveHandler(PreviewCanExecuteEvent, handler); 
        } 

        /// <summary> 
        ///     Event to determine if a command can be executed
        /// </summary>
        public static readonly RoutedEvent CanExecuteEvent =
                EventManager.RegisterRoutedEvent("CanExecute", 
                                                 RoutingStrategy.Bubble,
                                                 typeof(CanExecuteRoutedEventHandler), 
                                                 typeof(CommandManager)); 

        /// <summary> 
        ///     Attaches the handler on the element.
        /// </summary>
        /// <param name="element">The element on which to attach the handler.</param>
        /// <param name="handler">The handler to attach.</param> 
        public static void AddCanExecuteHandler(UIElement element, CanExecuteRoutedEventHandler handler)
        { 
            if (element == null) 
            {
                throw new ArgumentNullException("element"); 
            }
            if (handler == null)
            {
                throw new ArgumentNullException("handler"); 
            }
 
            element.AddHandler(CanExecuteEvent, handler); 
        }
 
        /// <summary>
        ///     Removes the handler from the element.
        /// </summary>
        /// <param name="element">The element from which to remove the handler.</param> 
        /// <param name="handler">The handler to remove.</param>
        public static void RemoveCanExecuteHandler(UIElement element, CanExecuteRoutedEventHandler handler) 
        { 
            if (element == null)
            { 
                throw new ArgumentNullException("element");
            }
            if (handler == null)
            { 
                throw new ArgumentNullException("handler");
            } 
 
            element.RemoveHandler(CanExecuteEvent, handler);
        } 

        #endregion

        #region Public Methods 

        /// <summary> 
        ///     Register class level InputBindings. 
        /// </summary>
        /// <param name="type">Owner type</param> 
        /// <param name="inputBinding">InputBinding to register</param>
        public static void RegisterClassInputBinding(Type type, InputBinding inputBinding)
        {
            if (type == null) 
            {
                throw new ArgumentNullException("type"); 
            } 
            if (inputBinding == null)
            { 
                throw new ArgumentNullException("inputBinding");
            }

            lock (_classInputBindings.SyncRoot) 
            {
                InputBindingCollection inputBindings = _classInputBindings[type] as InputBindingCollection; 
 
                if (inputBindings == null)
                { 
                    inputBindings = new InputBindingCollection();
                    _classInputBindings[type] = inputBindings;
                }
 
                inputBindings.Add(inputBinding);
 
                if (!inputBinding.IsFrozen) 
                {
                    inputBinding.Freeze(); 
                }
            }
        }
 
        /// <summary>
        ///     Register class level CommandBindings. 
        /// </summary> 
        /// <param name="type">Owner type</param>
        /// <param name="commandBinding">CommandBinding to register</param> 
        public static void RegisterClassCommandBinding(Type type, CommandBinding commandBinding)
        {
            if (type == null)
            { 
                throw new ArgumentNullException("type");
            } 
            if (commandBinding == null) 
            {
                throw new ArgumentNullException("commandBinding"); 
            }

            lock (_classCommandBindings.SyncRoot)
            { 
                CommandBindingCollection bindings = _classCommandBindings[type] as CommandBindingCollection;
 
                if (bindings == null) 
                {
                    bindings = new CommandBindingCollection(); 
                    _classCommandBindings[type] = bindings;
                }

                bindings.Add(commandBinding); 
            }
        } 
 
        /// <summary>
        ///     Invokes RequerySuggested listeners registered on the current thread. 
        /// </summary>
        public static void InvalidateRequerySuggested()
        {
            CommandManager.Current.RaiseRequerySuggested(); 
        }
 
        #endregion 

        #region Implementation 

        /// <summary>
        ///     Return the CommandManager associated with the current thread.
        /// </summary> 
        private static CommandManager Current
        { 
            get 
            {
                if (_commandManager == null) 
                {
                    _commandManager = new CommandManager();
                }
 
                return _commandManager;
            } 
        } 

        /// <summary> 
        ///     Scans input and command bindings for matching gestures and executes the appropriate command
        /// </summary>
        /// <remarks>
        ///     Scans for command to execute in the following order: 
        ///     - input bindings associated with the targetElement instance
        ///     - input bindings associated with the targetElement class 
        ///     - command bindings associated with the targetElement instance 
        ///     - command bindings associated with the targetElement class
        /// </remarks> 
        /// <param name="targetElement">UIElement/ContentElement to be scanned for input and command bindings</param>
        /// <param name="inputEventArgs">InputEventArgs to be matched against for gestures</param>
        /// <SecurityNote>
        ///     Critical: This code can be used to spoof input and cause elevations for Userinitiated paste 
        /// </SecurityNote>
        [SecurityCritical] 
        internal static void TranslateInput(IInputElement targetElement, InputEventArgs inputEventArgs) 
        {
            if ((targetElement == null) || (inputEventArgs == null)) 
            {
                return;
            }
 
            ICommand command = null;
            IInputElement target = null; 
            object parameter = null; 

            // Determine UIElement/ContentElement/Neither type 
            DependencyObject targetElementAsDO = targetElement as DependencyObject;
            bool isUIElement = InputElement.IsUIElement(targetElementAsDO);
            bool isContentElement = !isUIElement && InputElement.IsContentElement(targetElementAsDO);
            bool isUIElement3D = !isUIElement && !isContentElement && InputElement.IsUIElement3D(targetElementAsDO); 

            // Step 1: Check local input bindings 
            InputBindingCollection localInputBindings = null; 
            if (isUIElement)
            { 
                localInputBindings = ((UIElement)targetElement).InputBindingsInternal;
            }
            else if (isContentElement)
            { 
                localInputBindings = ((ContentElement)targetElement).InputBindingsInternal;
            } 
            else if (isUIElement3D) 
            {
                localInputBindings = ((UIElement3D)targetElement).InputBindingsInternal; 
            }
            if (localInputBindings != null)
            {
                InputBinding inputBinding = localInputBindings.FindMatch(targetElement, inputEventArgs); 
                if (inputBinding != null)
                { 
                    command = inputBinding.Command; 
                    target = inputBinding.CommandTarget;
                    parameter = inputBinding.CommandParameter; 
                }
            }

            // Step 2: If no command, check class input bindings 
            if (command == null)
            { 
                lock (_classInputBindings.SyncRoot) 
                {
                    Type classType = targetElement.GetType(); 
                    while (classType != null)
                    {
                        InputBindingCollection classInputBindings = _classInputBindings[classType] as InputBindingCollection;
                        if (classInputBindings != null) 
                        {
                            InputBinding inputBinding = classInputBindings.FindMatch(targetElement, inputEventArgs); 
                            if (inputBinding != null) 
                            {
                                command = inputBinding.Command; 
                                target = inputBinding.CommandTarget;
                                parameter = inputBinding.CommandParameter;
                                break;
                            } 
                        }
                        classType = classType.BaseType; 
                    } 
                }
            } 

            // Step 3: If no command, check local command bindings
            if (command == null)
            { 
                // Check for the instance level ones Next
                CommandBindingCollection localCommandBindings = null; 
                if (isUIElement) 
                {
                    localCommandBindings = ((UIElement)targetElement).CommandBindingsInternal; 
                }
                else if (isContentElement)
                {
                    localCommandBindings = ((ContentElement)targetElement).CommandBindingsInternal; 
                }
                else if (isUIElement3D) 
                { 
                    localCommandBindings = ((UIElement3D)targetElement).CommandBindingsInternal;
                } 
                if (localCommandBindings != null)
                {
                    command = localCommandBindings.FindMatch(targetElement, inputEventArgs);
                } 
            }
 
            // Step 4: If no command, look at class command bindings 
            if (command == null)
            { 
                lock (_classCommandBindings.SyncRoot)
                {
                    Type classType = targetElement.GetType();
                    while (classType != null) 
                    {
                        CommandBindingCollection classCommandBindings = _classCommandBindings[classType] as CommandBindingCollection; 
                        if (classCommandBindings != null) 
                        {
                            command = classCommandBindings.FindMatch(targetElement, inputEventArgs); 
                            if (command != null)
                            {
                                break;
                            } 
                        }
                        classType = classType.BaseType; 
                    } 
                }
            } 

            // Step 5: If found a command, then execute it (unless it is
            // the special "NotACommand" command, which we simply ignore without
            // setting Handled=true, so that the input bubbles up to the parent) 
            if (command != null && command != ApplicationCommands.NotACommand)
            { 
                // We currently do not support declaring the element with focus as the target 
                // element by setting target == null.  Instead, we interpret a null target to indicate
                // the element that we are routing the event through, e.g. the targetElement parameter. 
                if (target == null)
                {
                    target = targetElement;
                } 

                bool continueRouting = false; 
 
                RoutedCommand routedCommand = command as RoutedCommand;
                if (routedCommand != null) 
                {
                    if (routedCommand.CriticalCanExecute(parameter,
                                                    target,
                                                    inputEventArgs.UserInitiated /*trusted*/, 
                                                    out continueRouting))
                    { 
                        ExecuteCommand(routedCommand, parameter, target, inputEventArgs); 
                    }
                } 
                else
                {
                    if (command.CanExecute(parameter))
                    { 
                        command.Execute(parameter);
                    } 
                } 

                // If we mapped an input event to a command, we should always 
                // handle the input event - regardless of whether the command
                // was executed or not.  Unless the CanExecute handler told us
                // to continue the route.
                inputEventArgs.Handled = !continueRouting; 
            }
        } 
 
        /// <SecurityNote>
        ///     Critical - accesses critical information (determining if command is driven from user input) 
        ///     TreatAsSafe - Does so correctly (only looking at protected information, does not modify)
        /// </SecurityNote>
        [SecurityCritical, SecurityTreatAsSafe]
        private static bool ExecuteCommand(RoutedCommand routedCommand, object parameter, IInputElement target, InputEventArgs inputEventArgs) 
        {
            return routedCommand.ExecuteCore(parameter, target, inputEventArgs.UserInitiated); 
        } 

        /// <summary> 
        ///     Forwards CanExecute events to CommandBindings.
        /// </summary>
        internal static void OnCanExecute(object sender, CanExecuteRoutedEventArgs e)
        { 
            if ((sender != null) && (e != null) && (e.Command != null))
            { 
                FindCommandBinding(sender, e, e.Command, false); 

                if (!e.Handled && (e.RoutedEvent == CanExecuteEvent)) 
                {
                    DependencyObject d = sender as DependencyObject;
                    if (d != null)
                    { 
                        if (FocusManager.GetIsFocusScope(d))
                        { 
                            // This element is a focus scope. 
                            // Try to transfer the event to its parent focus scope's focused element.
                            IInputElement focusedElement = GetParentScopeFocusedElement(d); 
                            if (focusedElement != null)
                            {
                                TransferEvent(focusedElement, e);
                            } 
                        }
                    } 
                } 
            }
        } 

        private static bool CanExecuteCommandBinding(object sender, CanExecuteRoutedEventArgs e, CommandBinding commandBinding)
        {
            commandBinding.OnCanExecute(sender, e); 
            return e.CanExecute || e.Handled;
        } 
 
        /// <summary>
        ///     Forwards Executed events to CommandBindings. 
        /// </summary>
        internal static void OnExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            if ((sender != null) && (e != null) && (e.Command != null)) 
            {
                FindCommandBinding(sender, e, e.Command, true); 
 
                if (!e.Handled && (e.RoutedEvent == ExecutedEvent))
                { 
                    DependencyObject d = sender as DependencyObject;
                    if (d != null)
                    {
                        if (FocusManager.GetIsFocusScope(d)) 
                        {
                            // This element is a focus scope. 
                            // Try to transfer the event to its parent focus scope's focused element. 
                            IInputElement focusedElement = GetParentScopeFocusedElement(d);
                            if (focusedElement != null) 
                            {
                                TransferEvent(focusedElement, e);
                            }
                        } 
                    }
                } 
            } 
        }
 
        /// <SecurityNote>
        ///     Critical - creates critical information (determining if command is driven from user input)
        ///     TreatAsSafe - Does so correctly (only looking at protected information)
        /// </SecurityNote> 
        [SecurityCritical, SecurityTreatAsSafe]
        private static bool ExecuteCommandBinding(object sender, ExecutedRoutedEventArgs e, CommandBinding commandBinding) 
        { 
            // Asserting a permission in the case that the command was user initiated
            // and the command is a secure command. We can do this safely because at 
            // the time the binding was setup, we demanded the permission.
            ISecureCommand secureCommand = e.Command as ISecureCommand;
            bool elevate = e.UserInitiated && (secureCommand != null) && (secureCommand.UserInitiatedPermission != null);
 
            if (elevate)
            { 
                secureCommand.UserInitiatedPermission.Assert(); //BlessedAssert 
            }
            try 
            {
                commandBinding.OnExecuted(sender, e);
            }
            finally 
            {
                if (elevate) 
                { 
                    CodeAccessPermission.RevertAssert();
                } 
            }

            return e.Handled;
        } 

        internal static void OnCommandDevice(object sender, CommandDeviceEventArgs e) 
        { 
            if ((sender != null) && (e != null) && (e.Command != null))
            { 
                CanExecuteRoutedEventArgs canExecuteArgs = new CanExecuteRoutedEventArgs(e.Command, null /* parameter */);
                canExecuteArgs.RoutedEvent = CommandManager.CanExecuteEvent;
                canExecuteArgs.Source = sender;
                OnCanExecute(sender, canExecuteArgs); 

                if (canExecuteArgs.CanExecute) 
                { 
                    ExecutedRoutedEventArgs executedArgs = new ExecutedRoutedEventArgs(e.Command, null /* parameter */);
                    executedArgs.RoutedEvent = CommandManager.ExecutedEvent; 
                    executedArgs.Source = sender;
                    OnExecuted(sender, executedArgs);

                    if (executedArgs.Handled) 
                    {
                        e.Handled = true; 
                    } 
                }
            } 
        }

        private static void FindCommandBinding(object sender, RoutedEventArgs e, ICommand command, bool execute)
        { 
            // Check local command bindings
            CommandBindingCollection commandBindings = null; 
            DependencyObject senderAsDO = sender as DependencyObject; 
            if (InputElement.IsUIElement(senderAsDO))
            { 
                commandBindings = ((UIElement)senderAsDO).CommandBindingsInternal;
            }
            else if (InputElement.IsContentElement(senderAsDO))
            { 
                commandBindings = ((ContentElement)senderAsDO).CommandBindingsInternal;
            } 
            else if (InputElement.IsUIElement3D(senderAsDO)) 
            {
                commandBindings = ((UIElement3D)senderAsDO).CommandBindingsInternal; 
            }
            if (commandBindings != null)
            {
                FindCommandBinding(commandBindings, sender, e, command, execute); 
            }
 
            // If no command binding is found, check class command bindings 
            lock (_classCommandBindings.SyncRoot)
            { 
                // Check from the current type to all the base types
                Type classType = sender.GetType();
                while (classType != null)
                { 
                    CommandBindingCollection classCommandBindings = _classCommandBindings[classType] as CommandBindingCollection;
                    if (classCommandBindings != null) 
                    { 
                        FindCommandBinding(classCommandBindings, sender, e, command, execute);
                    } 
                    classType = classType.BaseType;
                }
            }
        } 

        private static void FindCommandBinding(CommandBindingCollection commandBindings, object sender, RoutedEventArgs e, ICommand command, bool execute) 
        { 
            int index = 0;
            while (true) 
            {
                CommandBinding commandBinding = commandBindings.FindMatch(command, ref index);
                if ((commandBinding == null) ||
                    (execute && ExecuteCommandBinding(sender, (ExecutedRoutedEventArgs)e, commandBinding)) || 
                    (!execute && CanExecuteCommandBinding(sender, (CanExecuteRoutedEventArgs)e, commandBinding)))
                { 
                    break; 
                }
            } 
        }

        private static void TransferEvent(IInputElement newSource, CanExecuteRoutedEventArgs e)
        { 
            RoutedCommand command = e.Command as RoutedCommand;
            if (command != null) 
            { 
                try
                { 
                    e.CanExecute = command.CanExecute(e.Parameter, newSource);
                }
                finally
                { 
                    e.Handled = true;
                } 
            } 
        }
 
        /// <SecurityNote>
        ///     Critical - creates critical information (determining if command is driven from user input)
        ///     TreatAsSafe - Does so correctly (only looking at protected information)
        /// </SecurityNote> 
        [SecurityCritical, SecurityTreatAsSafe]
        private static void TransferEvent(IInputElement newSource, ExecutedRoutedEventArgs e) 
        { 
            RoutedCommand command = e.Command as RoutedCommand;
            if (command != null) 
            {
                try
                {
                    // SecurityCritical: Must not modify UserInitiated 
                    command.ExecuteCore(e.Parameter, newSource, e.UserInitiated);
                } 
                finally 
                {
                    e.Handled = true; 
                }
            }
        }
 
        private static IInputElement GetParentScopeFocusedElement(DependencyObject childScope)
        { 
            DependencyObject parentScope = GetParentScope(childScope); 
            if (parentScope != null)
            { 
                IInputElement focusedElement = FocusManager.GetFocusedElement(parentScope);
                if ((focusedElement != null) && !ContainsElement(childScope, focusedElement as DependencyObject))
                {
                    // The focused element from the parent focus scope is not within the focus scope 
                    // of the current element.
                    return focusedElement; 
                } 
            }
 
            return null;
        }

        private static DependencyObject GetParentScope(DependencyObject childScope) 
        {
            // Get the parent element of the childScope element 
            DependencyObject parent = null; 
            UIElement element = childScope as UIElement;
            ContentElement contentElement = (element == null) ? childScope as ContentElement : null; 
            UIElement3D element3D = (element == null && contentElement == null) ? childScope as UIElement3D : null;

            if (element != null)
            { 
                parent = element.GetUIParent(true);
            } 
            else if (contentElement != null) 
            {
                parent = contentElement.GetUIParent(true); 
            }
            else if (element3D != null)
            {
                parent = element3D.GetUIParent(true); 
            }
 
            if (parent != null) 
            {
                // Get the next focus scope above this one 
                return FocusManager.GetFocusScope(parent);
            }

            return null; 
        }
 
        private static bool ContainsElement(DependencyObject scope, DependencyObject child) 
        {
            if (child != null) 
            {
                DependencyObject parentScope = FocusManager.GetFocusScope(child);
                while (parentScope != null)
                { 
                    if (parentScope == scope)
                    { 
                        // A parent scope matches the scope we are looking for 
                        return true;
                    } 
                    parentScope = GetParentScope(parentScope);
                }
            }
 
            return false;
        } 
 
        #endregion
 
        #region Data

        // The CommandManager associated with the current thread
        [ThreadStatic] 
        private static CommandManager _commandManager;
 
        // This is a Hashtable of CommandBindingCollections keyed on OwnerType 
        // Each ItemList holds the registered Class level CommandBindings for that OwnerType
        private static HybridDictionary _classCommandBindings = new HybridDictionary(); 

        // This is a Hashtable of InputBindingCollections keyed on OwnerType
        // Each Item holds the registered Class level CommandBindings for that OwnerType
        private static HybridDictionary _classInputBindings = new HybridDictionary(); 

        #endregion 
 
        #endregion
 
        #region Instance Features

        #region Constructor
 
        ///<summary>
        ///     Creates a new instance of this class. 
        ///</summary> 
        private CommandManager()
        { 
        }

        #endregion
 
        #region Implementation
 
        /// <summary> 
        ///     Adds an idle priority dispatcher operation to raise RequerySuggested.
        /// </summary> 
        private void RaiseRequerySuggested()
        {
            if (_requerySuggestedOperation == null)
            { 
                Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
                if ((dispatcher != null) && !dispatcher.HasShutdownStarted && !dispatcher.HasShutdownFinished) 
                { 
                    _requerySuggestedOperation = dispatcher.BeginInvoke(DispatcherPriority.Background, new DispatcherOperationCallback(RaiseRequerySuggested), null);
                } 
            }
        }

        private object RaiseRequerySuggested(object obj) 
        {
            // Call the RequerySuggested handlers 
            _requerySuggestedOperation = null; 
            CommandManager.CallWeakReferenceHandlers(_requerySuggestedHandlers);
            return null; 
        }

        internal static void CallWeakReferenceHandlers(List<WeakReference> handlers)
        { 
            if (handlers != null)
            { 
                // Take a snapshot of the handlers before we call out to them since the handlers 
                // could cause the array to me modified while we are reading it.
 
                EventHandler[] callees = new EventHandler[handlers.Count];
                int count = 0;

                for (int i = handlers.Count - 1; i >= 0; i--) 
                {
                    WeakReference reference = handlers[i]; 
                    EventHandler handler = reference.Target as EventHandler; 
                    if (handler == null)
                    { 
                        // Clean up old handlers that have been collected
                        handlers.RemoveAt(i);
                    }
                    else 
                    {
                        callees[count] = handler; 
                        count++; 
                    }
                } 

                // Call the handlers that we snapshotted
                for (int i = 0; i < count; i++)
                { 
                    EventHandler handler = callees[i];
                    handler(null, EventArgs.Empty); 
                } 
            }
        } 

        internal static void AddWeakReferenceHandler(ref List<WeakReference> handlers, EventHandler handler)
        {
            if (handlers == null) 
            {
                handlers = new List<WeakReference>(); 
            } 

            handlers.Add(new WeakReference(handler)); 
        }

        internal static void RemoveWeakReferenceHandler(List<WeakReference> handlers, EventHandler handler)
        { 
            if (handlers != null)
            { 
                for (int i = handlers.Count - 1; i >= 0; i--) 
                {
                    WeakReference reference = handlers[i]; 
                    EventHandler existingHandler = reference.Target as EventHandler;
                    if ((existingHandler == null) || (existingHandler == handler))
                    {
                        // Clean up old handlers that have been collected 
                        // in addition to the handler that is to be removed.
                        handlers.RemoveAt(i); 
                    } 
                }
            } 
        }

        #endregion
 
        #region Data
 
        // This is the event we fire when told to InvalidateStatus 
        private List<WeakReference> _requerySuggestedHandlers;
 
        // An operation object indicating that a callback is already on the dispatcher
        private DispatcherOperation _requerySuggestedOperation = null;

        #endregion 

        #endregion 
    } 
}

