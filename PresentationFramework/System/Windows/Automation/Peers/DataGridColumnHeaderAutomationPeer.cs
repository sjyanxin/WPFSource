using System; 
using System.Collections.Generic;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives; 
using MS.Internal;
 
namespace System.Windows.Automation.Peers 
{
    /// <summary> 
    /// AutomationPeer for DataGridColumnHeader
    /// </summary>
    public sealed class DataGridColumnHeaderAutomationPeer : ButtonBaseAutomationPeer
    { 
        #region Constructors
 
        /// <summary> 
        /// AutomationPeer for DataGridColumnHeader
        /// </summary> 
        /// <param name="owner">DataGridColumnHeader</param>
        public DataGridColumnHeaderAutomationPeer(DataGridColumnHeader owner)
            : base(owner)
        { 
        }
 
        #endregion 

        #region AutomationPeer Overrides 

        /// <summary>
        /// Gets the control type for the element that is associated with the UI Automation peer.
        /// </summary> 
        /// <returns>The control type.</returns>
        protected override AutomationControlType GetAutomationControlTypeCore() 
        { 
            return AutomationControlType.HeaderItem;
        } 

        /// <summary>
        /// Called by GetClassName that gets a human readable name that, in addition to AutomationControlType,
        /// differentiates the control represented by this AutomationPeer. 
        /// </summary>
        /// <returns>The string that contains the name.</returns> 
        protected override string GetClassNameCore() 
        {
            return Owner.GetType().Name; 
        }

        ///
        override protected bool IsOffscreenCore() 
        {
            if (!Owner.IsVisible) 
                return true; 

            Rect boundingRect = CalculateVisibleBoundingRect(); 
            return DoubleUtil.AreClose(boundingRect, Rect.Empty) || DoubleUtil.AreClose(boundingRect.Height, 0.0) || DoubleUtil.AreClose(boundingRect.Width, 0.0);
        }

        #endregion 
    }
} 

