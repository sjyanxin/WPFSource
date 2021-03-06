//------------------------------------------------------------------------------ 
// <copyright file="SafeNativeMethodsMilCoreApi.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// 
//  ABOUT THIS FILE:
//   -- This file contains native methods which are deemed SAFE for partial trust callers 
//   -- These methods DO have the SuppressUnmanagedCodeSecurity attribute which means 
//       stalk walks for unmanaged
//      code will stop with the immediate caler. 
//   -- Put methods in here which are needed in partial trust scenarios
//   -- If you have questions about how to use this file, email avsee
//-----------------------------------------------------------------------------
 
namespace MS.Win32.PresentationCore
{ 
    using System.Runtime.InteropServices; 
    using System.Runtime.InteropServices.ComTypes;
    using System; 
    using System.Security;
    using System.Security.Permissions;
    using System.Collections;
    using System.IO; 
    using System.Text;
    using System.Windows.Media.Composition; 
    using Microsoft.Internal; 

    using IComDataObject = System.Runtime.InteropServices.ComTypes.IDataObject; 

    internal static partial class SafeNativeMethods
    {
       ///<SecurityNote> 
       ///  TreatAsSafe: The security model here is that these APIs could be publicly exposed to partial trust
       ///               callers - no risk. 
       ///  Critical: This code elevates priviliges by adding a SuppressUnmanagedCodeSecurity 
       ///</SecurityNote>
       [SecurityCritical, SecurityTreatAsSafe] 
       internal static int MilCompositionEngine_InitializePartitionManager(int nPriority)
       {
            return SafeNativeMethodsPrivate.MilCompositionEngine_InitializePartitionManager(nPriority);
       } 

       ///<SecurityNote> 
       ///  TreatAsSafe: The security model here is that these APIs could be publicly exposed to partial trust 
       ///               callers - no risk.
       ///  Critical: This code elevates priviliges by adding a SuppressUnmanagedCodeSecurity 
       ///</SecurityNote>
       [SecurityCritical, SecurityTreatAsSafe]
       internal static int MilCompositionEngine_DeinitializePartitionManager()
       { 
            return SafeNativeMethodsPrivate.MilCompositionEngine_DeinitializePartitionManager();
       } 
 
       [SecurityCritical, SecurityTreatAsSafe]
       internal static long GetNextPerfElementId() 
       {
           return SafeNativeMethodsPrivate.GetNextPerfElementId();
       }
 
       /// <SecurityNote>
       ///  Critical - Uses SuppressUnmanagedCodeSecurityAttribute. 
       /// </SecurityNote> 
       [SuppressUnmanagedCodeSecurity, SecurityCritical(SecurityCriticalScope.Everything)]
       private static partial class SafeNativeMethodsPrivate 
       {
            [DllImport(DllImport.MilCore)]
            internal static extern int MilCompositionEngine_InitializePartitionManager(int nPriority);
 
            [DllImport(DllImport.MilCore)]
            internal static extern int MilCompositionEngine_DeinitializePartitionManager(); 
 
            [DllImport(DllImport.MilCore)]
            internal static extern long GetNextPerfElementId(); 
       }
    }
}
 

