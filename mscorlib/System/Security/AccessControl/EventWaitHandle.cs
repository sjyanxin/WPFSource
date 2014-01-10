// ==++== 
//
//   Copyright (c) Microsoft Corporation.  All rights reserved.
//
// ==--== 
//
// <OWNER>[....]</OWNER> 
/*============================================================================== 
**
** Class: EventWaitHandle 
**
**
** Purpose: Base class for representing Events
** 
**
=============================================================================*/ 
 

#if FEATURE_PAL || !FEATURE_MACL 
namespace System.Security.AccessControl
{
    public class EventWaitHandleSecurity
    { 
    }
    public enum EventWaitHandleRights 
    { 
    }
} 
#endif

namespace System.Threading
{ 
    using System;
    using System.Threading; 
    using System.Runtime.CompilerServices; 
    using System.Security.Permissions;
    using System.IO; 
    using Microsoft.Win32;
    using Microsoft.Win32.SafeHandles;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning; 
    using System.Security.AccessControl;
    using System.Diagnostics.Contracts; 
 
    [HostProtection(Synchronization=true, ExternalThreading=true)]
    [ComVisibleAttribute(true)] 
    public class EventWaitHandle : WaitHandle
    {
        [System.Security.SecuritySafeCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)] 
        [ResourceConsumption(ResourceScope.Machine, ResourceScope.Machine)]
        public EventWaitHandle(bool initialState, EventResetMode mode) : this(initialState,mode,null) { } 
 
        [System.Security.SecurityCritical]  // auto-generated_required
        [ResourceExposure(ResourceScope.Machine)] 
        [ResourceConsumption(ResourceScope.Machine)]
        public EventWaitHandle(bool initialState, EventResetMode mode, string name)
        {
            if(null != name && System.IO.Path.MAX_PATH < name.Length) 
            {
                throw new ArgumentException(Environment.GetResourceString("Argument_WaitHandleNameTooLong",name)); 
            } 
            Contract.EndContractBlock();
 
            SafeWaitHandle _handle = null;
            switch(mode)
            {
                case EventResetMode.ManualReset: 
                    _handle = Win32Native.CreateEvent(null, true, initialState, name);
                    break; 
                case EventResetMode.AutoReset: 
                    _handle = Win32Native.CreateEvent(null, false, initialState, name);
                    break; 

                default:
                    throw new ArgumentException(Environment.GetResourceString("Argument_InvalidFlag",name));
            }; 

            if (_handle.IsInvalid) 
            { 
                int errorCode = Marshal.GetLastWin32Error();
 
                _handle.SetHandleAsInvalid();
                if(null != name && 0 != name.Length && Win32Native.ERROR_INVALID_HANDLE == errorCode)
                    throw new WaitHandleCannotBeOpenedException(Environment.GetResourceString("Threading.WaitHandleCannotBeOpenedException_InvalidHandle",name));
 
                __Error.WinIOError(errorCode, name);
            } 
            SetHandleInternal(_handle); 
        }
 
        [System.Security.SecurityCritical]  // auto-generated_required
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        public EventWaitHandle(bool initialState, EventResetMode mode, string name, out bool createdNew) 
            : this(initialState, mode, name, out createdNew, null)
        { 
        } 

        [System.Security.SecurityCritical]  // auto-generated_required 
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        public unsafe EventWaitHandle(bool initialState, EventResetMode mode, string name, out bool createdNew, EventWaitHandleSecurity eventSecurity)
        { 
            if(null != name && System.IO.Path.MAX_PATH < name.Length)
            { 
                throw new ArgumentException(Environment.GetResourceString("Argument_WaitHandleNameTooLong",name)); 
            }
            Contract.EndContractBlock(); 
            Win32Native.SECURITY_ATTRIBUTES secAttrs = null;
#if !FEATURE_PAL && FEATURE_MACL
            // For ACL's, get the security descriptor from the EventWaitHandleSecurity.
            if (eventSecurity != null) { 
                secAttrs = new Win32Native.SECURITY_ATTRIBUTES();
                secAttrs.nLength = (int)Marshal.SizeOf(secAttrs); 
 
                byte[] sd = eventSecurity.GetSecurityDescriptorBinaryForm();
                byte* pSecDescriptor = stackalloc byte[sd.Length]; 
                Buffer.memcpy(sd, 0, pSecDescriptor, 0, sd.Length);
                secAttrs.pSecurityDescriptor = pSecDescriptor;
            }
#endif 

            SafeWaitHandle _handle = null; 
            Boolean isManualReset; 
            switch(mode)
            { 
                case EventResetMode.ManualReset:
                    isManualReset = true;
                    break;
                case EventResetMode.AutoReset: 
                    isManualReset = false;
                    break; 
 
                default:
                    throw new ArgumentException(Environment.GetResourceString("Argument_InvalidFlag",name)); 
            };

            _handle = Win32Native.CreateEvent(secAttrs, isManualReset, initialState, name);
            int errorCode = Marshal.GetLastWin32Error(); 

            if (_handle.IsInvalid) 
            { 

                _handle.SetHandleAsInvalid(); 
                if(null != name && 0 != name.Length && Win32Native.ERROR_INVALID_HANDLE == errorCode)
                    throw new WaitHandleCannotBeOpenedException(Environment.GetResourceString("Threading.WaitHandleCannotBeOpenedException_InvalidHandle",name));

                __Error.WinIOError(errorCode, name); 
            }
            createdNew = errorCode != Win32Native.ERROR_ALREADY_EXISTS; 
            SetHandleInternal(_handle); 
        }
 
        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        private EventWaitHandle(SafeWaitHandle handle) 
        {
            SetHandleInternal(handle); 
        } 

        [System.Security.SecurityCritical]  // auto-generated_required 
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        public static EventWaitHandle OpenExisting(string name)
        { 
#if FEATURE_PAL || !FEATURE_MACL
            return OpenExisting(name, (EventWaitHandleRights)0); 
#else 
            return OpenExisting(name, EventWaitHandleRights.Modify | EventWaitHandleRights.Synchronize);
#endif  // !FEATURE_PAL 
        }

        [System.Security.SecurityCritical]  // auto-generated_required
        [ResourceExposure(ResourceScope.Machine)] 
        [ResourceConsumption(ResourceScope.Machine)]
        public static EventWaitHandle OpenExisting(string name, EventWaitHandleRights rights) 
        { 
            if (name == null)
            { 
                throw new ArgumentNullException("name", Environment.GetResourceString("ArgumentNull_WithParamName"));
            }

            if(name.Length  == 0) 
            {
                throw new ArgumentException(Environment.GetResourceString("Argument_EmptyName"), "name"); 
            } 

            if(null != name && System.IO.Path.MAX_PATH < name.Length) 
            {
                throw new ArgumentException(Environment.GetResourceString("Argument_WaitHandleNameTooLong",name));
            }
 
            Contract.EndContractBlock();
 
#if !FEATURE_PAL && FEATURE_MACL 
            SafeWaitHandle myHandle = Win32Native.OpenEvent((int) rights, false, name);
#else 
            SafeWaitHandle myHandle = Win32Native.OpenEvent(Win32Native.EVENT_MODIFY_STATE | Win32Native.SYNCHRONIZE, false, name);
#endif

            if (myHandle.IsInvalid) 
            {
                int errorCode = Marshal.GetLastWin32Error(); 
 
                if(Win32Native.ERROR_FILE_NOT_FOUND == errorCode || Win32Native.ERROR_INVALID_NAME == errorCode)
                    throw new WaitHandleCannotBeOpenedException(); 
                if(null != name && 0 != name.Length && Win32Native.ERROR_INVALID_HANDLE == errorCode)
                    throw new WaitHandleCannotBeOpenedException(Environment.GetResourceString("Threading.WaitHandleCannotBeOpenedException_InvalidHandle",name));
                //this is for passed through Win32Native Errors
                __Error.WinIOError(errorCode,""); 
            }
            return new EventWaitHandle(myHandle); 
        } 
        [System.Security.SecuritySafeCritical]  // auto-generated
        public bool Reset() 
        {
            bool res = Win32Native.ResetEvent(safeWaitHandle);
            if (!res)
                __Error.WinIOError(); 
            return res;
        } 
        [System.Security.SecuritySafeCritical]  // auto-generated 
        public bool Set()
        { 
            bool res = Win32Native.SetEvent(safeWaitHandle);

            if (!res)
                __Error.WinIOError(); 

            return res; 
        } 

#if !FEATURE_PAL && FEATURE_MACL 
        [System.Security.SecuritySafeCritical]  // auto-generated
        public EventWaitHandleSecurity GetAccessControl()
        {
            return new EventWaitHandleSecurity(safeWaitHandle, AccessControlSections.Access | AccessControlSections.Owner | AccessControlSections.Group); 
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated 
        public void SetAccessControl(EventWaitHandleSecurity eventSecurity)
        { 
            if (eventSecurity == null)
                throw new ArgumentNullException("eventSecurity");
            Contract.EndContractBlock();
 
            eventSecurity.Persist(safeWaitHandle);
        } 
#endif 
    }
} 


