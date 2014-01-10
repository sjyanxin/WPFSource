using System; 
using System.Windows;
using System.Windows.Controls;

namespace System.Windows.Automation.Peers 
{
 
   /// 
   public class TabItemWrapperAutomationPeer: FrameworkElementAutomationPeer
    { 
        ///
        public TabItemWrapperAutomationPeer(TabItem owner): base(owner)
        {}
 
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

 


