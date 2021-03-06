//---------------------------------------------------------------------------- 
//
// <copyright file="ReturnEventSaver.cs" company="Microsoft">
//    Copyright (C) Microsoft Corporation.  All rights reserved.
// </copyright> 
//
// Description: 
//             This class provides a convenient way to persist/depersist the events on a PageFunction 
//
//              By calling the _Detach() method on a pagefunction, 
//               this class will build a list of the class & methods on that Pagefunction,
//               as well as removing the current listener on the class when it's done.
//
//               By passing in a pagefunction on the _Attach method, the class will reattach the 
//               saved list to the calling pagefunction
// 
// History: 
//  06/11/03:      marka     created
// 
//---------------------------------------------------------------------------

using System;
using System.Windows.Navigation; 
using System.Windows;
using System.Diagnostics; 
using System.Collections; 
using System.Reflection;
using System.IO; 
using System.Security.Permissions;
using System.Security;

namespace MS.Internal.AppModel 
{
    [Serializable] 
    internal struct ReturnEventSaverInfo 
    {
        internal ReturnEventSaverInfo(string delegateTypeName, string targetTypeName, string delegateMethodName, bool fSamePf) 
        {
            _delegateTypeName = delegateTypeName;
            _targetTypeName = targetTypeName;
            _delegateMethodName = delegateMethodName; 
            _delegateInSamePF = fSamePf;
        } 
 
        internal String _delegateTypeName;
        internal String _targetTypeName; 
        internal String _delegateMethodName;
        internal bool _delegateInSamePF;   // Return Event handler comes from the same pagefunction, this is for non-generic workaround.
    }
 
    [Serializable]
    internal class ReturnEventSaver 
    { 
        internal ReturnEventSaver()
        { 

        }

 
        /// <SecurityNote>
        /// Critical - sets the critical _returnList. 
        /// TreatAsSafe - _returnList is not exposed in any way. 
        /// </SecurityNote>
        [SecurityCritical, SecurityTreatAsSafe] 
        internal void _Detach(PageFunctionBase pf)
        {
            if (pf._Return != null && pf._Saver == null)
            { 
                ReturnEventSaverInfo[] list = null;
 
                Delegate[] delegates = null; 

                delegates = (pf._Return).GetInvocationList(); 
                list = _returnList = new ReturnEventSaverInfo[delegates.Length];

                for (int i = 0; i < delegates.Length; i++)
                { 
                    Delegate returnDelegate = delegates[i];
                    bool bSamePf = false; 
 
                    if (returnDelegate.Target == pf)
                    { 
                        // This is the Event Handler implemented by the same PF, use for NonGeneric handling.
                        bSamePf = true;
                    }
 
                    MethodInfo m = returnDelegate.Method;
                    ReturnEventSaverInfo info = new ReturnEventSaverInfo( 
                        returnDelegate.GetType().AssemblyQualifiedName, 
                        returnDelegate.Target.GetType().AssemblyQualifiedName,
                        m.Name, bSamePf); 

                    list[i] = info;
                }
 
                //
                // only save if there were delegates already attached. 
                // note that there will be cases where the Saver has already been pre-populated from a Load 
                // but no delegates have been created yet ( as the PF hasn`t called finish as yet)
                // 
                // By only storing the saver once there are delegates - we avoid the problem of
                // wiping out any newly restored saver
                pf._Saver = this;
            } 

            pf._DetachEvents(); 
        } 

 
        //
        // Attach the stored events to the supplied pagefunction.
        //
        // caller  - the Calling Page's root element. We will reattach events *from* this page root element *to* the child 
        //
        // child   - the child PageFunction. Caller was originally attached to child, we're now reattaching *to* the child 
        // 
        /// <SecurityNote>
        /// Critical - Asserts ReflectionPermission to be able re-create delegate to private method. 
        /// TreatAsSafe - The delegate created is identical to the one that _Detach() received from
        ///     the application and saved. This is ensured by matching the type of the original target
        ///     object against the type of the new target. Thus we know that the application was able
        ///     to create a delegate over the exact method, and even if that method had a LinkDemand, 
        ///     it was satisfied by the application.
        /// </SecurityNote> 
        [SecurityCritical, SecurityTreatAsSafe] 
        internal void _Attach(Object caller, PageFunctionBase child)
        { 
            ReturnEventSaverInfo[] list = null;

            list = _returnList;
 
            if (list != null)
            { 
                Debug.Assert(caller != null, "Caller should not be null"); 
                for (int i = 0; i < list.Length; i++)
                { 
                    //
                    //

 

 
                    if (string.Compare(_returnList[i]._targetTypeName, caller.GetType().AssemblyQualifiedName, StringComparison.Ordinal) != 0) 
                    {
                        throw new NotSupportedException(SR.Get(SRID.ReturnEventHandlerMustBeOnParentPage)); 
                    }

                    Delegate d;
                    try 
                    {
                        new ReflectionPermission(ReflectionPermissionFlag.MemberAccess).Assert(); // BlessedAssert 
 
                        d = Delegate.CreateDelegate(
                                                                Type.GetType(_returnList[i]._delegateTypeName), 
                                                                caller,
                                                                _returnList[i]._delegateMethodName);
                    }
                    catch (Exception ex) 
                    {
                        throw new NotSupportedException(SR.Get(SRID.ReturnEventHandlerMustBeOnParentPage), ex); 
                    } 
                    finally
                    { 
                        ReflectionPermission.RevertAssert();
                    }

                    child._AddEventHandler(d); 
                }
            } 
        } 

        /// <SecurityNote> 
        /// Critical: contains metadata for delegates created under elevation.
        /// </SecurityNote>
        [SecurityCritical]
        private ReturnEventSaverInfo[] _returnList;     // The list of delegates we want to persist and return later 
    }
} 

