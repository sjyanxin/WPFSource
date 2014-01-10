using System.Diagnostics; 
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Input; 
using System.Windows.Automation;
using System.Windows.Automation.Peers; 
using MS.Internal.PresentationCore; 

namespace MS.Internal 
{
    internal static class SynchronizedInputHelper
    {
        internal static DependencyObject GetUIParentCore(DependencyObject o) 
        {
            UIElement e = o as UIElement; 
            if (e != null) 
            {
                return e.GetUIParentCore(); 
            }
            else
            {
                ContentElement ce = o as ContentElement; 
                if (ce != null)
                { 
                    return ce.GetUIParentCore(); 
                }
                else 
                {
                    UIElement3D e3D = o as UIElement3D;
                    if (e3D != null)
                    { 
                        return e3D.GetUIParentCore();
                    } 
                } 
                return null;
            } 
        }

        // Check whether this element is listening for input.
        internal static bool IsListening(DependencyObject o, RoutedEventArgs args) 
        {
            if ((InputManager.ListeningElement == o) && (args.RoutedEvent == InputManager.SynchronizedInputEvent)) 
            { 
                return true;
            } 
            else
            {
                return false;
            } 
        }
 
        // Add a preopportunity handler for the logical parent incase of templated element. 
        internal static void AddParentPreOpportunityHandler(DependencyObject o, EventRoute route, RoutedEventArgs args)
        { 
            // If the logical parent is different from visual parent then add handler on behalf of the
            // parent into the route. This is to cover the templated elements, where event could be
            // handled by one of the child visual element but we should consider it as if event is handled by
            // parent element ( logical parent). 
            DependencyObject visualParent = null;
            if(o is Visual || o is Visual3D) 
            { 
                visualParent = UIElementHelper.GetUIParent(o);
            } 
            DependencyObject logicalParent = SynchronizedInputHelper.GetUIParentCore(o);
            if (logicalParent != null && logicalParent != visualParent)
            {
                UIElement e = logicalParent as UIElement; 
                if (e != null)
                { 
                    e.AddSynchronizedInputPreOpportunityHandler(route, args); 
                }
                else 
                {
                    ContentElement ce = logicalParent as ContentElement;
                    if (ce != null)
                    { 
                        ce.AddSynchronizedInputPreOpportunityHandler(route, args);
                    } 
                    else 
                    {
                        UIElement3D e3D = logicalParent as UIElement3D; 
                        if (e3D != null)
                        {
                            e3D.AddSynchronizedInputPreOpportunityHandler(route, args);
                        } 
                    }
                } 
            } 
        }
 
        // If the routed event type matches one the element listening on then add handler to the event route.
        internal static void AddHandlerToRoute(DependencyObject o, EventRoute route, RoutedEventHandler eventHandler, bool handledToo)
        {
            // Add a synchronized input handler to the route. 
            route.Add(o, eventHandler, handledToo);
        } 
 
        // If this handler is invoked then it indicates the element had the opportunity to handle event.
        internal static void PreOpportunityHandler(object sender, RoutedEventArgs args) 
        {
            KeyboardEventArgs kArgs = args as KeyboardEventArgs;
            // if it's the keyboard event then we have 1:1 mapping between handlers & events,
            // so no remapping required. 
            if (kArgs != null)
            { 
                InputManager.SynchronizedInputState = SynchronizedInputStates.HadOpportunity; 
            }
            else 
            {
                // If this is an mouse event then we have handlers only for generic MouseDown & MouseUp events,
                // so we need additional logic here to decide between Mouse left and right button events.
                MouseButtonEventArgs mbArgs = args as MouseButtonEventArgs; 
                if (mbArgs != null)
                { 
                    Debug.Assert(mbArgs != null); 
                    switch (mbArgs.ChangedButton)
                    { 
                        case MouseButton.Left:
                            if (InputManager.SynchronizeInputType == SynchronizedInputType.MouseLeftButtonDown ||
                                InputManager.SynchronizeInputType == SynchronizedInputType.MouseLeftButtonUp)
                            { 
                                InputManager.SynchronizedInputState = SynchronizedInputStates.HadOpportunity;
                            } 
                            break; 
                        case MouseButton.Right:
                            if (InputManager.SynchronizeInputType == SynchronizedInputType.MouseRightButtonDown || 
                                InputManager.SynchronizeInputType == SynchronizedInputType.MouseRightButtonUp)
                            {
                                InputManager.SynchronizedInputState = SynchronizedInputStates.HadOpportunity;
                            } 
                            break;
                        default: 
                            break; 
                    }
                } 
            }
        }

        // This handler will be called after all class and instance handlers are called, here we 
        // decide whether the event is handled by this element or some other element.
        internal static void PostOpportunityHandler(object sender, RoutedEventArgs args) 
        { 
            KeyboardEventArgs kArgs = args as KeyboardEventArgs;
            // if it's the keyboard event then we have 1:1 mapping between handlers & events, 
            // so no remapping required.
            if (kArgs != null)
            {
                InputManager.SynchronizedInputState = SynchronizedInputStates.Handled; 
            }
            else 
            { 
                // If this is an mouse event then we have handlers only for generic MouseDown & MouseUp events,
                // so we need additional logic here to decide between Mouse left and right button events. 
                MouseButtonEventArgs mbArgs = args as MouseButtonEventArgs;
                Debug.Assert(mbArgs != null);
                if (mbArgs != null)
                { 
                    switch (mbArgs.ChangedButton)
                    { 
                        case MouseButton.Left: 
                            if (InputManager.SynchronizeInputType == SynchronizedInputType.MouseLeftButtonDown ||
                                InputManager.SynchronizeInputType == SynchronizedInputType.MouseLeftButtonUp) 
                            {
                                InputManager.SynchronizedInputState = SynchronizedInputStates.Handled;
                            }
                            break; 
                        case MouseButton.Right:
                            if (InputManager.SynchronizeInputType == SynchronizedInputType.MouseRightButtonDown || 
                                InputManager.SynchronizeInputType == SynchronizedInputType.MouseRightButtonUp) 
                            {
                                InputManager.SynchronizedInputState = SynchronizedInputStates.Handled; 
                            }
                            break;
                        default:
                            break; 
                    }
                } 
            } 
        }
 


        // Map a Synchronized input type received from automation client to routed event
        internal static RoutedEvent MapInputTypeToRoutedEvent(SynchronizedInputType inputType) 
        {
            RoutedEvent e = null; 
            switch (inputType) 
            {
                case SynchronizedInputType.KeyUp: 
                    e = Keyboard.KeyUpEvent;
                    break;
                case SynchronizedInputType.KeyDown:
                    e = Keyboard.KeyDownEvent; 
                    break;
                case SynchronizedInputType.MouseLeftButtonDown: 
                case SynchronizedInputType.MouseRightButtonDown: 
                    e = Mouse.MouseDownEvent;
                    break; 
                case SynchronizedInputType.MouseLeftButtonUp:
                case SynchronizedInputType.MouseRightButtonUp:
                    e = Mouse.MouseUpEvent;
                    break; 
                default:
                    Debug.Assert(false); 
                    e = null; 
                    break;
            } 
            return e;
        }

        internal static void RaiseAutomationEvents() 
        {
            if (InputElement.IsUIElement(InputManager.ListeningElement)) 
            { 
                UIElement e = (UIElement)InputManager.ListeningElement;
                //Raise InputDiscarded automation event 
                SynchronizedInputHelper.RaiseAutomationEvent(e.GetAutomationPeer());
            }
            else if (InputElement.IsContentElement(InputManager.ListeningElement))
            { 
                ContentElement ce = (ContentElement)InputManager.ListeningElement;
                //Raise InputDiscarded automation event 
                SynchronizedInputHelper.RaiseAutomationEvent(ce.GetAutomationPeer()); 
            }
            else if (InputElement.IsUIElement3D(InputManager.ListeningElement)) 
            {
                UIElement3D e3D = (UIElement3D)InputManager.ListeningElement;
                //Raise InputDiscarded automation event
                SynchronizedInputHelper.RaiseAutomationEvent(e3D.GetAutomationPeer()); 
            }
        } 
 

        // Raise synchronized input automation events here. 
        internal static void RaiseAutomationEvent(AutomationPeer peer)
        {
            if (peer != null)
            { 
                switch (InputManager.SynchronizedInputState)
                { 
                    case SynchronizedInputStates.Handled: 
                        peer.RaiseAutomationEvent(AutomationEvents.InputReachedTarget);
                        break; 
                    case SynchronizedInputStates.Discarded:
                        peer.RaiseAutomationEvent(AutomationEvents.InputDiscarded);
                        break;
                    default: 
                        peer.RaiseAutomationEvent(AutomationEvents.InputReachedOtherElement);
                        break; 
                } 
            }
 
        }

        // Checks whether listening element is in event route
        internal static bool IsElementInEventRoute(DependencyObject listeningElement, DependencyObject source) 
        {
            DependencyObject d = source; 
            while (d != null) 
            {
                //listening element found in event route 
                if (d == listeningElement)
                {
                    return true;
                } 
                UIElement e = d as UIElement;
                if (e != null) 
                { 
                    d = e.GetUIParent();
                } 
                else
                {
                    ContentElement ce = d as ContentElement;
                    if (ce != null) 
                    {
                        d = ce.GetUIParent(); 
                    } 
                    else
                    { 
                        UIElement3D e3D = d as UIElement3D;
                        if (e3D != null)
                        {
                            d = e3D.GetUIParent(false); 
                        }
                        else 
                        { 
                            d = null;
                        } 
                    }
                }
            }
            return false; 
        }
    } 
 
    internal enum SynchronizedInputStates
    { 
        NoOpportunity  = 0x01,
        HadOpportunity = 0x02,
        Handled        = 0x04,
        Discarded      = 0x08 
    };
} 

