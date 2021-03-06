// ==++== 
//
//   Copyright (c) Microsoft Corporation.  All rights reserved.
//
// ==--== 
/*============================================================
** 
** Class:  PathTooLongException 
**
** <OWNER>[....]</OWNER> 
**
**
** Purpose: Exception for paths and/or filenames that are
** too long. 
**
** 
===========================================================*/ 

using System; 
using System.Runtime.Serialization;

namespace System.IO {
 
    [Serializable]
[System.Runtime.InteropServices.ComVisible(true)] 
    public class PathTooLongException : IOException 
    {
        public PathTooLongException() 
            : base(Environment.GetResourceString("IO.PathTooLong")) {
            SetErrorCode(__HResults.COR_E_PATHTOOLONG);
        }
 
        public PathTooLongException(String message)
            : base(message) { 
            SetErrorCode(__HResults.COR_E_PATHTOOLONG); 
        }
 
        public PathTooLongException(String message, Exception innerException)
            : base(message, innerException) {
            SetErrorCode(__HResults.COR_E_PATHTOOLONG);
        } 

        [System.Security.SecuritySafeCritical]  // auto-generated 
        protected PathTooLongException(SerializationInfo info, StreamingContext context) : base (info, context) { 
        }
    } 
}

