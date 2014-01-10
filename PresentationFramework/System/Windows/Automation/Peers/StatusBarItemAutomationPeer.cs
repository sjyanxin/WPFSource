using System; 
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security; 
using System.Text;
using System.Windows; 
using System.Windows.Automation.Provider; 
using System.Windows.Controls;
using System.Windows.Controls.Primitives; 
using System.Windows.Interop;
using System.Windows.Media;

using MS.Internal; 
using MS.Win32;
 
namespace System.Windows.Automation.Peers 
{
 
    ///
    public class StatusBarItemAutomationPeer : FrameworkElementAutomationPeer
    {
        /// 
        public StatusBarItemAutomationPeer(StatusBarItem owner): base(owner)
        { 
        } 

        /// 
        override protected string GetClassNameCore()
        {
            return "StatusBarItem";
        } 

        /// 
        override protected AutomationControlType GetAutomationControlTypeCore() 
        {
            return AutomationControlType.Text; 
        }

        ///
        override protected bool IsOffscreenCore() 
        {
            if (!Owner.IsVisible) 
                return true; 

            Rect boundingRect = CalculateVisibleBoundingRect(); 
            return (boundingRect == Rect.Empty || boundingRect.Height == 0 || boundingRect.Width == 0);
        }

    } 
}
 
 


