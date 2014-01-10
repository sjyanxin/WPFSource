using System.Diagnostics; 
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Input; 
using System.Windows.Automation.Peers;
 
using MS.Internal.PresentationCore; 

namespace MS.Internal 
{
    internal static class UIElementHelper
    {
        [FriendAccessAllowed] 
        internal static bool IsHitTestVisible(DependencyObject o)
        { 
            Debug.Assert(o != null, "UIElementHelper.IsHitTestVisible called with null argument"); 

            UIElement oAsUIElement = o as UIElement; 
            if (oAsUIElement != null)
            {
                return oAsUIElement.IsHitTestVisible;
            } 
            else
            { 
                return ((UIElement3D)o).IsHitTestVisible; 
            }
        } 

        [FriendAccessAllowed]
        internal static bool IsVisible(DependencyObject o)
        { 
            Debug.Assert(o != null, "UIElementHelper.IsVisible called with null argument");
 
            UIElement oAsUIElement = o as UIElement; 
            if (oAsUIElement != null)
            { 
                return oAsUIElement.IsVisible;
            }
            else
            { 
                return ((UIElement3D)o).IsVisible;
            } 
        } 

        [FriendAccessAllowed] 
        internal static DependencyObject PredictFocus(DependencyObject o, FocusNavigationDirection direction)
        {
            Debug.Assert(o != null, "UIElementHelper.PredictFocus called with null argument");
 
            UIElement oAsUIElement = o as UIElement;
            if (oAsUIElement != null) 
            { 
                return oAsUIElement.PredictFocus(direction);
            } 
            else
            {
                return ((UIElement3D)o).PredictFocus(direction);
            } 
        }
 
        [FriendAccessAllowed] 
        internal static UIElement GetContainingUIElement2D(DependencyObject reference)
        { 
            UIElement element = null;

            while (reference != null)
            { 
                element = reference as UIElement;
 
                if (element != null) break; 

                reference = VisualTreeHelper.GetParent(reference); 
            }

            return element;
        } 

        [FriendAccessAllowed] 
        internal static DependencyObject GetUIParent(DependencyObject child) 
        {
            DependencyObject parent = GetUIParent(child, false); 

            return parent;
        }
 
        [FriendAccessAllowed]
        internal static DependencyObject GetUIParent(DependencyObject child, bool continuePastVisualTree) 
        { 
            DependencyObject parent = null;
            DependencyObject myParent = null; 

            // Try to find a UIElement parent in the visual ancestry.
            if (child is Visual)
            { 
                myParent = ((Visual)child).InternalVisualParent;
            } 
            else 
            {
                myParent = ((Visual3D)child).InternalVisualParent; 
            }

            parent = InputElement.GetContainingUIElement(myParent) as DependencyObject;
 
            // If there was no UIElement parent in the visual ancestry,
            // check along the logical branch. 
            if(parent == null && continuePastVisualTree) 
            {
                UIElement childAsUIElement = child as UIElement; 
                if (childAsUIElement != null)
                {
                    parent = InputElement.GetContainingInputElement(childAsUIElement.GetUIParentCore()) as DependencyObject;
                } 
                else
                { 
                    UIElement3D childAsUIElement3D = child as UIElement3D; 
                    if (childAsUIElement3D != null)
                    { 
                        parent = InputElement.GetContainingInputElement(childAsUIElement3D.GetUIParentCore()) as DependencyObject;
                    }
                }
            } 

            return parent; 
        } 

        [FriendAccessAllowed] 
        internal static bool IsUIElementOrUIElement3D(DependencyObject o)
        {
            return (o is UIElement || o is UIElement3D);
        } 

        [FriendAccessAllowed] 
        internal static bool InvalidateAutomationAncestors(DependencyObject o) 
        {
            if (o == null) 
                return false;
            AutomationPeer ap = null;

            UIElement e = o as UIElement; 
            if (e != null)
            { 
                if (e.HasAutomationPeer == true) 
                    ap = e.GetAutomationPeer();
            } 
            else
            {
                ContentElement ce = o as ContentElement;
                if (ce != null) 
                {
                    if (ce.HasAutomationPeer == true) 
                        ap = ce.GetAutomationPeer(); 
                }
                else 
                {
                    UIElement3D e3d = o as UIElement3D;
                    if (e3d != null)
                    { 
                        if (e3d.HasAutomationPeer == true)
                            ap = e3d.GetAutomationPeer(); 
                    } 
                }
            } 

            if (ap != null)
            {
                ap.InvalidateAncestorsRecursive(); 

                // Check for parent being non-null while stopping as we don't want to stop in between due to peers not connected to AT 
                // those peers sometimes gets created to serve for various patterns. 
                // e.g: ScrollViewAutomationPeer for Scroll Pattern in case of ListBox.
                if (ap.GetParent() != null) 
                    return true;
                else
                    return false;
            } 

            // Propagate the value through parent peers in both logical & visual parent chain, 
            // because automation tree contains peers corresponding to subset of the elements from both the trees. 
            DependencyObject coreParent = DeferredElementTreeState.GetInputElementParent(o, null);
            DependencyObject logicalParent = DeferredElementTreeState.GetLogicalParent(o, null); 

            if (coreParent != null)
            {
                if (InvalidateAutomationAncestors(coreParent)) 
                    return true;
            } 
            if (logicalParent != null && logicalParent != coreParent) 
            {
                if (InvalidateAutomationAncestors(logicalParent)) 
                    return true;
            }

            return false; 
        }
    } 
} 

