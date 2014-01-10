//---------------------------------------------------------------------------- 
//
// Copyright (C) Microsoft Corporation.  All rights reserved.
//
//--------------------------------------------------------------------------- 

using MS.Internal; 
using MS.Utility; 

using System; 
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel; 
using System.Windows.Media;
using System.Windows.Controls.Primitives;   // IItemContainerGenerator 
 
namespace System.Windows.Controls
{ 
    /// <summary>
    ///     A base class that provides access to information that is useful for panels that with to implement virtualization.
    /// </summary>
    public abstract class VirtualizingPanel : Panel 
    {
        /// <summary> 
        ///     The default constructor. 
        /// </summary>
        protected VirtualizingPanel() : base() 
        {
        }

        /// <summary> 
        ///     The generator associated with this panel.
        /// </summary> 
        public IItemContainerGenerator ItemContainerGenerator 
        {
            get 
            {
                return Generator;
            }
        } 

        internal override void GenerateChildren() 
        { 
            // Do nothing. Subclasses will use the exposed generator to generate children.
        } 

        /// <summary>
        ///     Adds a child to the InternalChildren collection.
        ///     This method is meant to be used when a virtualizing panel 
        ///     generates a new child. This method circumvents some validation
        ///     that occurs in UIElementCollection.Add. 
        /// </summary> 
        /// <param name="child">Child to add.</param>
        protected void AddInternalChild(UIElement child) 
        {
            AddInternalChild(InternalChildren, child);
        }
 
        /// <summary>
        ///     Inserts a child into the InternalChildren collection. 
        ///     This method is meant to be used when a virtualizing panel 
        ///     generates a new child. This method circumvents some validation
        ///     that occurs in UIElementCollection.Insert. 
        /// </summary>
        /// <param name="index">The index at which to insert the child.</param>
        /// <param name="child">Child to insert.</param>
        protected void InsertInternalChild(int index, UIElement child) 
        {
            InsertInternalChild(InternalChildren, index, child); 
        } 

        /// <summary> 
        ///     Removes a child from the InternalChildren collection.
        ///     This method is meant to be used when a virtualizing panel
        ///     re-virtualizes a new child. This method circumvents some validation
        ///     that occurs in UIElementCollection.RemoveRange. 
        /// </summary>
        /// <param name="index"></param> 
        /// <param name="range"></param> 
        protected void RemoveInternalChildRange(int index, int range)
        { 
            RemoveInternalChildRange(InternalChildren, index, range);
        }

        // This is internal as an optimization for VirtualizingStackPanel (so it doesn't need to re-query InternalChildren repeatedly) 
        internal static void AddInternalChild(UIElementCollection children, UIElement child)
        { 
            children.AddInternal(child); 
        }
 
        // This is internal as an optimization for VirtualizingStackPanel (so it doesn't need to re-query InternalChildren repeatedly)
        internal static void InsertInternalChild(UIElementCollection children, int index, UIElement child)
        {
            children.InsertInternal(index, child); 
        }
 
        // This is internal as an optimization for VirtualizingStackPanel (so it doesn't need to re-query InternalChildren repeatedly) 
        internal static void RemoveInternalChildRange(UIElementCollection children, int index, int range)
        { 
            children.RemoveRangeInternal(index, range);
        }

 
        /// <summary>
        ///     Called when the Items collection associated with the containing ItemsControl changes. 
        /// </summary> 
        /// <param name="sender">sender</param>
        /// <param name="args">Event arguments</param> 
        protected virtual void OnItemsChanged(object sender, ItemsChangedEventArgs args)
        {
        }
 
        /// <summary>
        ///     Called when the UI collection of children is cleared by the base Panel class. 
        /// </summary> 
        protected virtual void OnClearChildren()
        { 
        }

        /// <summary>
        /// Generates the item at the specified index and calls BringIntoView on it. 
        /// </summary>
        /// <param name="index">Specify the item index that should become visible</param> 
        protected internal virtual void BringIndexIntoView(int index) 
        {
        } 

        internal override void OnItemsChangedInternal(object sender, ItemsChangedEventArgs args)
        {
            switch (args.Action) 
            {
                case NotifyCollectionChangedAction.Add: 
                case NotifyCollectionChangedAction.Remove: 
                case NotifyCollectionChangedAction.Replace:
                case NotifyCollectionChangedAction.Move: 
                    // Don't allow Panel's code to run for add/remove/replace/move
                    break;

                default: 
                    base.OnItemsChangedInternal(sender, args);
                    break; 
            } 

            OnItemsChanged(sender, args); 
        }

        internal override void OnClearChildrenInternal()
        { 
            OnClearChildren();
        } 
    } 
}

