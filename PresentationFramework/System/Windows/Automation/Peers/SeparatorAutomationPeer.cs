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
    public class SeparatorAutomationPeer : FrameworkElementAutomationPeer
    {
        /// 
        public SeparatorAutomationPeer(Separator owner): base(owner)
        {} 
 
        ///
        protected override string GetClassNameCore() 
        {
            return "Separator";
        }
 
        ///
        protected override AutomationControlType GetAutomationControlTypeCore() 
        { 
            return AutomationControlType.Separator;
        } 

    }
}
 

 

