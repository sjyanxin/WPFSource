// ==++== 
//
//   Copyright (c) Microsoft Corporation.  All rights reserved.
//
// ==--== 
/*============================================================
** 
** Class:  OperationCanceledException 
**
** 
** Purpose: Exception for cancelled IO requests.
**
**
===========================================================*/ 

using System; 
using System.Runtime.Serialization; 
using System.Threading;
 
namespace System {

    [Serializable]
    [System.Runtime.InteropServices.ComVisible(true)] 
    public class OperationCanceledException : SystemException
    { 
#if !FEATURE_CORECLR 
        [NonSerialized]
        private CancellationToken _cancellationToken; 

        public CancellationToken CancellationToken
        {
            get { return _cancellationToken;} 
            private set { _cancellationToken = value;}
        } 
#endif //!FEATURE_CORECLR 

        public OperationCanceledException() 
            : base(Environment.GetResourceString("OperationCanceled")) {
            SetErrorCode(__HResults.COR_E_OPERATIONCANCELED);
        }
 
        public OperationCanceledException(String message)
            : base(message) { 
            SetErrorCode(__HResults.COR_E_OPERATIONCANCELED); 
        }
 
        public OperationCanceledException(String message, Exception innerException)
            : base(message, innerException) {
            SetErrorCode(__HResults.COR_E_OPERATIONCANCELED);
        } 

#if !FEATURE_CORECLR 
        public OperationCanceledException(CancellationToken token) 
            :this()
        { 
            CancellationToken = token;
        }

        public OperationCanceledException(String message, CancellationToken token) 
            : this(message)
        { 
            CancellationToken = token; 
        }
 
        public OperationCanceledException(String message, Exception innerException, CancellationToken token)
            : this(message, innerException)
        {
            CancellationToken = token; 
        }
#endif //!FEATURE_CORECLR 
 
        protected OperationCanceledException(SerializationInfo info, StreamingContext context) : base (info, context) {
        } 
    }
}

