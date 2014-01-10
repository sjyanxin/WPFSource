//------------------------------------------------------------------------------ 
// <copyright file="eventproviderbase.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <OWNER>[....]</OWNER> 
//-----------------------------------------------------------------------------
 
using System; 
using System.Security;
using System.Runtime.InteropServices; 
using System.Diagnostics.Eventing;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
 
namespace System.Diagnostics.Eventing
{ 
 
    /// <summary>
    /// This part of eventProvider provides an abstraction that allows the rest of the EventProvider 
    /// to assume that all providers will be manifest-based.   On Windows XP where the manifest
    /// based provider APIs don't exist, this layer provides a shim that makes it work for this
    /// 'Classic' API.  Htere are no public declarations in this section.
    /// </summary> 
    internal unsafe partial class EventProvider
    { 
        // Variables used for classic ETW suport. 
        [Security.SecuritySafeCritical]
        private ClassicEtw.ControlCallback m_classicControlCallback;        // callback if we are using classic (XP) ETW 

        private ClassicEtw.EVENT_HEADER* m_classicEventHeader;              // We reuse this buffer for logging classic events
        private long m_classicSessionHandle;
 
        // These are look-alikes to the Manifest based ETW OS APIs that have been shimmed to work
        // either with Manifest ETW or Classic ETW (if Manifest based ETW is not available). 
        [Security.SecuritySafeCritical] 
        private uint EventRegister(ref Guid providerId, ManifestEtw.EtwEnableCallback enableCallback)
        { 
            s_isClassic = m_isClassic = Environment.OSVersion.Version.Major < 6;      // Use classic on XP.

            m_providerId = providerId;
            m_etwCallback = enableCallback; 
            if (!m_isClassic)
                return ManifestEtw.EventRegister(ref providerId, enableCallback, null, ref m_regHandle); 
            else 
                return ClassicShimRegister(providerId, enableCallback);
        } 

        [Security.SecuritySafeCritical]
        private uint EventUnregister()
        { 
            uint status;
            if (!m_isClassic) 
                status = ManifestEtw.EventUnregister(m_regHandle); 
            else
                status = ClassicShimUnregister(); 
            m_regHandle = 0;
            return status;
        }
 
        [Security.SecuritySafeCritical]
        private unsafe uint EventWrite(ref EventDescriptorInternal eventDescriptor, uint userDataCount, EventData* userData) 
        { 
            if (!m_isClassic)
                return ManifestEtw.EventWrite(m_regHandle, ref eventDescriptor, userDataCount, userData); 
            else
                return ClassicShimEventWrite(ref eventDescriptor, userDataCount, userData);
        }
 
        [Security.SecuritySafeCritical]
        private unsafe uint EventWriteTransfer(ref EventDescriptorInternal eventDescriptor, ref Guid activityId, ref Guid relatedActivityId, 
            uint userDataCount, EventData* userData) 
        {
            if (!m_isClassic) 
                return ManifestEtw.EventWriteTransfer(m_regHandle, ref eventDescriptor, ref activityId, ref relatedActivityId, userDataCount, userData);
            else
                return ClassicShimEventWriteTransfer(ref eventDescriptor, ref activityId, ref relatedActivityId, userDataCount, userData);
        } 

        [Security.SecuritySafeCritical] 
        private unsafe uint EventWriteString(byte level, long keywords, char* message) 
        {
            if (!m_isClassic) 
                return ManifestEtw.EventWriteString(m_regHandle, level, keywords, message);
            else
                return ClassicShimEventWriteString(level, keywords, message);
        } 

        [Security.SecuritySafeCritical] 
        private static uint EventActivityIdControl(int controlCode, ref Guid activityId) 
        {
            if (!s_isClassic) 
                return ManifestEtw.EventActivityIdControl(controlCode, ref activityId);
            else
                return ClassicShimEventActivityIdControl(controlCode, ref activityId);
        } 

        #region Classic ETW Shims 
        /// <summary> 
        /// This is called for classic (pre-Vista) ETW when the controller sends a command to the
        /// provider. 
        /// </summary>
        [Security.SecurityCritical]
        private uint ClassicControlCallback(ClassicEtw.WMIDPREQUESTCODE requestCode, IntPtr requestContext, IntPtr reserved, ClassicEtw.WNODE_HEADER* data)
        { 
            int flags = ClassicEtw.GetTraceEnableFlags(data->HistoricalContext);
            byte level = ClassicEtw.GetTraceEnableLevel(data->HistoricalContext); 
            int enabled = 0; 
            if (requestCode == ClassicEtw.WMIDPREQUESTCODE.EnableEvents)
            { 
                m_classicSessionHandle = ClassicEtw.GetTraceLoggerHandle(data);
                enabled = 1;
            }
            else if (requestCode == ClassicEtw.WMIDPREQUESTCODE.DisableEvents) 
            {
                m_classicSessionHandle = 0; 
                enabled = 0; 
            }
            m_etwCallback(ref m_providerId, enabled, level, flags, 0, null, null); 
            return 0;
        }

        [Security.SecuritySafeCritical] 
        private unsafe uint ClassicShimRegister(Guid providerId, ManifestEtw.EtwEnableCallback enableCallback)
        { 
            if (m_regHandle != 0)           // registering again illegal 
                throw new Exception();      //
 
            m_classicEventHeader = (ClassicEtw.EVENT_HEADER*)Marshal.AllocHGlobal(sizeof(ClassicEtw.EVENT_HEADER));
            ZeroMemory((IntPtr)m_classicEventHeader, sizeof(ClassicEtw.EVENT_HEADER));

            // We only declare one Task GUID because you don't need to be accurate. 
            ClassicEtw.TRACE_GUID_REGISTRATION registrationInfo;
            registrationInfo.RegHandle = null; 
            registrationInfo.Guid = &providerId; 

            // We assign it to a field variable to keep it alive until we unregister. 
            m_classicControlCallback = ClassicControlCallback;
            return ClassicEtw.RegisterTraceGuidsW(m_classicControlCallback, null, ref providerId, 1, &registrationInfo, null, null, out m_regHandle);
        }
 
        [Security.SecuritySafeCritical]
        private uint ClassicShimUnregister() 
        { 
            //
            uint status = ClassicEtw.UnregisterTraceGuids(m_regHandle); 
            m_regHandle = 0;
            m_classicControlCallback = null;
            m_classicSessionHandle = 0;
            if (m_classicEventHeader != null) 
            {
                Marshal.FreeHGlobal((IntPtr)m_classicEventHeader); 
                m_classicEventHeader = null; 
            }
            return status; 
        }

        [Security.SecurityCritical]
        private unsafe uint ClassicShimEventWrite(ref EventDescriptorInternal eventDescriptor, uint userDataCount, EventData* userData) 
        {
            m_classicEventHeader->Header.ClientContext = 0; 
            m_classicEventHeader->Header.Flags = ClassicEtw.WNODE_FLAG_TRACED_GUID | ClassicEtw.WNODE_FLAG_USE_MOF_PTR; 
            m_classicEventHeader->Header.Guid = GenTaskGuidFromProviderGuid(m_providerId, (ushort)eventDescriptor.Task);
            m_classicEventHeader->Header.Level = eventDescriptor.Level; 
            m_classicEventHeader->Header.Type = eventDescriptor.Opcode;
            m_classicEventHeader->Header.Version = eventDescriptor.Version;
            EventData* eventData = &m_classicEventHeader->Data;
 
            if (userDataCount > ClassicEtw.MAX_MOF_FIELDS)
                throw new Exception();      // 
            m_classicEventHeader->Header.Size = (ushort)(48 + userDataCount * sizeof(EventData)); 
            for (int i = 0; i < userDataCount; i++)
            { 
                eventData[i].Ptr = userData[i].Ptr;
                eventData[i].Size = userData[i].Size;
            }
            return ClassicEtw.TraceEvent(m_classicSessionHandle, m_classicEventHeader); 
        }
 
        [Security.SecurityCritical] 
        private unsafe uint ClassicShimEventWriteTransfer(ref EventDescriptorInternal eventDescriptor, ref Guid activityId, ref Guid relatedActivityId, uint userDataCount, EventData* userData)
        { 
            //
            throw new NotImplementedException();
        }
 
        [Security.SecurityCritical]
        private unsafe uint ClassicShimEventWriteString(byte level, long keywords, char* message) 
        { 
            //
            EventDescriptorInternal eventDescr = new EventDescriptorInternal(0, 0, 0, 0, 0, 0, 0); 

            char* end = message;
            while (*end != 0)
                end++; 

            EventData dataDesc = new EventData(); 
            dataDesc.Ptr = (ulong)message; 
            dataDesc.Size = (uint)(end - message) + 1;
            dataDesc.Reserved = 0; 

            return ClassicShimEventWrite(ref eventDescr, 1, &dataDesc);
        }
 
        private static uint ClassicShimEventActivityIdControl(int controlCode, ref Guid activityId)
        { 
            throw new NotImplementedException(); 
        }
 
        /// <summary>
        /// A helper for creating a set of related guids (knowing the providerGuid can can deduce the
        /// 'taskNumber' member of this group.  All we do is add the taskNumber to GUID as a number.
        /// </summary> 
        internal static Guid GenTaskGuidFromProviderGuid(Guid providerGuid, ushort taskNumber)
        { 
            byte[] bytes = providerGuid.ToByteArray(); 

            bytes[15] += (byte)taskNumber; 
            bytes[14] += (byte)(taskNumber >> 8);
            return new Guid(bytes);
        }
 
        internal static ushort GetTaskFromTaskGuid(Guid taskGuid, Guid providerGuid)
        { 
            byte[] taskGuidBytes = taskGuid.ToByteArray(); 
            byte[] providerGuidBytes = providerGuid.ToByteArray();
 
            // Spot check
            Contract.Assert(taskGuidBytes[0] == providerGuidBytes[0] && taskGuidBytes[6] == providerGuidBytes[6]);
            return (ushort)(((taskGuidBytes[1] - providerGuidBytes[14]) << 8) + (taskGuidBytes[15] - providerGuidBytes[15]));
        } 
        #endregion
 
        #region PInvoke Declarations 
        internal const String ADVAPI32 = "advapi32.dll";
        [SuppressUnmanagedCodeSecurityAttribute()] 
        internal static unsafe class ManifestEtw
        {
            //
            // Constants error coded returned by ETW APIs 
            //
 
            // The event size is larger than the allowed maximum (64k - header). 
            internal const int ERROR_ARITHMETIC_OVERFLOW = 534;
 
            // Occurs when filled buffers are trying to flush to disk,
            // but disk IOs are not happening fast enough.
            // This happens when the disk is slow and event traffic is heavy.
            // Eventually, there are no more free (empty) buffers and the event is dropped. 
            internal const int ERROR_NOT_ENOUGH_MEMORY = 8;
 
            internal const int ERROR_MORE_DATA = 0xEA; 

            // 
            // ETW Methods
            //
            //
            // Callback 
            //
            [Security.SecuritySafeCritical] 
            internal unsafe delegate void EtwEnableCallback( 
                [In] ref Guid sourceId,
                [In] int isEnabled, 
                [In] byte level,
                [In] long matchAnyKeywords,
                [In] long matchAllKeywords,
                [In] EVENT_FILTER_DESCRIPTOR* filterData, 
                [In] void* callbackContext
                ); 
 
            //
            // Registration APIs 
            //
            [Security.SecurityCritical]
            [DllImport(ADVAPI32, ExactSpelling = true, EntryPoint = "EventRegister", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
            internal static extern unsafe uint EventRegister( 
                        [In] ref Guid providerId,
                        [In]EtwEnableCallback enableCallback, 
                        [In]void* callbackContext, 
                        [In][Out]ref long registrationHandle
                        ); 

            //
            [Security.SecurityCritical]
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage")] 
            [DllImport(ADVAPI32, ExactSpelling = true, EntryPoint = "EventUnregister", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
            internal static extern uint EventUnregister([In] long registrationHandle); 
 
            //
            // Writing (Publishing/Logging) APIs 
            //
            //
            [Security.SecurityCritical]
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage")] 
            [DllImport(ADVAPI32, ExactSpelling = true, EntryPoint = "EventWrite", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
            internal static extern unsafe uint EventWrite( 
                    [In] long registrationHandle, 
                    [In] ref EventDescriptorInternal eventDescriptor,
                    [In] uint userDataCount, 
                    [In] EventData* userData
                    );

            // 
            [Security.SecurityCritical]
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage")] 
            [DllImport(ADVAPI32, ExactSpelling = true, EntryPoint = "EventWriteTransfer", CharSet = System.Runtime.InteropServices.CharSet.Unicode)] 
            internal static extern unsafe uint EventWriteTransfer(
                    [In] long registrationHandle, 
                    [In] ref EventDescriptorInternal eventDescriptor,
                    [In] ref Guid activityId,
                    [In] ref Guid relatedActivityId,
                    [In] uint userDataCount, 
                    [In] EventData* userData
                    ); 
 
            //
            [Security.SecurityCritical] 
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage")]
            [DllImport(ADVAPI32, ExactSpelling = true, EntryPoint = "EventWriteString", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
            internal static extern unsafe uint EventWriteString(
                    [In] long registrationHandle, 
                    [In] byte level,
                    [In] long keywords, 
                    [In] char* message 
                    );
            // 
            // ActivityId Control APIs
            //
            //
            [Security.SecurityCritical] 
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage")]
            [DllImport(ADVAPI32, ExactSpelling = true, EntryPoint = "EventActivityIdControl", CharSet = System.Runtime.InteropServices.CharSet.Unicode)] 
            internal static extern unsafe uint EventActivityIdControl([In] int ControlCode, [In][Out] ref Guid ActivityId); 

            [StructLayout(LayoutKind.Sequential)] 
            unsafe internal struct EVENT_FILTER_DESCRIPTOR
            {
                public long Ptr;
                public int Size; 
                public int Type;
            }; 
        } 

        [SuppressUnmanagedCodeSecurityAttribute()] 
        internal static unsafe class ClassicEtw
        {
            #region RegisterTraceGuidsW()
            // Support structs for RegisterTraceGuidsW 
            [StructLayout(LayoutKind.Sequential)]
            internal struct TRACE_GUID_REGISTRATION 
            { 
                internal unsafe Guid* Guid;
                internal unsafe void* RegHandle; 
            }

            [StructLayout(LayoutKind.Sequential)]
            internal struct WNODE_HEADER 
            {
                public UInt32 BufferSize; 
                public UInt32 ProviderId; 
                public UInt64 HistoricalContext;
                public UInt64 TimeStamp; 
                public Guid Guid;
                public UInt32 ClientContext;
                public UInt32 Flags;
            }; 

 
            internal enum WMIDPREQUESTCODE 
            {
                GetAllData = 0, 
                GetSingleInstance = 1,
                SetSingleInstance = 2,
                SetSingleItem = 3,
                EnableEvents = 4, 
                DisableEvents = 5,
                EnableCollection = 6, 
                DisableCollection = 7, 
                RegInfo = 8,
                ExecuteMethod = 9, 
            };

            [Security.SecurityCritical]
            internal unsafe delegate uint ControlCallback(WMIDPREQUESTCODE requestCode, IntPtr requestContext, IntPtr reserved, WNODE_HEADER* data); 

            [Security.SecurityCritical] 
            [DllImport(ADVAPI32, CharSet = CharSet.Unicode)] 
            internal static extern unsafe uint RegisterTraceGuidsW([In] ControlCallback cbFunc, [In] void* context, [In] ref Guid providerGuid, [In] int taskGuidCount, [In, Out] TRACE_GUID_REGISTRATION* taskGuids, [In] string mofImagePath, [In] string mofResourceName, out long regHandle);
            #endregion // RegisterTraceGuidsW 

            [Security.SecurityCritical]
            [DllImport(ADVAPI32)]
            internal static extern uint UnregisterTraceGuids(long regHandle); 

            [Security.SecurityCritical] 
            [DllImport(ADVAPI32)] 
            internal static extern int GetTraceEnableFlags(ulong traceHandle);
 
            [Security.SecurityCritical]
            [DllImport(ADVAPI32)]
            internal static extern byte GetTraceEnableLevel(ulong traceHandle);
 
            [Security.SecurityCritical]
            [DllImport(ADVAPI32)] 
            internal static extern long GetTraceLoggerHandle(WNODE_HEADER* data); 

            #region TraceEvent() 
            // Structures for TraceEvent API.

            // Constants for flags field.
            internal const int WNODE_FLAG_TRACED_GUID = 0x00020000; 
            internal const int WNODE_FLAG_USE_MOF_PTR = 0x00100000;
 
            // Size is 48 = 0x30 bytes; 
            [StructLayout(LayoutKind.Sequential)]
            internal struct EVENT_TRACE_HEADER 
            {
                public ushort Size;
                public ushort FieldTypeFlags;	// holds our MarkerFlags too
                public byte Type;               // This is now called opcode. 
                public byte Level;
                public ushort Version; 
                public int ThreadId; 
                public int ProcessId;
                public long TimeStamp;          // Offset 0x10 
                public Guid Guid;               // Offset 0x18
                public uint ClientContext;      // Offset 0x28
                public uint Flags;              // Offset 0x2C
            } 

            internal const int MAX_MOF_FIELDS = 16; 
            [StructLayout(LayoutKind.Explicit, Size = 304)] // Size = (48 + 16 * MAX_MOF_FIELDS) 
            internal struct EVENT_HEADER
            { 
                [FieldOffset(0)]
                public EVENT_TRACE_HEADER Header;
                [FieldOffset(48)]
                public EventData Data;         // Actually variable sized; 
            }
 
            [Security.SecurityCritical] 
            [DllImport(ADVAPI32)]
            internal static extern unsafe uint TraceEvent(long traceHandle, EVENT_HEADER* header); 
            #endregion // TraceEvent()
        }

        [Security.SecurityCritical] 
        [SuppressUnmanagedCodeSecurity]
        [DllImport("kernel32.dll", SetLastError = true)] 
        internal static extern void ZeroMemory(IntPtr handle, int length); 
        #endregion
    } 
}

