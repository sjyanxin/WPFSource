//---------------------------------------------------------------------------- 
//
// <copyright file="LostFocusEventManager.cs" company="Microsoft">
//    Copyright (C) Microsoft Corporation.  All rights reserved.
// </copyright> 
//
// Description: Manager for the LostFocus event in the "weak event listener" 
//              pattern.  See WeakEventTable.cs for an overview. 
//
//--------------------------------------------------------------------------- 

using System;
using System.Windows;       // WeakEventManager
using MS.Internal;          // Helper 

namespace System.Windows 
{ 
    /// <summary>
    /// Manager for the DependencyObject.LostFocus event. 
    /// </summary>
    public class LostFocusEventManager : WeakEventManager
    {
        #region Constructors 

        // 
        //  Constructors 
        //
 
        private LostFocusEventManager()
        {
        }
 
        #endregion Constructors
 
        #region Public Methods 

        // 
        //  Public Methods
        //

        /// <summary> 
        /// Add a listener to the given source's event.
        /// </summary> 
        public static void AddListener(DependencyObject source, IWeakEventListener listener) 
        {
            if (source == null) 
                throw new ArgumentNullException("source");
            if (listener == null)
                throw new ArgumentNullException("listener");
 
            CurrentManager.ProtectedAddListener(source, listener);
        } 
 
        /// <summary>
        /// Remove a listener to the given source's event. 
        /// </summary>
        public static void RemoveListener(DependencyObject source, IWeakEventListener listener)
        {
            if (source == null) 
                throw new ArgumentNullException("source");
            if (listener == null) 
                throw new ArgumentNullException("listener"); 

            CurrentManager.ProtectedRemoveListener(source, listener); 
        }

        #endregion Public Methods
 
        #region Protected Methods
 
        // 
        //  Protected Methods
        // 

        /// <summary>
        /// Listen to the given source for the event.
        /// </summary> 
        protected override void StartListening(object source)
        { 
            DependencyObject typedSource = (DependencyObject)source; 
            FrameworkElement fe;
            FrameworkContentElement fce; 
            Helper.DowncastToFEorFCE(typedSource, out fe, out fce, true);

            if (fe != null)
                fe.LostFocus += new RoutedEventHandler(OnLostFocus); 
            else if (fce != null)
                fce.LostFocus += new RoutedEventHandler(OnLostFocus); 
        } 

        /// <summary> 
        /// Stop listening to the given source for the event.
        /// </summary>
        protected override void StopListening(object source)
        { 
            DependencyObject typedSource = (DependencyObject)source;
            FrameworkElement fe; 
            FrameworkContentElement fce; 
            Helper.DowncastToFEorFCE(typedSource, out fe, out fce, true);
 
            if (fe != null)
                fe.LostFocus -= new RoutedEventHandler(OnLostFocus);
            else if (fce != null)
                fce.LostFocus -= new RoutedEventHandler(OnLostFocus); 
        }
 
        #endregion Protected Methods 

        #region Private Properties 

        //
        //  Private Properties
        // 

        // get the event manager for the current thread 
        private static LostFocusEventManager CurrentManager 
        {
            get 
            {
                Type managerType = typeof(LostFocusEventManager);
                LostFocusEventManager manager = (LostFocusEventManager)GetCurrentManager(managerType);
 
                // at first use, create and register a new manager
                if (manager == null) 
                { 
                    manager = new LostFocusEventManager();
                    SetCurrentManager(managerType, manager); 
                }

                return manager;
            } 
        }
 
        #endregion Private Properties 

        #region Private Methods 

        //
        //  Private Methods
        // 

        // event handler for LostFocus event 
        private void OnLostFocus(object sender, RoutedEventArgs args) 
        {
            DeliverEvent(sender, args); 
        }

        #endregion Private Methods
    } 
}
 

