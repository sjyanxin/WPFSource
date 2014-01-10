//------------------------------------------------------------------------------ 
//
// <copyright file="UseLicense.cs" company="Microsoft">
//    Copyright (C) Microsoft Corporation.  All rights reserved.
// </copyright> 
//
// Description: This class represents the Use Lciense which enables end users to 
//              consume protected content. 
//
// History: 
//  06/01/2005: IgorBel :   Initial Implementation
//
//-----------------------------------------------------------------------------
 
using System;
using System.Collections; 
using System.Collections.Generic; 
using System.Collections.ObjectModel;
using System.Diagnostics; 
using System.Globalization;
using System.Windows;
using SecurityHelper=MS.Internal.WindowsBase.SecurityHelper;
 
using MS.Internal.Security.RightsManagement;
using MS.Internal.Utility; 
 
// Disable message about unknown message numbers so as to allow the suppression
// of PreSharp warnings (whose numbers are unknown to the compiler). 
#pragma warning disable 1634, 1691

namespace System.Security.RightsManagement
{ 

    /// <summary> 
    /// This class represents the Use Lciense which enables end users to consume protected content. 
    /// </summary>
    /// <SecurityNote> 
    ///     Critical:    This class expose access to methods that eventually do one or more of the the following
    ///             1. call into unmanaged code
    ///             2. affects state/data that will eventually cross over unmanaged code boundary
    ///             3. Return some RM related information which is considered private 
    ///
    ///     TreatAsSafe: This attribute automatically applied to all public entry points. All the public entry points have 
    ///     Demands for RightsManagementPermission at entry to counter the possible attacks that do 
    ///     not lead to the unamanged code directly(which is protected by another Demand there) but rather leave
    ///     some status/data behind which eventually might cross the unamanaged boundary. 
    /// </SecurityNote>
    [SecurityCritical(SecurityCriticalScope.Everything)]
    public class UseLicense
    { 

        /// <summary> 
        /// This constructor accepts the serialized form of a use license, and builds an instance of the classs based on that. 
        /// </summary>
        public UseLicense(string useLicense) 
        {
            SecurityHelper.DemandRightsManagementPermission();

            if (useLicense == null) 
            {
                throw new ArgumentNullException("useLicense"); 
            } 
            _serializedUseLicense = useLicense;
 

            /////////////////
            // parse out the Content Id GUID
            ///////////////// 
            string contentId;
            string contentIdType; 
            ClientSession.GetContentIdFromLicense(_serializedUseLicense, out contentId, out contentIdType); 

            if (contentId == null) 
            {
                throw new RightsManagementException(RightsManagementFailureCode.InvalidLicense);
            }
            else 
            {
                _contentId = new Guid(contentId); 
            } 

            ///////////////// 
            // Get Owner information from the license
            /////////////////
            _owner = ClientSession.ExtractUserFromCertificateChain(_serializedUseLicense);
 
            /////////////////
            // Get ApplicationSpecific Data Dictionary 
            ///////////////// 
            _applicationSpecificDataDictionary = new ReadOnlyDictionary <string, string>
                    (ClientSession.ExtractApplicationSpecificDataFromLicense(_serializedUseLicense)); 
        }

        /// <summary>
        /// This constructor accepts the serialized form of a use license, and builds an instance of the classs based on that. 
        /// </summary>
        public ContentUser Owner 
        { 
            get
            { 
                SecurityHelper.DemandRightsManagementPermission();

                return _owner;
            } 
        }
 
        /// <summary> 
        /// The ContentId is created by the publisher and can be used to match content to UseLicense and PublishLicenses.
        /// </summary> 
        public Guid ContentId
        {
            get
            { 
                SecurityHelper.DemandRightsManagementPermission();
 
                return _contentId; 
            }
        } 

        /// <summary>
        /// Returns the original XrML string that was used to deserialize the Use License
        /// </summary> 
        public override string ToString()
        { 
            SecurityHelper.DemandRightsManagementPermission(); 

            return _serializedUseLicense; 
        }


        /// <summary> 
        /// This function allows an application to examine or exercise the rights on a locally stored license.
        /// </summary> 
        public CryptoProvider Bind (SecureEnvironment secureEnvironment) 
        {
            SecurityHelper.DemandRightsManagementPermission(); 

            if (secureEnvironment == null)
            {
                throw new ArgumentNullException("secureEnvironment"); 
            }
 
            // The SecureEnvironment constructor makes sure ClientSession cannot be null. 
            // Accordingly suppressing preSharp warning about having to validate ClientSession.
#pragma warning suppress 6506 
            return secureEnvironment.ClientSession.TryBindUseLicenseToAllIdentites(_serializedUseLicense);
        }

        /// <summary> 
        /// ApplicationData data dictionary contains values that are passed from publishing
        /// application to a consuming application. One data pair that is processed by a Rights 
        /// Management Services (RMS) server is the string pair "Allow_Server_Editing"/"True". 
        /// When an issuance license has this value pair, it will allow the service, or any trusted
        /// service, to reuse the content key. The pair "NOLICCACHE" / "1" is expected to control 
        /// Use License embedding policy of the consuming applications. If it is set to 1, applications
        /// are expected not to embed the Use License into the document.
        /// </summary>
        public IDictionary<string,string> ApplicationData 
        {
            get 
            { 
                SecurityHelper.DemandRightsManagementPermission();
 
                return _applicationSpecificDataDictionary;
            }
        }
 
        /// <summary>
        /// Test for equality. 
        /// </summary> 
        public override bool Equals(object x)
        { 
            SecurityHelper.DemandRightsManagementPermission();

            if (x == null)
                return false;   // Standard behavior. 

            if (x.GetType() != GetType()) 
                return false;   // Not the same type. 

            // Note that because of the GetType() checking above, the casting must be valid. 
            UseLicense obj = (UseLicense)x;
            return (String.CompareOrdinal(_serializedUseLicense, obj._serializedUseLicense) == 0);

        } 

        /// <summary> 
        /// Compute hash code. 
        /// </summary>
        public override int GetHashCode() 
        {
            SecurityHelper.DemandRightsManagementPermission();

            return _serializedUseLicense.GetHashCode(); 
        }
 
 
        private string _serializedUseLicense;
        private Guid _contentId; 
        private ContentUser _owner = null;
        private IDictionary <string, string> _applicationSpecificDataDictionary = null;
    }
} 
