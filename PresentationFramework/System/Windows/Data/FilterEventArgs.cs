//---------------------------------------------------------------------------- 
//
// <copyright file="FilterEventArgs.cs" company="Microsoft">
//    Copyright (C) Microsoft Corporation.  All rights reserved.
// </copyright> 
//
// Description: Filter event arguments 
// 
// Specs:       http://avalon/connecteddata/Specs/CollectionViewSource.mht
// 
//---------------------------------------------------------------------------

using System;
 
namespace System.Windows.Data
{ 
 
    /// <summary>
    /// Arguments for the Filter event. 
    /// </summary>
    /// <remarks>
    /// <p>The event receiver should set Accepted to true if the item
    /// passes the filter, or false if it fails.</p> 
    /// </remarks>
    public class FilterEventArgs : EventArgs 
    { 
        //-----------------------------------------------------
        // 
        //  Constructors
        //
        //-----------------------------------------------------
 
        internal FilterEventArgs(object item)
        { 
            _item = item; 
            _accepted = true;
        } 

        //------------------------------------------------------
        //
        //  Public Properties 
        //
        //----------------------------------------------------- 
 
        /// <summary>
        /// The object to be tested by the filter. 
        /// </summary>
        public object Item
        {
            get { return _item; } 
        }
 
        /// <summary> 
        /// The return value of the filter.
        /// </summary> 
        public bool Accepted
        {
            get { return _accepted; }
            set { _accepted = value; } 
        }
 
        //------------------------------------------------------ 
        //
        //  Private Fields 
        //
        //------------------------------------------------------

        private object _item; 
        private bool _accepted;
    } 
 
    /// <summary>
    ///     The delegate to use for handlers that receive FilterEventArgs. 
    /// </summary>
    public delegate void FilterEventHandler(object sender, FilterEventArgs e);
}
 


