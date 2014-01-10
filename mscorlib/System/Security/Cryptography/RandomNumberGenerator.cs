// ==++== 
//
//   Copyright (c) Microsoft Corporation.  All rights reserved.
//
// ==--== 
// <OWNER>[....]</OWNER>
// 
 
//
// RandomNumberGenerator.cs 
//

namespace System.Security.Cryptography {
#if !FEATURE_CORECLR && !SILVERLIGHT 
[System.Runtime.InteropServices.ComVisible(true)]
#endif // !FEATURE_CORECLR && !SILVERLIGHT 
    public abstract class RandomNumberGenerator 
    // On Orcas RandomNumberGenerator is not disposable, so we cannot add the IDisposable implementation to the
    // CoreCLR mscorlib.  However, this type does need to be disposable since subtypes can and do hold onto 
    // native resources. Therefore, on desktop mscorlibs we add an IDisposable implementation.
#if !FEATURE_CORECLR
    : IDisposable
#endif // !FEATURE_CORECLR 
    {
        protected RandomNumberGenerator() { 
        } 

        // 
        // public methods
        //

#if !FEATURE_CORECLR && !SILVERLIGHT && !CORIOLIS 
        [System.Security.SecuritySafeCritical]  // auto-generated
        static public RandomNumberGenerator Create() { 
            return Create("System.Security.Cryptography.RandomNumberGenerator"); 
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated
        static public RandomNumberGenerator Create(String rngName) {
            return (RandomNumberGenerator) CryptoConfig.CreateFromName(rngName);
        } 
#endif // !FEATURE_CORECLR && !SILVERLIGHT && !CORIOLIS
 
        public void Dispose() { 
            Dispose(true);
            GC.SuppressFinalize(this); 
        }

        protected virtual void Dispose(bool disposing) {
            return; 
        }
 
        public abstract void GetBytes(byte[] data); 

#if !FEATURE_CORECLR && !SILVERLIGHT 
        public abstract void GetNonZeroBytes(byte[] data);
#endif // !FEATURE_CORECLR && !SILVERLIGHT
    }
} 

