//------------------------------------------------------------------------------ 
//
// <copyright file="NativeRightsManagementAPIsStructures.cs" company="Microsoft">
//    Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright> 
//
// Description: 
//  These are the Structure declarations for interop services required to call into unmanaged 
//  Promethium Rights Management SDK APIs
// 
// History:
//  06/13/2005: IgorBel:  Initial implementation.
//
//----------------------------------------------------------------------------- 

#define PRESENTATION_HOST_DLL 
// for "PresentationHostDLL.dll" 

 
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Security; 

namespace MS.Internal.Security.RightsManagement 
{ 

    /// <SecurityNote> 
    ///     Critical:  This class server as a wrapper on top of private class UnsafeNativeMethods.
    ///     UnsafeNativeMethods has suppress unamanged code attribute set.
    ///     It is up to this class to ensure that the only calls that can go through must beeither done in Full Trust
    ///     or with RightsManagementPermission. This class exposes DRMFoo functions that perform demand on the 
    ///     RightsManagementPermission and then call through to the matching member of the Private Static class
    ///     UnsafeNativeMethods 
    /// </SecurityNote> 
    [SecurityCritical(SecurityCriticalScope.Everything)]
    [StructLayout(LayoutKind.Sequential)] 
    internal class ActivationServerInfo
    {
        public uint    Version;
        [MarshalAs( UnmanagedType.LPWStr )]internal string  PubKey = ""; 
        [MarshalAs( UnmanagedType.LPWStr )]internal string  Url  = "";
    } 
 
    /// <SecurityNote>
    ///     Critical:  This class server as a wrapper on top of private class UnsafeNativeMethods. 
    ///     UnsafeNativeMethods has suppress unamanged code attribute set.
    ///     It is up to this class to ensure that the only calls that can go through must beeither done in Full Trust
    ///     or with RightsManagementPermission. This class exposes DRMFoo functions that perform demand on the
    ///     RightsManagementPermission and then call through to the matching member of the Private Static class 
    ///     UnsafeNativeMethods
    /// </SecurityNote> 
    [SecurityCritical(SecurityCriticalScope.Everything)] 
    // Declare a class to represent unmanaged SYSTEMTIME structure expected by DRM SDK
    [ StructLayout( LayoutKind.Sequential )] 
    internal class SystemTime
    {
        internal SystemTime (DateTime dateTime)
        { 
            Year = (ushort)dateTime.Year;
            Month =  (ushort)dateTime.Month; 
            DayOfWeek =  (ushort)dateTime.DayOfWeek; 
            Day =  (ushort)dateTime.Day;
            Hour =  (ushort)dateTime.Hour; 
            Minute =  (ushort)dateTime.Minute;
            Second =  (ushort)dateTime.Second;
            Milliseconds =  (ushort)dateTime.Millisecond;
        } 

        static internal uint Size 
        { 
            get
            { 
                return 8 * sizeof(short);
            }
        }
 
        // construct it from memory buffer
        internal SystemTime(byte[]  dataBuffer) 
        { 
            Year = BitConverter.ToUInt16(dataBuffer,0);
            Month = BitConverter.ToUInt16(dataBuffer,2); 
            DayOfWeek = BitConverter.ToUInt16(dataBuffer,4);
            Day = BitConverter.ToUInt16(dataBuffer,6);
            Hour = BitConverter.ToUInt16(dataBuffer,8);
            Minute = BitConverter.ToUInt16(dataBuffer,10); 
            Second = BitConverter.ToUInt16(dataBuffer,12);
            Milliseconds = BitConverter.ToUInt16(dataBuffer,14); 
        } 

        internal DateTime GetDateTime (DateTime defaultValue) 
        {
            // It seems that unmanaged APIs use the all 0s values to indicate
            // that Date Time isn't present
            if ((Year == 0) && 
                (Month == 0) &&
                (Day == 0) && 
                (Hour == 0) && 
                (Minute == 0) &&
                (Second == 0) && 
                (Milliseconds == 0))
            {
                return defaultValue;
            } 
            else
            { 
                return new DateTime(Year, Month, Day, 
                        Hour, Minute, Second, Milliseconds);
            } 
        }

       ushort Year =0;
       ushort Month =0; 
       ushort DayOfWeek =0;
       ushort Day =0; 
       ushort Hour =0; 
       ushort Minute =0;
       ushort Second =0; 
       ushort Milliseconds =0;
    }

    /// <SecurityNote> 
    ///     Critical:  This class server as a wrapper on top of private class UnsafeNativeMethods.
    ///     UnsafeNativeMethods has suppress unamanged code attribute set. 
    ///     It is up to this class to ensure that the only calls that can go through must beeither done in Full Trust 
    ///     or with RightsManagementPermission. This class exposes DRMFoo functions that perform demand on the
    ///     RightsManagementPermission and then call through to the matching member of the Private Static class 
    ///     UnsafeNativeMethods
    /// </SecurityNote>
    [SecurityCritical(SecurityCriticalScope.Everything)]
    [StructLayout(LayoutKind.Sequential)] 
    internal class BoundLicenseParams
    { 
        internal uint     uVersion = 0; 
        internal uint     hEnablingPrincipal = 0;
        internal uint     hSecureStore = 0; 
        [MarshalAs( UnmanagedType.LPWStr )]public string  wszRightsRequested = null;
        [MarshalAs( UnmanagedType.LPWStr )]public string  wszRightsGroup = null;

        //Actual members of DRMID 
        internal uint     DRMIDuVersion = 0;
        [MarshalAs( UnmanagedType.LPWStr )]public string  DRMIDIdType = null; 
        [MarshalAs( UnmanagedType.LPWStr )]public string  DRMIDId = null; 

        internal uint     cAuthenticatorCount = 0;//reserved.should be 0. 

        internal IntPtr rghAuthenticators = IntPtr.Zero;

        [MarshalAs( UnmanagedType.LPWStr )]public string  wszDefaultEnablingPrincipalCredentials = null; 
        internal uint     dwFlags = 0;
    } 
} 
