// ==++== 
//
//   Copyright (c) Microsoft Corporation.  All rights reserved.
//
// ==--== 
//  PolicyException.cs
// 
// <OWNER>[....]</OWNER> 
//
//  Use this class to throw a PolicyException 
//

namespace System.Security.Policy {
 
    using System;
    using System.Runtime.Serialization; 
    [Serializable] 
[System.Runtime.InteropServices.ComVisible(true)]
    public class PolicyException : SystemException 
    {
        public PolicyException()

            : base(Environment.GetResourceString( "Policy_Default" )) { 
            HResult = __HResults.CORSEC_E_POLICY_EXCEPTION;
        } 
 
        public PolicyException(String message)
 
            : base(message) {
            HResult = __HResults.CORSEC_E_POLICY_EXCEPTION;
        }
 
        public PolicyException(String message, Exception exception)
 
            : base(message, exception) { 
            HResult = __HResults.CORSEC_E_POLICY_EXCEPTION;
        } 

        [System.Security.SecuritySafeCritical]  // auto-generated
        protected PolicyException(SerializationInfo info, StreamingContext context) : base (info, context) {}
 
        internal PolicyException(String message, int hresult) : base (message)
        { 
            HResult = hresult; 
        }
 
        internal PolicyException(String message, int hresult, Exception exception) : base (message, exception)
        {
            HResult = hresult;
        } 

    } 
 
}

