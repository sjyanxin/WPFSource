//------------------------------------------------------------------------------ 
//
// <copyright file="RevocationPoint.cs" company="Microsoft">
//    Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright> 
//
// Description: 
//  This is a structure representing a revocation point, as it is being defined by the 
//  DRMGetRevocationPoint DRMSetRevocationPoint   MS DRM SDK functions.
// 
// History:
//  02/27/2006: IgorBel:  Initial implementation.
//
//----------------------------------------------------------------------------- 

 
using System; 
using System.Runtime.InteropServices;
using System.Text; 
using System.Security;

namespace MS.Internal.Security.RightsManagement
{ 
    /// <summary>
    ///  This class doesn't have any data validation. It is only used as a pass through mechanism from 
    ///  GetRevocationPoint to SetRevocationPoint. If we ever choose to add public APIs that control 
    ///  revocation point settings extra validation will need to be added on all the public API entry points
    /// and probably n the class properties as well. 
    /// The Frequency property in the public API space would be better represented by a TimeSpan (not DateTime).
    /// </summary>
    /// <SecurityNote>
    ///     Critical:    This class exposes access to methods that eventually do one or more of the the following 
    ///             1. call into unmanaged code
    ///             2. affects state/data that will eventually cross over unmanaged code boundary 
    ///             3. Return some RM related information which is considered private 
    /// </SecurityNote>
    [SecurityCritical(SecurityCriticalScope.Everything)] 
    internal class RevocationPoint
    {
        //-----------------------------------------------------
        // 
        //  Internal Properties
        // 
        //----------------------------------------------------- 
        internal string Id
        { 
            get
            {
                return _id;
            } 
            set
            { 
                _id = value; 
            }
        } 

        internal string IdType
        {
            get 
            {
                return _idType; 
            } 
            set
            { 
                _idType = value;
            }
        }
 
        internal Uri Url
        { 
            get 
            {
                return _url; 
            }
            set
            {
                _url = value; 
            }
        } 
 
        internal SystemTime Frequency
        { 
            get
            {
                return _frequency;
            } 
            set
            { 
                _frequency = value; 
            }
        } 

        internal string Name
        {
            get 
            {
                return _name; 
            } 
            set
            { 
                _name = value;
            }
        }
 
        internal string PublicKey
        { 
            get 
            {
                return _publicKey; 
            }
            set
            {
                _publicKey = value; 
            }
        } 
 
        //------------------------------------------------------
        // 
        //  Private Fields
        //
        //-----------------------------------------------------
        private string _id; 
        private string _idType;
        private Uri _url; 
        private SystemTime _frequency; 
        private string _name;
        private string _publicKey; 
    }
}

