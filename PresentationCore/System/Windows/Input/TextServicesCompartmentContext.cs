//---------------------------------------------------------------------------- 
//
// <copyright file=TextServicesCompartmentContext.cs company=Microsoft>
//    Copyright (C) Microsoft Corporation.  All rights reserved.
// </copyright> 
//
// Description: Manages Text Services Compartment. 
// 
// History:
//  07/30/2003 : yutakas - Ported from .net tree. 
//
//---------------------------------------------------------------------------

using System; 
using System.Runtime.InteropServices;
using System.Windows.Threading; 
 
using System.Security;
using System.Security.Permissions; 
using System.Diagnostics;
using System.Collections;
using MS.Internal;
using MS.Utility; 
using MS.Win32;
 
namespace System.Windows.Input 
{
    //----------------------------------------------------- 
    //
    //  TextServicesCompartmentContext class
    //
    //----------------------------------------------------- 

    internal class TextServicesCompartmentContext 
    { 
        //------------------------------------------------------
        // 
        //  Constructors
        //
        //-----------------------------------------------------
 
        /// <summary>
        ///  private constructer to avoid from creating instance outside. 
        /// </summary> 
        private TextServicesCompartmentContext()
        { 
        }

        //------------------------------------------------------
        // 
        //  Internal Methods
        // 
        //------------------------------------------------------ 

        #region Internal Methods 

        /// <summary>
        ///  Get the compartment of the given input method state.
        /// </summary> 
        /// <SecurityNote>
        /// Critical - retrieves message pump/input manager wrapper class 
        /// TreatAsSafe - returns safe wrapper for property request 
        /// </SecurityNote>
        [SecurityCritical, SecurityTreatAsSafe] 
        internal TextServicesCompartment GetCompartment(InputMethodStateType statetype)
        {
             for (int i = 0; i < InputMethodEventTypeInfo.InfoList.Length; i++)
             { 
                 InputMethodEventTypeInfo iminfo = InputMethodEventTypeInfo.InfoList[i];
 
                 if (iminfo.Type  == statetype) 
                 {
                     if (iminfo.Scope == CompartmentScope.Thread) 
                         return GetThreadCompartment(iminfo.Guid);
                     else if (iminfo.Scope == CompartmentScope.Global)
                         return GetGlobalCompartment(iminfo.Guid);
                 } 
             }
             return null; 
        } 

        /// <summary> 
        ///  Get the thread compartment of the Guid.
        /// </summary>
        /// <SecurityNote>
        /// Critical - manipulates input manager/message pump 
        /// </SecurityNote>
        [SecurityCritical] 
        internal TextServicesCompartment GetThreadCompartment(Guid guid) 
        {
            // No TextServices are installed so that the compartment won't work. 
            if (!TextServicesLoader.ServicesInstalled ||
                TextServicesContext.DispatcherCurrent == null)
                return null;
 
            UnsafeNativeMethods.ITfThreadMgr threadmgr = TextServicesContext.DispatcherCurrent.ThreadManager;
            if (threadmgr == null) 
                return null; 

            if (_compartmentTable == null) 
                _compartmentTable = new Hashtable();

            TextServicesCompartment compartment;
 
            compartment = _compartmentTable[guid] as TextServicesCompartment;
            if (compartment == null) 
            { 
                compartment = new TextServicesCompartment(guid,
                                                          threadmgr as UnsafeNativeMethods.ITfCompartmentMgr); 
                _compartmentTable[guid] = compartment;
            }

            return compartment; 
        }
 
        /// <summary> 
        ///  Get the global compartment of the Guid.
        /// </summary> 
        /// <SecurityNote>
        /// Critical - access input manager directly
        /// </SecurityNote>
        [SecurityCritical] 
        internal TextServicesCompartment GetGlobalCompartment(Guid guid)
        { 
            // No TextServices are installed so that the compartment won't work. 
            if (!TextServicesLoader.ServicesInstalled ||
                TextServicesContext.DispatcherCurrent == null) 
                return null;

            if (_globalcompartmentTable == null)
                _globalcompartmentTable = new Hashtable(); 

            if (_globalcompartmentmanager == null) 
            { 
                UnsafeNativeMethods.ITfThreadMgr threadmgr = TextServicesContext.DispatcherCurrent.ThreadManager;
 
                if (threadmgr == null)
                    return null;

                threadmgr.GetGlobalCompartment(out _globalcompartmentmanager); 
            }
 
            TextServicesCompartment compartment = null; 

            compartment = _globalcompartmentTable[guid] as TextServicesCompartment; 
            if (compartment == null)
            {
                compartment = new TextServicesCompartment(guid, _globalcompartmentmanager);
                _globalcompartmentTable[guid] = compartment; 
            }
 
            return compartment; 
        }
 
        #endregion Internal Methods

        //-----------------------------------------------------
        // 
        //  Internal Properties
        // 
        //------------------------------------------------------ 

        /// <summary> 
        ///  Create and get thread local compartment context.
        /// </summary>
        internal static TextServicesCompartmentContext Current
        { 
            get
            { 
                // TextServicesCompartmentContext for the current Dispatcher is stored in InputMethod of 
                // the current Dispatcher.
                if (InputMethod.Current.TextServicesCompartmentContext == null) 
                    InputMethod.Current.TextServicesCompartmentContext = new TextServicesCompartmentContext();

                return InputMethod.Current.TextServicesCompartmentContext;
            } 
        }
 
 
        //-----------------------------------------------------
        // 
        //  Private Fields
        //
        //-----------------------------------------------------
 
        // cache of ITfCompartments
        private Hashtable _compartmentTable; 
        private Hashtable _globalcompartmentTable; 

        // cache of the global compartment manager 
        private UnsafeNativeMethods.ITfCompartmentMgr  _globalcompartmentmanager;


    } 
}

