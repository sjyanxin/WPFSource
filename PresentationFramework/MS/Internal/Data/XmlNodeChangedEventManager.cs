//---------------------------------------------------------------------------- 
//
// <copyright file="XmlNodeChangedEventManager.cs" company="Microsoft">
//    Copyright (C) Microsoft Corporation.  All rights reserved.
// </copyright> 
//
// Description: Manager for the XmlNodeChanged event in the "weak event listener" 
//              pattern.  See WeakEventTable.cs for an overview. 
//
//--------------------------------------------------------------------------- 

using System;
using System.Xml;
using System.Windows;       // WeakEventManager 

namespace MS.Internal.Data 
{ 
    /// <summary>
    /// Manager for the XmlDocument.XmlNodeChanged event. 
    /// </summary>
    internal class XmlNodeChangedEventManager : WeakEventManager
    {
        #region Constructors 

        // 
        //  Constructors 
        //
 
        private XmlNodeChangedEventManager()
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
        public static void AddListener(XmlDocument source, IWeakEventListener listener) 
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
        public static void RemoveListener(XmlDocument source, IWeakEventListener listener)
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
            XmlNodeChangedEventHandler handler = new XmlNodeChangedEventHandler(OnXmlNodeChanged); 
            XmlDocument doc = (XmlDocument)source;
            doc.NodeInserted += handler; 
            doc.NodeRemoved += handler;
            doc.NodeChanged += handler;
        }
 
        /// <summary>
        /// Stop listening to the given source for the event. 
        /// </summary> 
        protected override void StopListening(object source)
        { 
            XmlNodeChangedEventHandler handler = new XmlNodeChangedEventHandler(OnXmlNodeChanged);
            XmlDocument doc = (XmlDocument)source;
            doc.NodeInserted -= handler;
            doc.NodeRemoved -= handler; 
            doc.NodeChanged -= handler;
        } 
 
        #endregion Protected Methods
 
        #region Private Properties

        //
        //  Private Properties 
        //
 
        // get the event manager for the current thread 
        private static XmlNodeChangedEventManager CurrentManager
        { 
            get
            {
                Type managerType = typeof(XmlNodeChangedEventManager);
                XmlNodeChangedEventManager manager = (XmlNodeChangedEventManager)GetCurrentManager(managerType); 

                // at first use, create and register a new manager 
                if (manager == null) 
                {
                    manager = new XmlNodeChangedEventManager(); 
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
 
        // event handler for XmlNodeChanged event 
        private void OnXmlNodeChanged(object sender, XmlNodeChangedEventArgs args)
        { 
            DeliverEvent(sender, args);
        }

        #endregion Private Methods 
    }
} 
 

