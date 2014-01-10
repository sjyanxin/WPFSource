using System; 
using System.Collections.Generic;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives; 
using MS.Internal;
 
namespace System.Windows.Automation.Peers 
{
    /// <summary> 
    /// AutomationPeer for DataGridDetailsPresenter
    /// </summary>
    public sealed class DataGridDetailsPresenterAutomationPeer : FrameworkElementAutomationPeer
    { 
        #region Constructors
 
        /// <summary> 
        /// AutomationPeer for DataGridDetailsPresenter
        /// </summary> 
        /// <param name="owner">DataGridDetailsPresenter</param>
        public DataGridDetailsPresenterAutomationPeer(DataGridDetailsPresenter owner)
            : base(owner)
        { 
        }
 
        #endregion 

        #region AutomationPeer Overrides 

        ///
        protected override string GetClassNameCore()
        { 
            return this.Owner.GetType().Name;
        } 
 
        ///
        protected override bool IsContentElementCore() 
        {
            return false;
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

