//------------------------------------------------------------------------------ 
// <copyright file="etwprovider.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <OWNER>[....]</OWNER> 
//-----------------------------------------------------------------------------
using System; 
using System.Runtime.InteropServices; 
using System.Runtime.CompilerServices;
using Microsoft.Win32; 
using System.Globalization;
using System.Collections.Generic;
using System.Threading;
using System.Security.Permissions; 
using System.Security;
using System.Diagnostics.CodeAnalysis; 
 
namespace System.Diagnostics.Eventing
{ 
    // New in CLR4.0
    internal enum ControllerCommand
    {
        // Strictly Positive numbers are for provider-specific commands, negative number are for 'shared' commands. 256 
        // The first 256 negative numbers are reserved for the framework.
        Update = 0, 
        SendManifest = -1, 
    };
 
    [System.Security.Permissions.HostProtection(MayLeakOnAbort = true)]
    internal partial class EventProvider : IDisposable
    {
        // This is the windows EVENT_DATA_DESCRIPTOR structure.  We expose it because this is what 
        // subclasses of EventProvider use when creating efficient (but unsafe) version of
        // EventWrite.   We do make it a nested type because we really don't expect anyone to use 
        // it except subclasses (and then only rarely). 
        public struct EventData
        { 
            internal unsafe ulong Ptr;
            internal uint Size;
            internal uint Reserved;
        } 

        [System.Security.SecuritySafeCritical] 
        ManifestEtw.EtwEnableCallback m_etwCallback;     // Trace Callback function 

        private long m_regHandle;                        // Trace Registration Handle 
        private byte m_level;                            // Tracing Level
        private long m_anyKeywordMask;                   // Trace Enable Flags
        private long m_allKeywordMask;                   // Match all keyword
        private int m_enabled;                           // Enabled flag from Trace callback 
        private Guid m_providerId;                       // Control Guid
        private int m_disposed;                          // when 1, provider has unregister 
        private bool m_isClassic;                        // Should we use classic (XP) ETW APIs (more efficient than s_isClassic) 
        private static bool s_isClassic;                 // same as m_isClassic for static APIs.
 
        [ThreadStatic]
        private static WriteEventErrorCode s_returnCode; // The last return code

        private const int s_basicTypeAllocationBufferSize = 16; 
        private const int s_etwMaxMumberArguments = 32;
        private const int s_etwAPIMaxStringCount = 8; 
        private const int s_maxEventDataDescriptors = 128; 
        private const int s_traceEventMaximumSize = 65482;
        private const int s_traceEventMaximumStringSize = 32724; 

        [SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible")]
        public enum WriteEventErrorCode : int
        { 
            //check mapping to runtime codes
            NoError = 0, 
            NoFreeBuffers = 1, 
            EventTooBig = 2
        } 

        private enum ActivityControl : uint
        {
            EVENT_ACTIVITY_CTRL_GET_ID = 1, 
            EVENT_ACTIVITY_CTRL_SET_ID = 2,
            EVENT_ACTIVITY_CTRL_CREATE_ID = 3, 
            EVENT_ACTIVITY_CTRL_GET_SET_ID = 4, 
            EVENT_ACTIVITY_CTRL_CREATE_SET_ID = 5
        } 

        // <SecurityKernel Critical="True" Ring="1">
        // <ReferencesCritical Name="Method: Register():Void" Ring="1" />
        // </SecurityKernel> 
        /// <summary>
        /// Constructs a new EventProvider.  This causes the class to be registered with the OS an 
        /// if a ETW controller turns on the logging then logging will start. 
        /// </summary>
        /// <param name="providerGuid">The GUID that identifies this provider to the system.</param> 
        [System.Security.SecurityCritical]
        [PermissionSet(SecurityAction.Demand, Unrestricted = true)]
        [SuppressMessage("Microsoft.Naming", "CA1720:IdentifiersShouldNotContainTypeNames", MessageId = "guid")]
        protected EventProvider(Guid providerGuid) 
        {
            m_providerId = providerGuid; 
            s_isClassic = m_isClassic = true; 
            //
            // Register the ProviderId with ETW 
            //
            Register(providerGuid);
        }
 
        internal EventProvider()
        { 
            s_isClassic = m_isClassic = true; 
        }
 
        /// <summary>
        /// This method registers the controlGuid of this class with ETW.
        /// We need to be running on Vista or above. If not an
        /// PlatformNotSupported exception will be thrown. 
        /// If for some reason the ETW Register call failed
        /// a NotSupported exception will be thrown. 
        /// </summary> 
        // <SecurityKernel Critical="True" Ring="0">
        // <CallsSuppressUnmanagedCode Name="ManifestEtw.EventRegister(System.Guid&,Microsoft.Win32.ManifestEtw+EtwEnableCallback,System.Void*,System.Int64&):System.UInt32" /> 
        // <SatisfiesLinkDemand Name="Win32Exception..ctor(System.Int32)" />
        // <ReferencesCritical Name="Method: EtwEnableCallBack(Guid&, Int32, Byte, Int64, Int64, Void*, Void*):Void" Ring="1" />
        // </SecurityKernel>
        [System.Security.SecurityCritical] 
        internal unsafe void Register(Guid providerGuid)
        { 
            m_providerId = providerGuid; 
            uint status;
            m_etwCallback = new ManifestEtw.EtwEnableCallback(EtwEnableCallBack); 

            status = EventRegister(ref m_providerId, m_etwCallback);
            if (status != 0)
            { 
                throw new ArgumentException(Win32Native.GetMessage(unchecked((int)status)));
            } 
        } 

        // 
        // implement Dispose Pattern to early deregister from ETW insted of waiting for
        // the finalizer to call deregistration.
        // Once the user is done with the provider it needs to call Close() or Dispose()
        // If neither are called the finalizer will unregister the provider anyway 
        //
        public void Dispose() 
        { 
            Dispose(true);
            GC.SuppressFinalize(this); 
        }

        // <SecurityKernel Critical="True" TreatAsSafe="Does not expose critical resource" Ring="1">
        // <ReferencesCritical Name="Method: Deregister():Void" Ring="1" /> 
        // </SecurityKernel>
        [System.Security.SecuritySafeCritical] 
        protected virtual void Dispose(bool disposing) 
        {
            // 
            // explicit cleanup is done by calling Dispose with true from
            // Dispose() or Close(). The disposing arguement is ignored because there
            // are no unmanaged resources.
            // The finalizer calls Dispose with false. 
            //
 
            // 
            // check if the object has been allready disposed
            // 
            if (m_disposed == 1) return;

            if (Interlocked.Exchange(ref m_disposed, 1) != 0)
            { 
                // somebody is allready disposing the provider
                return; 
            } 

            // 
            // Disables Tracing in the provider, then unregister
            //

            m_enabled = 0; 

            Deregister(); 
        } 

        /// <summary> 
        /// This method deregisters the controlGuid of this class with ETW.
        ///
        /// </summary>
        public virtual void Close() 
        {
            Dispose(); 
        } 

        ~EventProvider() 
        {
            Dispose(false);
        }
 
        /// <summary>
        /// This method un-registers from ETW. 
        /// </summary> 
        // <SecurityKernel Critical="True" Ring="0">
        // <CallsSuppressUnmanagedCode Name="ManifestEtw.EventUnregister(System.Int64):System.Int32" /> 
        // </SecurityKernel>
        //
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1806:DoNotIgnoreMethodResults", MessageId = "Microsoft.Win32.ManifestEtw.EventUnregister(System.Int64)"), System.Security.SecurityCritical]
        private unsafe void Deregister() 
        {
            // 
            // Unregister from ETW using the RegHandle saved from 
            // the register call.
            // 

            if (m_regHandle != 0)
            {
                EventUnregister(); 
                m_regHandle = 0;
            } 
        } 

        // <SecurityKernel Critical="True" Ring="0"> 
        // <UsesUnsafeCode Name="Parameter filterData of type: Void*" />
        // <UsesUnsafeCode Name="Parameter callbackContext of type: Void*" />
        // </SecurityKernel>
        [System.Security.SecurityCritical] 
        unsafe void EtwEnableCallBack(
                        [In] ref System.Guid sourceId, 
                        [In] int isEnabled, 
                        [In] byte setLevel,
                        [In] long anyKeyword, 
                        [In] long allKeyword,
                        [In] ManifestEtw.EVENT_FILTER_DESCRIPTOR* filterData,
                        [In] void* callbackContext
                        ) 
        {
            m_enabled = isEnabled; 
            m_level = setLevel; 
            m_anyKeywordMask = anyKeyword;
            m_allKeywordMask = allKeyword; 
            ControllerCommand command = Eventing.ControllerCommand.Update;
            IDictionary<string, string> args = null;

            byte[] data; 
            int keyIndex;
            if (GetDataFromController(filterData, out command, out data, out keyIndex)) 
            { 
                args = new Dictionary<string, string>(4);
                while (keyIndex < data.Length) 
                {
                    int keyEnd = FindNull(data, keyIndex);
                    int valueIdx = keyEnd + 1;
                    int valueEnd = FindNull(data, valueIdx); 
                    if (valueEnd < data.Length)
                    { 
                        string key = System.Text.Encoding.UTF8.GetString(data, keyIndex, keyEnd - keyIndex); 
                        string value = System.Text.Encoding.UTF8.GetString(data, valueIdx, valueEnd - valueIdx);
                        args[key] = value; 
                    }
                    keyIndex = valueEnd + 1;
                }
            } 
            OnControllerCommand(command, args);
        } 
 
        // New in CLR4.0
        protected virtual void OnControllerCommand(ControllerCommand command, IDictionary<string, string> arguments) { } 
        protected EventLevel Level { get { return (EventLevel)m_level; } set { m_level = (byte)value; } }
        protected EventKeywords MatchAnyKeyword { get { return (EventKeywords)m_anyKeywordMask; } set { m_anyKeywordMask = (long)value; } }
        protected EventKeywords MatchAllKeyword { get { return (EventKeywords)m_allKeywordMask; } set { m_allKeywordMask = (long)value; } }
 
        static private int FindNull(byte[] buffer, int idx)
        { 
            while (idx < buffer.Length && buffer[idx] != 0) 
                idx++;
            return idx; 
        }

        /// <summary>
        /// Gets any data to be passed from the controller to the provider.  It starts with what is passed 
        /// into the callback, but unfortunately this data is only present for when the provider is active
        /// at the the time the controller issues the command.  To allow for providers to activate after the 
        /// controller issued a command, we also check the registry and use that to get the data.  The function 
        /// returns an array of bytes representing the data, the index into that byte array where the data
        /// starts, and the command being issued associated with that data. 
        /// </summary>
        [System.Security.SecurityCritical]
        private unsafe bool GetDataFromController(ManifestEtw.EVENT_FILTER_DESCRIPTOR* filterData, out ControllerCommand command, out byte[] data, out int dataStart)
        { 
            data = null;
            if (filterData == null) 
            { 
                string regKey = @"\Microsoft\Windows\CurrentVersion\Winevt\Publishers\{" + m_providerId + "}";
                if (System.Runtime.InteropServices.Marshal.SizeOf(typeof(IntPtr)) == 8) 
                    regKey = @"HKEY_LOCAL_MACHINE\Software" + @"\Wow6432Node" + regKey;
                else
                    regKey = @"HKEY_LOCAL_MACHINE\Software" + regKey;
 

                data = Microsoft.Win32.Registry.GetValue(regKey, "ControllerData", null) as byte[]; 
                if (data != null && data.Length >= 4) 
                {
                    // 
                    command = (ControllerCommand)(((data[3] << 8 + data[2]) << 8 + data[1]) << 8 + data[0]);
                    dataStart = 4;
                    return true;
                } 
            }
            else 
            { 
                if (filterData->Ptr != 0 && 0 < filterData->Size && filterData->Size <= 1024)
                { 
                    data = new byte[filterData->Size];
                    Marshal.Copy((IntPtr)filterData->Ptr, data, 0, data.Length);
                }
                command = (ControllerCommand) filterData->Type; 
                dataStart = 0;
                return true; 
            } 

            dataStart = 0; 
            command = ControllerCommand.Update;
            return false;
        }
 
        /// <summary>
        /// IsEnabled, method used to test if provider is enabled 
        /// </summary> 
        public bool IsEnabled()
        { 
            return (m_enabled != 0) ? true : false;
        }

        /// <summary> 
        /// IsEnabled, method used to test if event is enabled
        /// </summary> 
        /// <param name="Lvl"> 
        /// Level  to test
        /// </param> 
        /// <param name="Keyword">
        /// Keyword  to test
        /// </param>
        public bool IsEnabled(byte level, long keywords) 
        {
            // 
            // If not enabled at all, return false. 
            //
            if (m_enabled == 0) 
            {
                return false;
            }
 
            // This also covers the case of Level == 0.
            if ((level <= m_level) || 
                (m_level == 0)) 
            {
 
                //
                // Check if Keyword is enabled
                //
 
                if ((keywords == 0) ||
                    (((keywords & m_anyKeywordMask) != 0) && 
                     ((keywords & m_allKeywordMask) == m_allKeywordMask))) 
                {
                    return true; 
                }
            }

            return false; 
        }
 
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate")] 
        public static WriteEventErrorCode GetLastWriteEventError()
        { 
            return s_returnCode;
        }

        // 
        // Helper function to set the last error on the thread
        // 
        private static void SetLastError(int error) 
        {
            switch (error) 
            {
                case ManifestEtw.ERROR_ARITHMETIC_OVERFLOW:
                case ManifestEtw.ERROR_MORE_DATA:
                    s_returnCode = WriteEventErrorCode.EventTooBig; 
                    break;
                case ManifestEtw.ERROR_NOT_ENOUGH_MEMORY: 
                    s_returnCode = WriteEventErrorCode.NoFreeBuffers; 
                    break;
            } 
        }


        // <SecurityKernel Critical="True" Ring="0"> 
        // <UsesUnsafeCode Name="Local intptrPtr of type: IntPtr*" />
        // <UsesUnsafeCode Name="Local intptrPtr of type: Int32*" /> 
        // <UsesUnsafeCode Name="Local longptr of type: Int64*" /> 
        // <UsesUnsafeCode Name="Local uintptr of type: UInt32*" />
        // <UsesUnsafeCode Name="Local ulongptr of type: UInt64*" /> 
        // <UsesUnsafeCode Name="Local charptr of type: Char*" />
        // <UsesUnsafeCode Name="Local byteptr of type: Byte*" />
        // <UsesUnsafeCode Name="Local shortptr of type: Int16*" />
        // <UsesUnsafeCode Name="Local sbyteptr of type: SByte*" /> 
        // <UsesUnsafeCode Name="Local ushortptr of type: UInt16*" />
        // <UsesUnsafeCode Name="Local floatptr of type: Single*" /> 
        // <UsesUnsafeCode Name="Local doubleptr of type: Double*" /> 
        // <UsesUnsafeCode Name="Local boolptr of type: Boolean*" />
        // <UsesUnsafeCode Name="Local guidptr of type: Guid*" /> 
        // <UsesUnsafeCode Name="Local decimalptr of type: Decimal*" />
        // <UsesUnsafeCode Name="Local booleanptr of type: Boolean*" />
        // <UsesUnsafeCode Name="Parameter dataDescriptor of type: EventData*" />
        // <UsesUnsafeCode Name="Parameter dataBuffer of type: Byte*" /> 
        // </SecurityKernel>
        [System.Security.SecurityCritical] 
        private static unsafe string EncodeObject(ref object data, EventData* dataDescriptor, byte* dataBuffer) 
        /*++
 
        Routine Description:

           This routine is used by WriteEvent to unbox the object type and
           to fill the passed in ETW data descriptor. 

        Arguments: 
 
           data - argument to be decoded
 
           dataDescriptor - pointer to the descriptor to be filled

           dataBuffer - storage buffer for storing user data, needed because cant get the address of the object
 
        Return Value:
 
           null if the object is a basic type other than string. String otherwise 

        --*/ 
        {
            dataDescriptor->Reserved = 0;

            string sRet = data as string; 
            if (sRet != null)
            { 
                dataDescriptor->Size = (uint)((sRet.Length + 1) * 2); 
                return sRet;
            } 

            if (data is IntPtr)
            {
                dataDescriptor->Size = (uint)sizeof(IntPtr); 
                IntPtr* intptrPtr = (IntPtr*)dataBuffer;
                *intptrPtr = (IntPtr)data; 
                dataDescriptor->Ptr = (ulong)intptrPtr; 
            }
            else if (data is int) 
            {
                dataDescriptor->Size = (uint)sizeof(int);
                int* intptrPtr = (int*)dataBuffer;
                *intptrPtr = (int)data; 
                dataDescriptor->Ptr = (ulong)intptrPtr;
            } 
            else if (data is long) 
            {
                dataDescriptor->Size = (uint)sizeof(long); 
                long* longptr = (long*)dataBuffer;
                *longptr = (long)data;
                dataDescriptor->Ptr = (ulong)longptr;
            } 
            else if (data is uint)
            { 
                dataDescriptor->Size = (uint)sizeof(uint); 
                uint* uintptr = (uint*)dataBuffer;
                *uintptr = (uint)data; 
                dataDescriptor->Ptr = (ulong)uintptr;
            }
            else if (data is UInt64)
            { 
                dataDescriptor->Size = (uint)sizeof(ulong);
                UInt64* ulongptr = (ulong*)dataBuffer; 
                *ulongptr = (ulong)data; 
                dataDescriptor->Ptr = (ulong)ulongptr;
            } 
            else if (data is char)
            {
                dataDescriptor->Size = (uint)sizeof(char);
                char* charptr = (char*)dataBuffer; 
                *charptr = (char)data;
                dataDescriptor->Ptr = (ulong)charptr; 
            } 
            else if (data is byte)
            { 
                dataDescriptor->Size = (uint)sizeof(byte);
                byte* byteptr = (byte*)dataBuffer;
                *byteptr = (byte)data;
                dataDescriptor->Ptr = (ulong)byteptr; 
            }
            else if (data is short) 
            { 
                dataDescriptor->Size = (uint)sizeof(short);
                short* shortptr = (short*)dataBuffer; 
                *shortptr = (short)data;
                dataDescriptor->Ptr = (ulong)shortptr;
            }
            else if (data is sbyte) 
            {
                dataDescriptor->Size = (uint)sizeof(sbyte); 
                sbyte* sbyteptr = (sbyte*)dataBuffer; 
                *sbyteptr = (sbyte)data;
                dataDescriptor->Ptr = (ulong)sbyteptr; 
            }
            else if (data is ushort)
            {
                dataDescriptor->Size = (uint)sizeof(ushort); 
                ushort* ushortptr = (ushort*)dataBuffer;
                *ushortptr = (ushort)data; 
                dataDescriptor->Ptr = (ulong)ushortptr; 
            }
            else if (data is float) 
            {
                dataDescriptor->Size = (uint)sizeof(float);
                float* floatptr = (float*)dataBuffer;
                *floatptr = (float)data; 
                dataDescriptor->Ptr = (ulong)floatptr;
            } 
            else if (data is double) 
            {
                dataDescriptor->Size = (uint)sizeof(double); 
                double* doubleptr = (double*)dataBuffer;
                *doubleptr = (double)data;
                dataDescriptor->Ptr = (ulong)doubleptr;
            } 
            else if (data is bool)
            { 
                dataDescriptor->Size = (uint)sizeof(bool); 
                bool* boolptr = (bool*)dataBuffer;
                *boolptr = (bool)data; 
                dataDescriptor->Ptr = (ulong)boolptr;
            }
            else if (data is Guid)
            { 
                dataDescriptor->Size = (uint)sizeof(Guid);
                Guid* guidptr = (Guid*)dataBuffer; 
                *guidptr = (Guid)data; 
                dataDescriptor->Ptr = (ulong)guidptr;
            } 
            else if (data is decimal)
            {
                dataDescriptor->Size = (uint)sizeof(decimal);
                decimal* decimalptr = (decimal*)dataBuffer; 
                *decimalptr = (decimal)data;
                dataDescriptor->Ptr = (ulong)decimalptr; 
            } 
            else if (data is Boolean)
            { 
                dataDescriptor->Size = (uint)sizeof(Boolean);
                Boolean* booleanptr = (Boolean*)dataBuffer;
                *booleanptr = (Boolean)data;
                dataDescriptor->Ptr = (ulong)booleanptr; 
            }
            else 
            { 
                //To our eyes, everything else is a just a string
                sRet = data.ToString(); 
                dataDescriptor->Size = (uint)((sRet.Length + 1) * 2);
                return sRet;
            }
 
            return null;
        } 
 

        /// <summary> 
        /// WriteMessageEvent, method to write a string with level and Keyword
        /// </summary>
        /// <param name="level">
        /// Level  to test 
        /// </param>
        /// <param name="Keyword"> 
        /// Keyword  to test 
        /// </param>
        // <SecurityKernel Critical="True" Ring="0"> 
        // <CallsSuppressUnmanagedCode Name="ManifestEtw.EventWriteString(System.Int64,System.Byte,System.Int64,System.Char*):System.UInt32" />
        // <UsesUnsafeCode Name="Local pdata of type: Char*" />
        // </SecurityKernel>
        [System.Security.SecuritySafeCritical] 
        public bool WriteMessageEvent(string eventMessage, byte eventLevel, long eventKeywords)
        { 
            int status = 0; 

            if (eventMessage == null) 
            {
                throw new ArgumentNullException("eventMessage");
            }
 
            if (IsEnabled(eventLevel, eventKeywords))
            { 
                if (eventMessage.Length > s_traceEventMaximumStringSize) 
                {
                    s_returnCode = WriteEventErrorCode.EventTooBig; 
                    return false;
                }
                unsafe
                { 
                    fixed (char* pdata = eventMessage)
                    { 
                        status = (int)EventWriteString(eventLevel, eventKeywords, pdata); 
                    }
 
                    if (status != 0)
                    {
                        SetLastError(status);
                        return false; 
                    }
                } 
            } 
            return true;
        } 

        /// <summary>
        /// WriteMessageEvent, method to write a string with level=0 and Keyword=0
        /// </summary> 
        /// <param name="eventMessage">
        /// Message to log 
        /// </param> 
        public bool WriteMessageEvent(string eventMessage)
        { 
            return WriteMessageEvent(eventMessage, 0, 0);
        }

 
        /// <summary>
        /// WriteEvent, method to write a parameters with event schema properties 
        /// </summary> 
        /// <param name="EventDescriptor">
        /// Event Descriptor for this event. 
        /// </param>
        // <SecurityKernel Critical="True" Ring="0">
        // <CallsSuppressUnmanagedCode Name="ManifestEtw.EventWrite(System.Int64,System.Diagnostics.Eventing.EventDescriptor&,System.UInt32,System.Void*):System.UInt32" />
        // <UsesUnsafeCode Name="Local dataBuffer of type: Byte*" /> 
        // <UsesUnsafeCode Name="Local pdata of type: Char*" />
        // <UsesUnsafeCode Name="Local userData of type: EventData*" /> 
        // <UsesUnsafeCode Name="Local userDataPtr of type: EventData*" /> 
        // <UsesUnsafeCode Name="Local currentBuffer of type: Byte*" />
        // <UsesUnsafeCode Name="Local v0 of type: Char*" /> 
        // <UsesUnsafeCode Name="Local v1 of type: Char*" />
        // <UsesUnsafeCode Name="Local v2 of type: Char*" />
        // <UsesUnsafeCode Name="Local v3 of type: Char*" />
        // <UsesUnsafeCode Name="Local v4 of type: Char*" /> 
        // <UsesUnsafeCode Name="Local v5 of type: Char*" />
        // <UsesUnsafeCode Name="Local v6 of type: Char*" /> 
        // <UsesUnsafeCode Name="Local v7 of type: Char*" /> 
        // <ReferencesCritical Name="Method: EncodeObject(Object&, EventData*, Byte*):String" Ring="1" />
        // </SecurityKernel> 
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity", Justification = "Performance-critical code")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference")]
        [System.Security.SecuritySafeCritical]
        public bool WriteEvent(ref EventDescriptorInternal eventDescriptor, params  object[] eventPayload) 
        {
            uint status = 0; 
 
            if (IsEnabled(eventDescriptor.Level, eventDescriptor.Keywords))
            { 
                int argCount = 0;

                unsafe
                { 
                    if ((eventPayload == null)
                        || (eventPayload.Length == 0) 
                        || (eventPayload.Length == 1)) 
                    {
                        string dataString = null; 
                        EventData userData;

                        byte* dataBuffer = stackalloc byte[s_basicTypeAllocationBufferSize]; // Assume a max of 16 chars for non-string argument
 
                        userData.Size = 0;
                        if ((eventPayload != null) && (eventPayload.Length != 0)) 
                        { 
                            //
                            // Figure out the type and fill the data descriptor 
                            //
                            dataString = EncodeObject(ref eventPayload[0], &userData, dataBuffer);
                            argCount = 1;
                        } 

                        if (userData.Size > s_traceEventMaximumSize) 
                        { 
                            //
                            // Maximum size of the event payload plus header is 64k 
                            //
                            s_returnCode = WriteEventErrorCode.EventTooBig;
                            return false;
                        } 

                        if (dataString != null) 
                        { 
                            fixed (char* pdata = dataString)
                            { 
                                userData.Ptr = (ulong)pdata;
                                status = EventWrite(ref eventDescriptor, (uint)argCount, &userData);
                            }
                        } 
                        else
                        { 
                            if (argCount == 0) 
                            {
                                status = EventWrite(ref eventDescriptor, 0, null); 
                            }
                            else
                            {
                                status = EventWrite(ref eventDescriptor, (uint)argCount, &userData); 
                            }
 
                        } 
                    }
                    else 
                    {

                        argCount = eventPayload.Length;
 
                        if (argCount > s_etwMaxMumberArguments)
                        { 
                            // 
                            //too many arguments to log
                            // 
                            throw new ArgumentOutOfRangeException("eventPayload",
                                SRETW.GetString(SRETW.ArgumentOutOfRange_MaxArgExceeded, s_etwMaxMumberArguments));
                        }
 
                        uint totalEventSize = 0;
                        int index; 
                        int stringIndex = 0; 
                        int[] stringPosition = new int[s_etwAPIMaxStringCount];
                        string[] dataString = new string[s_etwAPIMaxStringCount]; 
                        EventData* userData = stackalloc EventData[argCount];
                        EventData* userDataPtr = (EventData*)userData;
                        byte* dataBuffer = stackalloc byte[s_basicTypeAllocationBufferSize * argCount]; // Assume 16 chars for non-string argument
                        byte* currentBuffer = dataBuffer; 

                        // 
                        // The loop below goes through all the arguments and fills in the data 
                        // descriptors. For strings save the location in the dataString array.
                        // Caculates the total size of the event by adding the data descriptor 
                        // size value set in EncodeObjec method.
                        //
                        for (index = 0; index < eventPayload.Length; index++)
                        { 
                            if (eventPayload[index] != null)
                            { 
                                string isString; 
                                isString = EncodeObject(ref eventPayload[index], userDataPtr, currentBuffer);
                                currentBuffer += s_basicTypeAllocationBufferSize; 
                                totalEventSize += userDataPtr->Size;
                                userDataPtr++;
                                if (isString != null)
                                { 
                                    if (stringIndex < s_etwAPIMaxStringCount)
                                    { 
                                        dataString[stringIndex] = isString; 
                                        stringPosition[stringIndex] = index;
                                        stringIndex++; 
                                    }
                                    else
                                    {
                                        throw new ArgumentOutOfRangeException("eventPayload", 
                                            SRETW.GetString(SRETW.ArgumentOutOfRange_MaxStringsExceeded, s_etwAPIMaxStringCount));
                                    } 
                                } 
                            }
                        } 

                        if (totalEventSize > s_traceEventMaximumSize)
                        {
                            s_returnCode = WriteEventErrorCode.EventTooBig; 
                            return false;
                        } 
 
                        //
                        // now fix any string arguments and set the pointer on the data descriptor 
                        //
                        fixed (char* v0 = dataString[0], v1 = dataString[1], v2 = dataString[2], v3 = dataString[3],
                                v4 = dataString[4], v5 = dataString[5], v6 = dataString[6], v7 = dataString[7])
                        { 
                            userDataPtr = (EventData*)userData;
                            if (dataString[0] != null) 
                            { 
                                userDataPtr[stringPosition[0]].Ptr = (ulong)v0;
                            } 
                            if (dataString[1] != null)
                            {
                                userDataPtr[stringPosition[1]].Ptr = (ulong)v1;
                            } 
                            if (dataString[2] != null)
                            { 
                                userDataPtr[stringPosition[2]].Ptr = (ulong)v2; 
                            }
                            if (dataString[3] != null) 
                            {
                                userDataPtr[stringPosition[3]].Ptr = (ulong)v3;
                            }
                            if (dataString[4] != null) 
                            {
                                userDataPtr[stringPosition[4]].Ptr = (ulong)v4; 
                            } 
                            if (dataString[5] != null)
                            { 
                                userDataPtr[stringPosition[5]].Ptr = (ulong)v5;
                            }
                            if (dataString[6] != null)
                            { 
                                userDataPtr[stringPosition[6]].Ptr = (ulong)v6;
                            } 
                            if (dataString[7] != null) 
                            {
                                userDataPtr[stringPosition[7]].Ptr = (ulong)v7; 
                            }

                            status = EventWrite(ref eventDescriptor, (uint)argCount, userData);
                        } 

                    } 
                } 
            }
 
            if (status != 0)
            {
                SetLastError((int)status);
                return false; 
            }
 
            return true; 
        }
 
        /// <summary>
        /// WriteEvent, method to write a string with event schema properties
        /// </summary>
        /// <param name="EventDescriptor"> 
        /// Event Descriptor for this event.
        /// </param> 
        /// <param name="dataString"> 
        /// string to log.
        /// </param> 
        // <SecurityKernel Critical="True" Ring="0">
        // <CallsSuppressUnmanagedCode Name="ManifestEtw.EventWrite(System.Int64,System.Diagnostics.Eventing.EventDescriptor&,System.UInt32,System.Void*):System.UInt32" />
        // <UsesUnsafeCode Name="Local pdata of type: Char*" />
        // </SecurityKernel> 
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2208:InstantiateArgumentExceptionsCorrectly")] 
        [System.Security.SecurityCritical] 
        public bool WriteEvent(ref EventDescriptorInternal eventDescriptor, string data)
        { 
            uint status = 0;

            if (data == null)
            { 
                throw new ArgumentNullException("dataString");
            } 
 
            if (IsEnabled(eventDescriptor.Level, eventDescriptor.Keywords))
            { 
                if (data.Length > s_traceEventMaximumStringSize)
                {
                    s_returnCode = WriteEventErrorCode.EventTooBig;
                    return false; 
                }
 
                EventData userData; 

                userData.Size = (uint)((data.Length + 1) * 2); 
                userData.Reserved = 0;

                unsafe
                { 
                    fixed (char* pdata = data)
                    { 
                        userData.Ptr = (ulong)pdata; 
                        status = EventWrite(ref eventDescriptor, 1, &userData);
                    } 
                }
            }

            if (status != 0) 
            {
                SetLastError((int)status); 
                return false; 
            }
            return true; 
        }

        /// <summary>
        /// WriteEvent, method to be used by generated code on a derived class 
        /// </summary>
        /// <param name="EventDescriptor"> 
        /// Event Descriptor for this event. 
        /// </param>
        /// <param name="count"> 
        /// number of event descriptors
        /// </param>
        /// <param name="data">
        /// pointer  do the event data 
        /// </param>
        // <SecurityKernel Critical="True" Ring="0"> 
        // <CallsSuppressUnmanagedCode Name="ManifestEtw.EventWrite(System.Int64,System.Diagnostics.Eventing.EventDescriptor&,System.UInt32,System.Void*):System.UInt32" /> 
        // </SecurityKernel>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference")] 
        [System.Security.SecuritySafeCritical]
        internal protected bool WriteEvent(ref EventDescriptorInternal eventDescriptor, int dataCount, IntPtr data)
        {
            uint status = 0; 
            unsafe
            { 
                status = EventWrite(ref eventDescriptor, (uint)dataCount, (EventData*)data); 
            }
            if (status != 0) 
            {
                SetLastError((int)status);
                return false;
            } 
            return true;
        } 
 

        /// <summary> 
        /// WriteTransferEvent, method to write a parameters with event schema properties
        /// </summary>
        /// <param name="eventDescriptor">
        /// Event Descriptor for this event. 
        /// </param>
        // <SecurityKernel Critical="True" Ring="0"> 
        // <CallsSuppressUnmanagedCode Name="ManifestEtw.EventWriteTransfer(System.Int64,System.Diagnostics.Eventing.EventDescriptor&,System.Guid&,System.Guid&,System.UInt32,System.Void*):System.UInt32" /> 
        // <UsesUnsafeCode Name="Local userData of type: EventData*" />
        // <UsesUnsafeCode Name="Local userDataPtr of type: EventData*" /> 
        // <UsesUnsafeCode Name="Local dataBuffer of type: Byte*" />
        // <UsesUnsafeCode Name="Local currentBuffer of type: Byte*" />
        // <UsesUnsafeCode Name="Local v0 of type: Char*" />
        // <UsesUnsafeCode Name="Local v1 of type: Char*" /> 
        // <UsesUnsafeCode Name="Local v2 of type: Char*" />
        // <UsesUnsafeCode Name="Local v3 of type: Char*" /> 
        // <UsesUnsafeCode Name="Local v4 of type: Char*" /> 
        // <UsesUnsafeCode Name="Local v5 of type: Char*" />
        // <UsesUnsafeCode Name="Local v6 of type: Char*" /> 
        // <UsesUnsafeCode Name="Local v7 of type: Char*" />
        // <ReferencesCritical Name="Method: GetActivityId():Guid" Ring="1" />
        // <ReferencesCritical Name="Method: EncodeObject(Object&, EventData*, Byte*):String" Ring="1" />
        // </SecurityKernel> 
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", MessageId = "0#")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity", Justification = "Performance-critical code")] 
        [System.Security.SecurityCritical] 
        public bool WriteTransferEvent(ref EventDescriptorInternal eventDescriptor, Guid relatedActivityId, params object[] eventPayload)
        { 
            uint status = 0;

            if (IsEnabled(eventDescriptor.Level, eventDescriptor.Keywords))
            { 

                Guid activityId = GetActivityId(); 
 
                unsafe
                { 
                    if ((eventPayload != null) && (eventPayload.Length != 0))
                    {
                        int argCount = eventPayload.Length;
                        if (argCount > s_etwMaxMumberArguments) 
                        {
                            // 
                            //too many arguments to log 
                            //
                            throw new ArgumentOutOfRangeException("eventPayload", 
                                SRETW.GetString(SRETW.ArgumentOutOfRange_MaxArgExceeded, s_etwMaxMumberArguments));
                        }

                        uint totalEventSize = 0; 
                        int index;
                        int stringIndex = 0; 
                        int[] stringPosition = new int[s_etwAPIMaxStringCount]; //used to keep the position of strings in the eventPayload parameter 
                        string[] dataString = new string[s_etwAPIMaxStringCount]; // string arrays from the eventPayload parameter
                        EventData* userData = stackalloc EventData[argCount]; // allocation for the data descriptors 
                        EventData* userDataPtr = (EventData*)userData;
                        byte* dataBuffer = stackalloc byte[s_basicTypeAllocationBufferSize * argCount]; // 16 byte for unboxing non-string argument
                        byte* currentBuffer = dataBuffer;
 
                        //
                        // The loop below goes through all the arguments and fills in the data 
                        // descriptors. For strings save the location in the dataString array. 
                        // Caculates the total size of the event by adding the data descriptor
                        // size value set in EncodeObjec method. 
                        //
                        for (index = 0; index < eventPayload.Length; index++)
                        {
                            if (eventPayload[index] != null) 
                            {
                                string isString; 
                                isString = EncodeObject(ref eventPayload[index], userDataPtr, currentBuffer); 
                                currentBuffer += s_basicTypeAllocationBufferSize;
                                totalEventSize += userDataPtr->Size; 
                                userDataPtr++;
                                if (isString != null)
                                {
                                    if (stringIndex < s_etwAPIMaxStringCount) 
                                    {
                                        dataString[stringIndex] = isString; 
                                        stringPosition[stringIndex] = index; 
                                        stringIndex++;
                                    } 
                                    else
                                    {
                                        throw new ArgumentOutOfRangeException("eventPayload",
                                            SRETW.GetString(SRETW.ArgumentOutOfRange_MaxStringsExceeded, s_etwAPIMaxStringCount)); 
                                    }
                                } 
                            } 
                        }
 
                        if (totalEventSize > s_traceEventMaximumSize)
                        {
                            s_returnCode = WriteEventErrorCode.EventTooBig;
                            return false; 
                        }
 
                        fixed (char* v0 = dataString[0], v1 = dataString[1], v2 = dataString[2], v3 = dataString[3], 
                                v4 = dataString[4], v5 = dataString[5], v6 = dataString[6], v7 = dataString[7])
                        { 
                            userDataPtr = (EventData*)userData;
                            if (dataString[0] != null)
                            {
                                userDataPtr[stringPosition[0]].Ptr = (ulong)v0; 
                            }
                            if (dataString[1] != null) 
                            { 
                                userDataPtr[stringPosition[1]].Ptr = (ulong)v1;
                            } 
                            if (dataString[2] != null)
                            {
                                userDataPtr[stringPosition[2]].Ptr = (ulong)v2;
                            } 
                            if (dataString[3] != null)
                            { 
                                userDataPtr[stringPosition[3]].Ptr = (ulong)v3; 
                            }
                            if (dataString[4] != null) 
                            {
                                userDataPtr[stringPosition[4]].Ptr = (ulong)v4;
                            }
                            if (dataString[5] != null) 
                            {
                                userDataPtr[stringPosition[5]].Ptr = (ulong)v5; 
                            } 
                            if (dataString[6] != null)
                            { 
                                userDataPtr[stringPosition[6]].Ptr = (ulong)v6;
                            }
                            if (dataString[7] != null)
                            { 
                                userDataPtr[stringPosition[7]].Ptr = (ulong)v7;
                            } 
 
                            status = EventWriteTransfer(ref eventDescriptor, ref activityId, ref relatedActivityId, (uint)argCount, userData);
                        } 

                    }
                    else
                    { 
                        status = EventWriteTransfer(ref eventDescriptor, ref activityId, ref relatedActivityId, 0, null);
 
                    } 
                }
            } 

            if (status != 0)
            {
                SetLastError((int)status); 
                return false;
            } 
            return true; 
        }
 
        // <SecurityKernel Critical="True" Ring="0">
        // <CallsSuppressUnmanagedCode Name="ManifestEtw.EventWriteTransfer(System.Int64,System.Diagnostics.Eventing.EventDescriptor&,System.Guid&,System.Guid&,System.UInt32,System.Void*):System.UInt32" />
        // <ReferencesCritical Name="Method: GetActivityId():Guid" Ring="1" />
        // </SecurityKernel> 
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", MessageId = "0#")]
        [System.Security.SecurityCritical] 
        protected bool WriteTransferEvent(ref EventDescriptorInternal eventDescriptor, Guid relatedActivityId, int dataCount, IntPtr data) 
        {
            uint status = 0; 
            Guid activityId = GetActivityId();
            unsafe
            {
                status = EventWriteTransfer( 
                                                ref eventDescriptor,
                                                ref activityId, 
                                                ref relatedActivityId, 
                                                (uint)dataCount,
                                                (EventData*)data); 
            }

            if (status != 0)
            { 
                SetLastError((int)status);
                return false; 
            } 
            return true;
        } 

        // <SecurityKernel Critical="True" Ring="0">
        // <SatisfiesLinkDemand Name="Trace.get_CorrelationManager():System.Diagnostics.CorrelationManager" />
        // </SecurityKernel> 
        //
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1806:DoNotIgnoreMethodResults", MessageId = "Microsoft.Win32.ManifestEtw.EventActivityIdControl(System.Int32,System.Guid@)")] 
        [System.Security.SecurityCritical] 
        private static Guid GetActivityId()
        { 
            //


 

            Guid id = new Guid(); 
            EventActivityIdControl((int)ActivityControl.EVENT_ACTIVITY_CTRL_GET_ID, ref id); 
            return id;
        } 

        // <SecurityKernel Critical="True" Ring="0">
        // <CallsSuppressUnmanagedCode Name="ManifestEtw.EventActivityIdControl(System.Int32,System.Guid&):System.UInt32" />
        // </SecurityKernel> 
        //
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1806:DoNotIgnoreMethodResults", MessageId = "Microsoft.Win32.ManifestEtw.EventActivityIdControl(System.Int32,System.Guid@)")] 
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", MessageId = "0#")] 
        [System.Security.SecurityCritical]
        public static void SetActivityId(ref Guid id) 
        {
            EventActivityIdControl((int)ActivityControl.EVENT_ACTIVITY_CTRL_SET_ID, ref id);
        }
 
        // <SecurityKernel Critical="True" Ring="0">
        // <CallsSuppressUnmanagedCode Name="ManifestEtw.EventActivityIdControl(System.Int32,System.Guid&):System.UInt32" /> 
        // </SecurityKernel> 
        //
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1806:DoNotIgnoreMethodResults", MessageId = "Microsoft.Win32.ManifestEtw.EventActivityIdControl(System.Int32,System.Guid@)")] 
        [System.Security.SecurityCritical]
        public static Guid CreateActivityId()
        {
            Guid newId = new Guid(); 
            EventActivityIdControl((int)ActivityControl.EVENT_ACTIVITY_CTRL_CREATE_ID, ref newId);
            return newId; 
        } 

    } 
}

