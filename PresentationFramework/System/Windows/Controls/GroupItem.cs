//---------------------------------------------------------------------------- 
//
// <copyright file="GroupItem.cs" company="Microsoft">
//    Copyright (C) Microsoft Corporation.  All rights reserved.
// </copyright> 
//
// Description: GroupItem object - root of the UI subtree generated for a CollectionViewGroup 
// 
// Specs:       http://avalon/connecteddata/M5%20General%20Docs/Data%20Styling.mht
// 
//---------------------------------------------------------------------------

using System;
using System.Collections; 

namespace System.Windows.Controls 
{ 
    /// <summary>
    ///     A GroupItem appears as the root of the visual subtree generated for a CollectionViewGroup. 
    /// </summary>
    public class GroupItem : ContentControl
    {
        static GroupItem() 
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(GroupItem), new FrameworkPropertyMetadata(typeof(GroupItem))); 
            _dType = DependencyObjectType.FromSystemTypeInternal(typeof(GroupItem)); 

            // GroupItems should not be focusable by default 
            FocusableProperty.OverrideMetadata(typeof(GroupItem), new FrameworkPropertyMetadata(false));
        }

        /// <summary> 
        /// Creates AutomationPeer (<see cref="UIElement.OnCreateAutomationPeer"/>)
        /// </summary> 
        protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer() 
        {
            return new System.Windows.Automation.Peers.GroupItemAutomationPeer(this); 
        }

        /// <summary>
        ///     Gives a string representation of this object. 
        /// </summary>
        /// <returns></returns> 
        internal override string GetPlainText() 
        {
            System.Windows.Data.CollectionViewGroup cvg = Content as System.Windows.Data.CollectionViewGroup; 
            if (cvg != null && cvg.Name != null)
            {
                return cvg.Name.ToString();
            } 

            return base.GetPlainText(); 
        } 

        //----------------------------------------------------- 
        //
        // Internal Properties
        //
        //----------------------------------------------------- 

        internal ItemContainerGenerator Generator { get { return _generator; } set { _generator = value; } } 
 
        //------------------------------------------------------
        // 
        // Internal Methods
        //
        //-----------------------------------------------------
 
        internal void PrepareItemContainer(object item)
        { 
            if (Generator == null) 
                return;     // user-declared GroupItem - ignore (bug 108423)
 
            ItemContainerGenerator generator = Generator.Parent;
            GroupStyle groupStyle = generator.GroupStyle;

            // apply the container style 
            Style style = groupStyle.ContainerStyle;
 
            // no ContainerStyle set, try ContainerStyleSelector 
            if (style == null)
            { 
                if (groupStyle.ContainerStyleSelector != null)
                {
                    style = groupStyle.ContainerStyleSelector.SelectStyle(item, this);
                } 
            }
 
            // apply the style, if found 
            if (style != null)
            { 
                // verify style is appropriate before applying it
                if (!style.TargetType.IsInstanceOfType(this))
                    throw new InvalidOperationException(SR.Get(SRID.StyleForWrongType, style.TargetType.Name, this.GetType().Name));
 
                this.Style = style;
                this.WriteInternalFlag2(InternalFlags2.IsStyleSetFromGenerator, true); 
            } 

            // forward the header template information 
            if (!HasNonDefaultValue(ContentProperty))
                this.Content = item;
            if (!HasNonDefaultValue(ContentTemplateProperty))
                this.ContentTemplate = groupStyle.HeaderTemplate; 
            if (!HasNonDefaultValue(ContentTemplateSelectorProperty))
                this.ContentTemplateSelector = groupStyle.HeaderTemplateSelector; 
            if (!HasNonDefaultValue(ContentStringFormatProperty)) 
                this.ContentStringFormat = groupStyle.HeaderStringFormat;
        } 

        internal void ClearContainerForItem(object item)
        {
            if (Generator == null) 
                return;     // user-declared GroupItem - ignore (bug 108423)
 
            ItemContainerGenerator generator = Generator.Parent; 
            GroupStyle groupStyle = generator.GroupStyle;
 
            if (Object.Equals(this.Content, item))
                ClearValue(ContentProperty);
            if (this.ContentTemplate == groupStyle.HeaderTemplate)
                ClearValue(ContentTemplateProperty); 
            if (this.ContentTemplateSelector == groupStyle.HeaderTemplateSelector)
                ClearValue(ContentTemplateSelectorProperty); 
            if (this.ContentStringFormat == groupStyle.HeaderStringFormat) 
                ClearValue(ContentStringFormatProperty);
 
            Generator.Release();
        }

 
        //------------------------------------------------------
        // 
        // Private Fields 
        //
        //------------------------------------------------------ 

        ItemContainerGenerator _generator;

        #region DTypeThemeStyleKey 

        // Returns the DependencyObjectType for the registered ThemeStyleKey's default 
        // value. Controls will override this method to return approriate types. 
        internal override DependencyObjectType DTypeThemeStyleKey
        { 
            get { return _dType; }
        }

        private static DependencyObjectType _dType; 

        #endregion DTypeThemeStyleKey 
    } 
}
 

