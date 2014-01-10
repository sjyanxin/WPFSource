using System; 
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Permissions;
using System.Text; 
using System.Windows;
using System.Windows.Controls.Primitives; 
using System.Windows.Interop; 
using System.Windows.Media;
 
using MS.Internal;
using MS.Win32;

namespace System.Windows.Automation.Peers 
{
 
    /// 
    internal class PopupRootAutomationPeer : FrameworkElementAutomationPeer
    { 
        ///
        public PopupRootAutomationPeer(PopupRoot owner): base(owner)
        {}
 
        ///
        override protected string GetClassNameCore() 
        { 
            return "Popup";
        } 

        ///
        override protected AutomationControlType GetAutomationControlTypeCore()
        { 
            return AutomationControlType.Window;
        } 
    } 
}
 
