//------------------------------------------------------------------------------ 
// <copyright file="TimeZoneNotFoundException.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//----------------------------------------------------------------------------- 

namespace System { 
   using  System.Runtime.Serialization; 
   using  System.Runtime.CompilerServices;
 
   [Serializable]
   [TypeForwardedFrom("System.Core, Version=3.5.0.0, Culture=Neutral, PublicKeyToken=b77a5c561934e089")]
   [System.Security.Permissions.HostProtection(MayLeakOnAbort = true)]
   public class TimeZoneNotFoundException : Exception { 
       public TimeZoneNotFoundException(String message)
           : base(message) { } 
 
       public TimeZoneNotFoundException(String message, Exception innerException)
           : base(message, innerException) { } 

       [System.Security.SecuritySafeCritical]  // auto-generated
       protected TimeZoneNotFoundException(SerializationInfo info, StreamingContext context)
           : base(info, context) { } 

       public TimeZoneNotFoundException() { } 
   } 
}

