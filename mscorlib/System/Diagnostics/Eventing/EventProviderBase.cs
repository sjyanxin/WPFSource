//------------------------------------------------------------------------------ 
// <copyright file="eventproviderbase.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <OWNER>[....]</OWNER> 
//-----------------------------------------------------------------------------
// This program uses code hyperlinks available as part of the HyperAddin Visual Studio plug-in. 
// It is available from http://www.codeplex.com/hyperAddin 

using System; 
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Diagnostics.Eventing; 
using System.Diagnostics;
using System.Reflection; 
using System.Threading; 
using System.Diagnostics.Contracts;
 
// Implementation of my EventProvider scheme
namespace System.Diagnostics.Eventing
{
    /// <summary> 
    /// This class is meant to be inherited by a user provider (which provides specific events and then
    /// calls code:EventProviderBase.WriteEvent to log them). 
    /// 
    /// sealed class MinimalProvider : EventProviderBase
    /// { 
    ///     * public void Load(long ImageBase, string Name) { WriteEvent(1, ImageBase, Name); }
    ///     * public void Unload(long ImageBase) { WriteEvent(2, ImageBase); }
    ///     * private MinimalProvider() : base(new Guid(0xc836fd3, 0xee92, 0x4301, 0xbc, 0x13, 0xd8, 0x94, 0x3e, 0xc, 0x1e, 0x77)) {}
    /// } 
    ///
    /// This functionaity is sufficient for many users.   When more control is needed over the ETW manifest 
    /// that is created, that can be done by adding [Event] attributes on the  methods. 
    ///
    /// Finally for very advanced Providers, it is possible to intercept the commands being given to the 
    /// provider and change what filtering is done (or cause actions to be performed by the provider (eg
    /// duming a data structure).
    ///
    /// The providers can be turned on with Window ETW controllers (eg logman), immediately.  It is also 
    /// possible to control and intercept the data stream programatically.  We code:EventProviderDataStream for
    /// more. 
    /// </summary> 

    [System.Runtime.CompilerServices.FriendAccessAllowed] 
    internal class EventProviderBase : IDisposable
    {
        /// <summary>
        /// Most events should be fired by calling methods on a subclass of code:EventProviderBase but we 
        /// provide this API in the base to provide this trivial functionality in the case of a single
        /// string. 
        /// </summary> 
        public void WriteMessage(string eventMessage)
        { 
            WriteMessage(eventMessage, EventLevel.LogAlways, EventKeywords.None);
        }

        public void WriteMessage(string eventMessage, EventLevel level, EventKeywords keywords) 
        {
#if ETW_SUPPORTED 
            if(m_provider != null) 
                m_provider.WriteMessageEvent(eventMessage, (byte)level, (long)keywords);
#endif 
            WriteToAllStreams(0, eventMessage);
        }

        /// <summary> 
        /// The human-friendly name of the provider.  It defaults to the simple name of the class
        /// </summary> 
        public string Name { get { return m_name; } } 

        /// <summary> 
        /// Every provider is assigned a GUID to uniquely identify it to the system.
        /// </summary>
        public Guid Guid { get { return m_guid; } }
        /// <summary> 
        /// Returns true if the provider has been enabled at all.
        /// </summary> 
        public bool IsEnabled() 
        {
            return m_providerEnabled; 
        }
        public bool IsEnabled(EventLevel level, EventKeywords keywords)
        {
            if (!m_providerEnabled) 
                return false;
            if (m_level != 0 && m_level < level) 
                return false; 
            return m_matchAnyKeyword == 0 || (keywords & m_matchAnyKeyword) != 0;
        } 

        /// <summary>
        /// Returns a string of the XML manifest associated with the provider. The scheme for this XML is
        /// documented at in EventManifest Schema http://msdn.microsoft.com/en-us/library/aa384043(VS.85).aspx 
        /// </summary>
        /// <param name="providerDllName">The manifest XML fragment contains the string name of the DLL name in 
        /// which it is embeded.  This parameter spcifies what name will be used</param> 
        /// <returns>The XML data string</returns>
        public string ProviderManifestXmlFragment(string providerDllName) 
        {
            byte[] providerBytes = m_rawManifest;
            if (providerBytes == null)
                providerBytes = CreateManifestAndDescriptors(providerDllName); 
            return Encoding.UTF8.GetString(providerBytes);
        } 
 
        #region protected
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1720:IdentifiersShouldNotContainTypeNames", MessageId = "guid")] 
        protected EventProviderBase(Guid providerGuid) : this(providerGuid, null) { }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1720:IdentifiersShouldNotContainTypeNames", MessageId = "guid")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors", Justification = "The constructor call chains to DoDebugChecks() is safe.")] 
        [System.Security.SecuritySafeCritical]
        protected EventProviderBase(Guid providerGuid, string providerName) 
        { 
            if (providerName == null)
                providerName = GetType().Name; 
            m_name = providerName;
            m_guid = providerGuid;
#if ETW_SUPPORTED
            m_provider = new OverideEventProvider(this); 

            try { 
                m_provider.Register(providerGuid); 
            } catch (ArgumentException) {
                // Failed to register.  Don't crash the app, just don't write events to ETW. 
                m_provider = null;
            }

#endif 
            if (m_providerEnabled && !m_ETWManifestSent)
            { 
                SendManifest(m_rawManifest, null); 
                m_ETWManifestSent = true;
            } 
            m_completelyInited = true;
            // Add the provider to the global (weak) list.  This also sets m_id, which is the
            // index in the list.
            EventProviderDataStream.AddProvider(this); 
        }
 
        /// <summary> 
        /// This method is called when the provider is updated by the controller.
        /// </summary> 
        protected virtual void OnControllerCommand(EventProviderDataStream outputStream, ControllerCommand command, IDictionary<string, string> arguments) { }

        // optimized for common signatures (no args)
        protected unsafe void WriteEvent(int eventId) 
        {
            Contract.Assert(m_eventData[eventId].Descriptor.EventId != 0); 
#if ETW_SUPPORTED 
            if (m_provider != null && m_eventData[eventId].EnabledForETW)
            { 
                m_provider.WriteEvent(ref m_eventData[eventId].Descriptor, 0, (IntPtr)0);
                if (m_eventData[eventId].CaptureStack)
                    CaptureStack();
            } 
#endif
            if (m_OutputStreams != null && m_eventData[eventId].EnabledForAnyStream) WriteToAllStreams(eventId); 
        } 

        // optimized for common signatures (ints) 
        [Security.SecuritySafeCritical]
        protected unsafe void WriteEvent(int eventId, int value)
        {
            Contract.Assert(m_eventData[eventId].Descriptor.EventId != 0); 
#if ETW_SUPPORTED
            if (m_provider != null && m_eventData[eventId].EnabledForETW) 
            { 
                EventProvider.EventData* dataDescrs = stackalloc EventProvider.EventData[1];
                dataDescrs[0].Ptr = (ulong)&value; 
                dataDescrs[0].Size = 4;
                dataDescrs[0].Reserved = 0;

                m_provider.WriteEvent(ref m_eventData[eventId].Descriptor, 1, (IntPtr)dataDescrs); 
                if (m_eventData[eventId].CaptureStack)
                    CaptureStack(); 
            } 
#endif
            if (m_OutputStreams != null && m_eventData[eventId].EnabledForAnyStream) WriteToAllStreams(eventId, value); 
        }

        [Security.SecuritySafeCritical]
        protected unsafe void WriteEvent(int eventId, int value1, int value2) 
        {
            Contract.Assert(m_eventData[eventId].Descriptor.EventId != 0); 
#if ETW_SUPPORTED 
            if (m_provider != null && m_eventData[eventId].EnabledForETW)
            { 
                EventProvider.EventData* dataDescrs = stackalloc EventProvider.EventData[2];
                dataDescrs[0].Ptr = (ulong)&value1;
                dataDescrs[0].Size = 4;
                dataDescrs[0].Reserved = 0; 

                dataDescrs[1].Ptr = (ulong)&value2; 
                dataDescrs[1].Size = 4; 
                dataDescrs[1].Reserved = 0;
 
                m_provider.WriteEvent(ref m_eventData[eventId].Descriptor, 2, (IntPtr)dataDescrs);
                if (m_eventData[eventId].CaptureStack)
                    CaptureStack();
            } 
#endif
            if (m_OutputStreams != null && m_eventData[eventId].EnabledForAnyStream) WriteToAllStreams(eventId, value1, value2); 
        } 

        [Security.SecuritySafeCritical] 
        protected unsafe void WriteEvent(int eventId, int value1, int value2, int value3)
        {
            Contract.Assert(m_eventData[eventId].Descriptor.EventId != 0);
#if ETW_SUPPORTED 
            if (m_provider != null && m_eventData[eventId].EnabledForETW)
            { 
                EventProvider.EventData* dataDescrs = stackalloc EventProvider.EventData[3]; 
                dataDescrs[0].Ptr = (ulong)&value1;
                dataDescrs[0].Size = 4; 
                dataDescrs[0].Reserved = 0;

                dataDescrs[1].Ptr = (ulong)&value2;
                dataDescrs[1].Size = 4; 
                dataDescrs[1].Reserved = 0;
 
                dataDescrs[2].Ptr = (ulong)&value3; 
                dataDescrs[2].Size = 4;
                dataDescrs[2].Reserved = 0; 

                m_provider.WriteEvent(ref m_eventData[eventId].Descriptor, 3, (IntPtr)dataDescrs);
                if (m_eventData[eventId].CaptureStack)
                    CaptureStack(); 
            }
#endif 
            if (m_OutputStreams != null && m_eventData[eventId].EnabledForAnyStream) WriteToAllStreams(eventId, value1, value2, value3); 
        }
 
        // optimized for common signatures (longs)
        [Security.SecuritySafeCritical]
        protected unsafe void WriteEvent(int eventId, long value)
        { 
            Contract.Assert(m_eventData[eventId].Descriptor.EventId != 0);
#if ETW_SUPPORTED 
            if (m_provider != null && m_eventData[eventId].EnabledForETW) 
            {
                EventProvider.EventData* dataDescrs = stackalloc EventProvider.EventData[1]; 
                dataDescrs[0].Ptr = (ulong)&value;
                dataDescrs[0].Size = 8;
                dataDescrs[0].Reserved = 0;
 
                m_provider.WriteEvent(ref m_eventData[eventId].Descriptor, 1, (IntPtr)dataDescrs);
                if (m_eventData[eventId].CaptureStack) 
                    CaptureStack(); 
            }
#endif 
            if (m_OutputStreams != null && m_eventData[eventId].EnabledForAnyStream) WriteToAllStreams(eventId, value);
        }

        [Security.SecuritySafeCritical] 
        protected unsafe void WriteEvent(int eventId, long value1, long value2)
        { 
            Contract.Assert(m_eventData[eventId].Descriptor.EventId != 0); 
#if ETW_SUPPORTED
            if (m_provider != null && m_eventData[eventId].EnabledForETW) 
            {
                EventProvider.EventData* dataDescrs = stackalloc EventProvider.EventData[2];
                dataDescrs[0].Ptr = (ulong)&value1;
                dataDescrs[0].Size = 8; 
                dataDescrs[0].Reserved = 0;
 
                dataDescrs[1].Ptr = (ulong)&value2; 
                dataDescrs[1].Size = 8;
                dataDescrs[1].Reserved = 0; 

                m_provider.WriteEvent(ref m_eventData[eventId].Descriptor, 2, (IntPtr)dataDescrs);
                if (m_eventData[eventId].CaptureStack)
                    CaptureStack(); 
            }
#endif 
            if (m_OutputStreams != null && m_eventData[eventId].EnabledForAnyStream) WriteToAllStreams(eventId, value1, value2); 
        }
 
        [Security.SecuritySafeCritical]
        protected unsafe void WriteEvent(int eventId, long value1, long value2, long value3)
        {
            Contract.Assert(m_eventData[eventId].Descriptor.EventId != 0); 
#if ETW_SUPPORTED
            if (m_provider != null && m_eventData[eventId].EnabledForETW) 
            { 
                EventProvider.EventData* dataDescrs = stackalloc EventProvider.EventData[3];
                dataDescrs[0].Ptr = (ulong)&value1; 
                dataDescrs[0].Size = 8;
                dataDescrs[0].Reserved = 0;

                dataDescrs[1].Ptr = (ulong)&value2; 
                dataDescrs[1].Size = 8;
                dataDescrs[1].Reserved = 0; 
 
                dataDescrs[2].Ptr = (ulong)&value3;
                dataDescrs[2].Size = 8; 
                dataDescrs[2].Reserved = 0;

                m_provider.WriteEvent(ref m_eventData[eventId].Descriptor, 3, (IntPtr)dataDescrs);
                if (m_eventData[eventId].CaptureStack) 
                    CaptureStack();
            } 
#endif 
            if (m_OutputStreams != null && m_eventData[eventId].EnabledForAnyStream) WriteToAllStreams(eventId, value1, value2, value3);
        } 

        // optimized for common signatures (strings)
        [Security.SecuritySafeCritical]
        protected unsafe void WriteEvent(int eventId, string value) 
        {
            Contract.Assert(m_eventData[eventId].Descriptor.EventId != 0); 
#if ETW_SUPPORTED 
            if (m_provider != null && m_eventData[eventId].EnabledForETW)
            { 
                fixed (char* stringBytes = value)
                {
                    EventProvider.EventData* dataDescrs = stackalloc EventProvider.EventData[1];
                    dataDescrs[0].Ptr = (ulong)stringBytes; 
                    dataDescrs[0].Size = (uint)((value.Length + 1) * 2);
                    dataDescrs[0].Reserved = 0; 
 
                    m_provider.WriteEvent(ref m_eventData[eventId].Descriptor, 1, (IntPtr)dataDescrs);
                    if (m_eventData[eventId].CaptureStack) 
                        CaptureStack();
                }
            }
#endif 
            if (m_OutputStreams != null && m_eventData[eventId].EnabledForAnyStream) WriteToAllStreams(eventId, value);
        } 
 
        [Security.SecuritySafeCritical]
        protected unsafe void WriteEvent(int eventId, string value1, string value2) 
        {
            Contract.Assert(m_eventData[eventId].Descriptor.EventId != 0);
#if ETW_SUPPORTED
            if (m_provider != null && m_eventData[eventId].EnabledForETW) 
            {
                fixed (char* string1Bytes = value1) 
                fixed (char* string2Bytes = value2) 
                {
                    EventProvider.EventData* dataDescrs = stackalloc EventProvider.EventData[2]; 
                    dataDescrs[0].Ptr = (ulong)string1Bytes;
                    dataDescrs[0].Size = (uint)((value1.Length + 1) * 2);
                    dataDescrs[0].Reserved = 0;
 
                    dataDescrs[1].Ptr = (ulong)string2Bytes;
                    dataDescrs[1].Size = (uint)((value2.Length + 1) * 2); 
                    dataDescrs[1].Reserved = 0; 

                    m_provider.WriteEvent(ref m_eventData[eventId].Descriptor, 2, (IntPtr)dataDescrs); 
                    if (m_eventData[eventId].CaptureStack)
                        CaptureStack();
                }
            } 
#endif
            if (m_OutputStreams != null && m_eventData[eventId].EnabledForAnyStream) WriteToAllStreams(eventId, value1, value2); 
        } 

        [Security.SecuritySafeCritical] 
        protected unsafe void WriteEvent(int eventId, string value1, string value2, string value3)
        {
            Contract.Assert(m_eventData[eventId].Descriptor.EventId != 0);
#if ETW_SUPPORTED 
            if (m_provider != null && m_eventData[eventId].EnabledForETW)
            { 
                fixed (char* string1Bytes = value1) 
                fixed (char* string2Bytes = value2)
                fixed (char* string3Bytes = value3) 
                {
                    EventProvider.EventData* dataDescrs = stackalloc EventProvider.EventData[3];
                    dataDescrs[0].Ptr = (ulong)string1Bytes;
                    dataDescrs[0].Size = (uint)((value1.Length + 1) * 2); 
                    dataDescrs[0].Reserved = 0;
 
                    dataDescrs[1].Ptr = (ulong)string2Bytes; 
                    dataDescrs[1].Size = (uint)((value2.Length + 1) * 2);
                    dataDescrs[1].Reserved = 0; 

                    dataDescrs[2].Ptr = (ulong)string3Bytes;
                    dataDescrs[2].Size = (uint)((value3.Length + 1) * 2);
                    dataDescrs[2].Reserved = 0; 
                    m_provider.WriteEvent(ref m_eventData[eventId].Descriptor, 3, (IntPtr)dataDescrs);
                    if (m_eventData[eventId].CaptureStack) 
                        CaptureStack(); 
                }
            } 
#endif
            if (m_OutputStreams != null && m_eventData[eventId].EnabledForAnyStream) WriteToAllStreams(eventId, value1, value2, value3);
        }
 
        // optimized for common signatures (string and ints)
        [Security.SecuritySafeCritical] 
        protected unsafe void WriteEvent(int eventId, string value1, int value2) 
        {
            Contract.Assert(m_eventData[eventId].Descriptor.EventId != 0); 
#if ETW_SUPPORTED
            if (m_provider != null && m_eventData[eventId].EnabledForETW)
            {
                fixed (char* string1Bytes = value1) 
                {
                    EventProvider.EventData* dataDescrs = stackalloc EventProvider.EventData[2]; 
                    dataDescrs[0].Ptr = (ulong)string1Bytes; 
                    dataDescrs[0].Size = (uint)((value1.Length + 1) * 2);
                    dataDescrs[0].Reserved = 0; 

                    dataDescrs[1].Ptr = (ulong)&value2;
                    dataDescrs[1].Size = 4;
                    dataDescrs[1].Reserved = 0; 

                    m_provider.WriteEvent(ref m_eventData[eventId].Descriptor, 2, (IntPtr)dataDescrs); 
                    if (m_eventData[eventId].CaptureStack) 
                        CaptureStack();
                } 
            }
#endif
            if (m_OutputStreams != null && m_eventData[eventId].EnabledForAnyStream) WriteToAllStreams(eventId, value1, value2);
        } 

        [Security.SecuritySafeCritical] 
        protected unsafe void WriteEvent(int eventId, string value1, int value2, int value3) 
        {
            Contract.Assert(m_eventData[eventId].Descriptor.EventId != 0); 
#if ETW_SUPPORTED
            if (m_provider != null && m_eventData[eventId].EnabledForETW)
            {
                fixed (char* string1Bytes = value1) 
                {
                    EventProvider.EventData* dataDescrs = stackalloc EventProvider.EventData[3]; 
                    dataDescrs[0].Ptr = (ulong)string1Bytes; 
                    dataDescrs[0].Size = (uint)((value1.Length + 1) * 2);
                    dataDescrs[0].Reserved = 0; 

                    dataDescrs[1].Ptr = (ulong)&value2;
                    dataDescrs[1].Size = 4;
                    dataDescrs[1].Reserved = 0; 

                    dataDescrs[2].Ptr = (ulong)&value3; 
                    dataDescrs[2].Size = 4; 
                    dataDescrs[2].Reserved = 0;
 
                    m_provider.WriteEvent(ref m_eventData[eventId].Descriptor, 3, (IntPtr)dataDescrs);
                    if (m_eventData[eventId].CaptureStack)
                        CaptureStack();
                } 
            }
#endif 
            if (m_OutputStreams != null && m_eventData[eventId].EnabledForAnyStream) WriteToAllStreams(eventId, value1, value2, value3); 
        }
 
        // optimized for common signatures (string and longs)
        [Security.SecuritySafeCritical]
        protected unsafe void WriteEvent(int eventId, string value1, long value2)
        { 
            Contract.Assert(m_eventData[eventId].Descriptor.EventId != 0);
#if ETW_SUPPORTED 
            if (m_provider != null && m_eventData[eventId].EnabledForETW) 
            {
                fixed (char* string1Bytes = value1) 
                {
                    EventProvider.EventData* dataDescrs = stackalloc EventProvider.EventData[2];
                    dataDescrs[0].Ptr = (ulong)string1Bytes;
                    dataDescrs[0].Size = (uint)((value1.Length + 1) * 2); 
                    dataDescrs[0].Reserved = 0;
 
                    dataDescrs[1].Ptr = (ulong)&value2; 
                    dataDescrs[1].Size = 8;
                    dataDescrs[1].Reserved = 0; 

                    m_provider.WriteEvent(ref m_eventData[eventId].Descriptor, 2, (IntPtr)dataDescrs);
                    if (m_eventData[eventId].CaptureStack)
                        CaptureStack(); 
                }
            } 
#endif 
            if (m_OutputStreams != null && m_eventData[eventId].EnabledForAnyStream) WriteToAllStreams(eventId, value1, value2);
        } 

        // fallback varags helpers.
        [Security.SecuritySafeCritical]
        protected unsafe void WriteEvent(int eventId, params object[] args) 
        {
            Contract.Assert(m_eventData[eventId].Descriptor.EventId != 0); 
#if ETW_SUPPORTED 
            if (m_provider != null && m_eventData[eventId].EnabledForETW)
                m_provider.WriteEvent(ref m_eventData[eventId].Descriptor, args); 
#endif
            if (m_OutputStreams != null && m_eventData[eventId].EnabledForAnyStream) WriteToAllStreams(eventId, args);
        }
 
        [Security.SecuritySafeCritical]
        protected void WriteTransferEventHelper(int eventId, Guid relatedActivityId, params object[] args) 
        { 
            Contract.Assert(m_eventData[eventId].Descriptor.EventId != 0);
#if ETW_SUPPORTED 
            if (m_provider != null && m_eventData[eventId].EnabledForETW)
                m_provider.WriteTransferEvent(ref m_eventData[eventId].Descriptor, relatedActivityId, args);
#endif
            // 
            if (m_OutputStreams != null && m_eventData[eventId].EnabledForAnyStream)
                WriteToAllStreams(0, args); 
        } 

        // This method is intended to allow users to get at the 'raw' helpers if they need fine control 
        // over the event descriptor.
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", MessageId = "0#")]
        protected unsafe void WriteEvent(ref EventDescriptorInternal descriptor, params object[] args)
        { 
            Contract.Assert(m_eventData[descriptor.EventId].Descriptor.EventId != 0);
#if ETW_SUPPORTED 
            if (m_provider != null && m_eventData[descriptor.EventId].EnabledForETW) 
                m_provider.WriteEvent(ref descriptor, args);
#endif 
            if (m_OutputStreams != null && m_eventData[descriptor.EventId].EnabledForAnyStream)
                WriteToAllStreams(descriptor.EventId, args);
        }
 
        /// <summary>
        /// Returns a number one greater than the largest event Id for the provider. 
        /// </summary> 
        protected internal int EventIdLimit { get { InsureInitialized(); return m_eventData.Length; } }
 
        protected void SetEnabled(EventProviderDataStream outputStream, int eventId, bool value)
        {
            if (outputStream == null)
            { 
                Contract.Assert(m_providerEnabled || !value, "m_providerEnabled || !value");
                m_eventData[eventId].EnabledForETW = value; 
            } 
            else
            { 
                outputStream.m_EventEnabled[eventId] = value;
                if (value)
                {
                    m_providerEnabled = true; 
                    m_eventData[eventId].EnabledForAnyStream = true;
                } 
                else 
                {
                    m_eventData[eventId].EnabledForAnyStream = false; 
                    for (EventProviderDataStream stream = m_OutputStreams; stream != null; stream = stream.m_Next)
                        if (stream.m_EventEnabled[eventId])
                        {
                            m_eventData[eventId].EnabledForAnyStream = true; 
                            break;
                        } 
                } 
            }
        } 

        protected bool IsEnabled(EventProviderDataStream outputStream, int eventId)
        {
            return outputStream.m_EventEnabled[eventId]; 
        }
 
        /// <summary> 
        /// This indicates whether the subclass is a debug build or not (whether more validation should be
        /// done).  By default it will do validation.   It is suggested that for release builds you override 
        /// this function to return false.
        /// </summary>
        protected virtual bool DoDebugChecks() { return true; }
        #endregion 

        #region IDisposable Members 
        public void Dispose() 
        {
            this.Dispose(true); 
            GC.SuppressFinalize(this);
        }
        /// <summary>
        /// Disposes of an EventProvider. 
        /// </summary>
        /// <remarks> 
        /// Called from Dispose() with disposing=true, and from the finalizer (~MeasurementBlock) with disposing=false. 
        /// Guidelines:
        /// 1. We may be called more than once: do nothing after the first call. 
        /// 2. Avoid throwing exceptions if disposing is false, i.e. if we're being finalized.
        /// </remarks>
        /// <param name="disposing">True if called from Dispose(), false if called from the finalizer.</param>
        protected virtual void Dispose(bool disposing) 
        {
            if (disposing) 
            { 
#if ETW_SUPPORTED
                if (m_provider != null) 
                {
                    m_provider.Dispose();
                    m_provider = null;
                } 
#endif
            } 
        } 
        ~EventProviderBase()
        { 
            this.Dispose(false);
        }
        #endregion
 
        #region private
        private void WriteToAllStreams(int eventId, params object[] args) 
        { 
            m_eventCallbackArgs.EventId = eventId;
            m_eventCallbackArgs.Payload = args; 
            for (EventProviderDataStream outputStream = m_OutputStreams; outputStream != null; outputStream = outputStream.m_Next)
            {
                if (outputStream.m_EventEnabled[eventId])
                    outputStream.m_Callback(m_eventCallbackArgs); 
            }
            if (m_eventData[eventId].CaptureStack) 
                CaptureStack(); 
        }
 
        // Send out an event that captures the stack trace.
        private void CaptureStack()
        {
            // 
        }
 
        /// <summary> 
        /// Returns true if 'eventNum' is enabled if you only consider the levle and matchAnyKeyword filters.
        /// It is possible that providers turn off the event based on additional filtering criteria. 
        /// </summary>
        private bool IsEnabledDefault(int eventNum, EventLevel currentLevel, EventKeywords currentMatchAnyKeyword)
        {
            if (!m_providerEnabled) 
                return false;
 
            EventLevel eventLevel = (EventLevel)m_eventData[eventNum].Descriptor.Level; 
            EventKeywords eventKeywords = (EventKeywords)m_eventData[eventNum].Descriptor.Keywords;
 
            if ((eventLevel <= currentLevel) || (currentLevel == 0))
            {
                if ((eventKeywords == 0) || ((eventKeywords & currentMatchAnyKeyword) != 0))
                    return true; 
            }
            return false; 
        } 

#if ETW_SUPPORTED 
        /// <summary>
        /// This class lets us hook the 'OnControllerCommand' from the provider.
        /// </summary>
        private class OverideEventProvider : EventProvider 
        {
            public OverideEventProvider(EventProviderBase eventProvider) 
            { 
                this.m_eventProviderBase = eventProvider;
            } 
            protected override void OnControllerCommand(ControllerCommand command, IDictionary<string, string> arguments)
            {
                // We use null to represent the ETW EventProviderDataStream.  We may want change this if it
                // is too confusing, but it avoids making another sentinal. 
                EventProviderDataStream etwStream = null;
                m_eventProviderBase.SendCommand(etwStream, IsEnabled(), Level, MatchAnyKeyword, command, arguments); 
            } 
            private EventProviderBase m_eventProviderBase;
        } 
#endif

        /// <summary>
        /// Used to hold all the static information about an event.  This includes everything in the event 
        /// descriptor as well as some stuff we added specifically for EventProviderBase. see the
        /// code:m_eventData for where we use this. 
        /// </summary> 
        internal struct EventData
        { 
            public EventDescriptorInternal Descriptor;
            public string Message;
            public bool EnabledForAnyStream;        // true if any stream has this event turned on
            public bool EnabledForETW;              // is this event on for the OS ETW data stream? 
            public bool CaptureStack;               // Should we caputure stack traces for this event?
        }; 
 
        [System.Security.SecuritySafeCritical]
        internal void SendCommand(EventProviderDataStream outputStream, bool enable, EventLevel level, EventKeywords matchAnyKeyword, ControllerCommand command, IDictionary<string, string> commandArguments) 
        {
            InsureInitialized();
            if (m_OutputStreams != null && m_eventCallbackArgs == null)
                m_eventCallbackArgs = new EventWrittenEventArgs(this); 

            m_providerEnabled = enable; 
            m_level = level; 
            m_matchAnyKeyword = matchAnyKeyword;
 
            // Find the per-Provider stream cooresponding to registered outputStream
            EventProviderDataStream providerOutputStream = m_OutputStreams;
            if (providerOutputStream != null)
            { 
                for (; ; )
                { 
                    if (providerOutputStream == null) 
                        throw new ArgumentException("outputStream not found");
                    if (providerOutputStream.m_MasterStream == outputStream) 
                        break;
                    providerOutputStream = providerOutputStream.m_Next;
                }
            } 

            if (enable) 
            { 
                // Send the manifest if the stream once per stream
                if (providerOutputStream != null) 
                {
                    if (!providerOutputStream.m_ManifestSent)
                    {
                        providerOutputStream.m_ManifestSent = true; 
                        SendManifest(m_rawManifest, providerOutputStream);
                    } 
                } 
                else
                { 
                    // providerOutputStream == null means this is the ETW manifest
                    // If we are not completely initalized we can't send the manifest because WriteEvent
                    // will fail (handle was not returned from OS API).  We will try again after the
                    // constuctor completes. 
                    if (!m_ETWManifestSent && m_completelyInited)
                    { 
                        m_ETWManifestSent = true; 
                        SendManifest(m_rawManifest, providerOutputStream);
                    } 
                }
            }
            else
            {   // 
                if (providerOutputStream != null)
                    providerOutputStream.m_ManifestSent = false; 
                else 
                    m_ETWManifestSent = false;
            } 

            // Set it up using the 'standard' filtering bitfields
            for (int i = 0; i < m_eventData.Length; i++)
                SetEnabled(providerOutputStream, i, IsEnabledDefault(i, level, matchAnyKeyword)); 

            if (commandArguments == null) 
                commandArguments = new Dictionary<string, string>(); 

            // Allow subclasses to fiddle with it from there. 
            OnControllerCommand(providerOutputStream, command, commandArguments);

        }
 
        [System.Security.SecuritySafeCritical]
        private void InsureInitialized() 
        { 
            //
            if (m_rawManifest == null) 
            {
                lock (this)
                {
                    if (m_rawManifest == null) 
                    {
                        Contract.Assert(m_rawManifest == null); 
                        m_rawManifest = CreateManifestAndDescriptors(""); 
                    }
                } 
            }
        }

 
        [System.Security.SecuritySafeCritical]
        private unsafe bool SendManifest(byte[] rawManifest, EventProviderDataStream outputStream) 
        { 
            fixed (byte* dataPtr = rawManifest)
            { 
                EventDescriptorInternal manifestDescr = new EventDescriptorInternal(0xFFFE, 1, 0, 0, 0xFE, 0, -1);
                ManifestEnvelope envelope = new ManifestEnvelope();

                envelope.Format = ManifestEnvelope.ManifestFormats.SimpleXmlFormat; 
                envelope.MajorVersion = 1;
                envelope.MinorVersion = 0; 
                envelope.Magic = 0x5B;              // An unusual number that can be checked for consistancy. 
                int dataLeft = rawManifest.Length;
                envelope.TotalChunks = (ushort)((dataLeft + (ManifestEnvelope.MaxChunkSize - 1)) / ManifestEnvelope.MaxChunkSize); 
                envelope.ChunkNumber = 0;

                EventProvider.EventData* dataDescrs = stackalloc EventProvider.EventData[2];
                dataDescrs[0].Ptr = (ulong)&envelope; 
                dataDescrs[0].Size = (uint)sizeof(ManifestEnvelope);
                dataDescrs[0].Reserved = 0; 
 
                dataDescrs[1].Ptr = (ulong)dataPtr;
                dataDescrs[1].Reserved = 0; 

                bool success = true;
                while (dataLeft > 0)
                { 
                    dataDescrs[1].Size = (uint)Math.Min(dataLeft, ManifestEnvelope.MaxChunkSize);
#if ETW_SUPPORTED 
                    if (outputStream == null && m_provider != null && !m_provider.WriteEvent(ref manifestDescr, 2, (IntPtr)dataDescrs)) 
                        success = false;
#endif 
                    if (outputStream != null)
                    {
                        byte[] envelopeBlob = null;
                        byte[] manifestBlob = null; 
                        if (envelopeBlob == null)
                        { 
                            envelopeBlob = new byte[dataDescrs[0].Size]; 
                            manifestBlob = new byte[dataDescrs[1].Size];
                        } 
                        System.Runtime.InteropServices.Marshal.Copy((IntPtr)dataDescrs[0].Ptr, envelopeBlob, 0, (int)dataDescrs[0].Size);
                        System.Runtime.InteropServices.Marshal.Copy((IntPtr)dataDescrs[1].Ptr, manifestBlob, 0, (int)dataDescrs[1].Size);

                        m_eventCallbackArgs.EventId = manifestDescr.EventId; 
                        m_eventCallbackArgs.Payload = new object[] { envelopeBlob, manifestBlob };
                        outputStream.m_Callback(m_eventCallbackArgs); 
                    } 

                    dataLeft -= ManifestEnvelope.MaxChunkSize; 
                    envelope.ChunkNumber++;
                }
                return success;
            } 
        }
 
        [System.Security.SecuritySafeCritical] 
        private byte[] CreateManifestAndDescriptors(string providerDllName)
        { 
            Type providerType = this.GetType();
            MethodInfo[] methods = providerType.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            EventAttribute defaultEventAttribute = new EventAttribute(0);
            int eventId = 1;        // The number given to an event that does not have a explicitly given ID. 
            m_eventData = new EventData[methods.Length];
            ManifestBuilder manifest = new ManifestBuilder(Name, Guid, providerDllName); 
 
            // Collect task, opcode, keyword and channel information
            FieldInfo[] staticFields = providerType.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static); 
            if (staticFields.Length > 0)
            {
                foreach (FieldInfo staticField in staticFields)
                { 
                    Type staticFieldType = staticField.FieldType;
                    if (staticFieldType == typeof(EventOpcode)) 
                        manifest.AddOpcode(staticField.Name, (int)staticField.GetRawConstantValue()); 
                    else if (staticFieldType == typeof(EventTask))
                        manifest.AddTask(staticField.Name, (int)staticField.GetRawConstantValue()); 
                    else if (staticFieldType == typeof(EventKeywords))
                        manifest.AddKeyword(staticField.Name, (ulong)(long)staticField.GetRawConstantValue());
                    else if (staticFieldType == typeof(EventChannel))
                        manifest.AddChannel(staticField.Name, (int)staticField.GetRawConstantValue()); 
                }
            } 
 
            for (int i = 0; i < methods.Length; i++)
            { 
                MethodInfo method = methods[i];
                ParameterInfo[] args = method.GetParameters();

                // Get the EventDescriptorInternal (from the Custom attributes) 
                EventAttribute eventAttribute = (EventAttribute)Attribute.GetCustomAttribute(method, typeof(EventAttribute), false);
 
                // Methods that don't return void can't be events. 
                if (method.ReturnType != typeof(void))
                { 
                    if (eventAttribute != null && DoDebugChecks())
                        throw new ArgumentException("Event attribute placed on method " + method.Name + " which does not return 'void'");
                    continue;
                } 
                if (method.IsVirtual || method.IsStatic)
                { 
                    continue; 
                }
 
                if (eventAttribute == null)
                {
                    // If we explictly mark the method as not being an event, then honor that.
                    if (Attribute.GetCustomAttribute(method, typeof(NonEventAttribute), false) != null) 
                        continue;
 
                    defaultEventAttribute.EventId = eventId; 
                    defaultEventAttribute.Opcode = EventOpcode.Info;
                    defaultEventAttribute.Task = EventTask.None; 
                    eventAttribute = defaultEventAttribute;
                }
                else if (eventAttribute.EventId <= 0)
                    throw new ArgumentException("Event IDs <= 0 are illegal."); 
                eventId++;
 
                if (eventAttribute.Opcode == EventOpcode.Info && eventAttribute.Task == EventTask.None) 
                    eventAttribute.Opcode = (EventOpcode)(10 + eventAttribute.EventId);
 
                manifest.StartEvent(method.Name, eventAttribute);
                for (int fieldIdx = 0; fieldIdx < args.Length; fieldIdx++)
                    manifest.AddEventParameter(args[fieldIdx].ParameterType, args[fieldIdx].Name);
                manifest.EndEvent(); 

                if (DoDebugChecks()) 
                    DebugCheckEvent(method, eventAttribute); 
                AddEventDescriptor(eventAttribute);
            } 
            TrimEventDescriptors();
            m_eventsByName = null;

            return manifest.CreateManifest(); 
        }
 
        [System.Security.SecuritySafeCritical] 
        private void AddEventDescriptor(EventAttribute eventAttribute)
        { 
            if (m_eventData == null || m_eventData.Length <= eventAttribute.EventId)
            {
                EventData[] newValues = new EventData[m_eventData.Length + 16];
                Array.Copy(m_eventData, newValues, m_eventData.Length); 
                m_eventData = newValues;
            } 
            m_eventData[eventAttribute.EventId].Descriptor = new EventDescriptorInternal( 
                    eventAttribute.EventId,
                    eventAttribute.Version, 
                    (byte)eventAttribute.Channel,
                    (byte)eventAttribute.Level,
                    (byte)eventAttribute.Opcode,
                    (int)eventAttribute.Task, 
                    (long)eventAttribute.Keywords);
 
            m_eventData[eventAttribute.EventId].CaptureStack = eventAttribute.CaptureStack; 
            m_eventData[eventAttribute.EventId].Message = eventAttribute.Message;
        } 

        [System.Security.SecuritySafeCritical]
        private void TrimEventDescriptors()
        { 
            int idx = m_eventData.Length;
            while (0 < idx) 
            { 
                --idx;
                if (m_eventData[idx].Descriptor.EventId != 0) 
                    break;
            }
            if (m_eventData.Length - idx > 2)      // allow one wasted slot.
            { 
                EventData[] newValues = new EventData[idx + 1];
                Array.Copy(m_eventData, newValues, newValues.Length); 
                m_eventData = newValues; 
            }
        } 

        private void DebugCheckEvent(MethodInfo method, EventAttribute eventAttribute)
        {
            int eventArg = GetHelperCallFirstArg(method); 
            if (eventArg >= 0 && eventAttribute.EventId != eventArg)
            { 
                throw new ArgumentException("Error: event " + method.Name + " is given event ID " + 
                    eventAttribute.EventId + " but " + eventArg + " was passed to the helper.");
            } 

            if (eventAttribute.EventId < m_eventData.Length && m_eventData[eventAttribute.EventId].Descriptor.EventId != 0)
            {
                throw new ArgumentException("Event " + method.Name + " has ID " + eventAttribute.EventId + 
                    " which is the same as a previously defined event.");
            } 
 
            if (m_eventsByName == null)
                m_eventsByName = new Dictionary<string, string>(); 

            if (m_eventsByName.ContainsKey(method.Name))
                throw new ArgumentException("Event name " + method.Name + " used more than once.  " +
                    "If you wish to overload a method, the overloaded method should have a " + 
                    "[Event(-1)] attribute to indicate the method should not have associated meta-data.");
 
            m_eventsByName[method.Name] = method.Name; 
        }
 
        /// <summary>
        /// This method looks at the IL and tries to pattern match against the standard
        /// 'boilerplate' event body
        /// 
        ///     { if (Enabled()) WriteEvent(#, ...) }
        /// 
        /// If the pattern matches, it returns the literal number passed as the first parameter to 
        /// the WriteEvent.  This is used to find common user errors (mismatching this
        /// number with the EventAttribute ID).  It is only used for validation. 
        /// </summary>
        /// <param name="method">The method to probe.</param>
        /// <returns>The literal value or -1 if the value could not be determined. </returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity", Justification = "Switch statement is clearer than alternatives")] 
        static private int GetHelperCallFirstArg(MethodInfo method)
        { 
            // Currently searches for the following pattern 
            //
            // ...     // CAN ONLY BE THE INSTRUCTIONS BELOW 
            // LDARG0
            // LDC.I4 XXX
            // ...     // CAN ONLY BE THE INSTRUCTIONS BELOW CAN'T BE A BRANCH OR A CALL
            // CALL 
            // NOP     // 0 or more times
            // RET 
            // 
            // If we find this pattern we return the XXX.  Otherwise we return -1.
            byte[] instrs = method.GetMethodBody().GetILAsByteArray(); 
            int retVal = -1;
            for (int idx = 0; idx < instrs.Length; )
            {
                switch (instrs[idx]) 
                {
                    case 0: // NOP 
                    case 1: // BREAK 
                    case 2: // LDARG_0
                    case 3: // LDARG_1 
                    case 4: // LDARG_2
                    case 5: // LDARG_3
                    case 6: // LDLOC_0
                    case 7: // LDLOC_1 
                    case 8: // LDLOC_2
                    case 9: // LDLOC_3 
                    case 10: // STLOC_0 
                    case 11: // STLOC_1
                    case 12: // STLOC_2 
                    case 13: // STLOC_3
                        break;
                    case 14: // LDARG_S
                    case 16: // STARG_S 
                        idx++;
                        break; 
                    case 20: // LDNULL 
                        break;
                    case 21: // LDC_I4_M1 
                    case 22: // LDC_I4_0
                    case 23: // LDC_I4_1
                    case 24: // LDC_I4_2
                    case 25: // LDC_I4_3 
                    case 26: // LDC_I4_4
                    case 27: // LDC_I4_5 
                    case 28: // LDC_I4_6 
                    case 29: // LDC_I4_7
                    case 30: // LDC_I4_8 
                        if (idx > 0 && instrs[idx - 1] == 2)  // preceeded by LDARG0
                            retVal = instrs[idx] - 22;
                        break;
                    case 31: // LDC_I4_S 
                        if (idx > 0 && instrs[idx - 1] == 2)  // preceeded by LDARG0
                            retVal = instrs[idx + 1]; 
                        idx++; 
                        break;
                    case 32: // LDC_I4 
                        idx += 4;
                        break;
                    case 37: // DUP
                        break; 
                    case 40: // CALL
                        idx += 4; 
 
                        if (retVal >= 0)
                        { 
                            // Is this call just before return?
                            for (int search = idx + 1; search < instrs.Length; search++)
                            {
                                if (instrs[search] == 42)  // RET 
                                    return retVal;
                                if (instrs[search] != 0)   // NOP 
                                    break; 
                            }
                        } 
                        retVal = -1;
                        break;
                    case 44: // BRFALSE_S
                    case 45: // BRTRUE_S 
                        retVal = -1;
                        idx++; 
                        break; 
                    case 57: // BRFALSE
                    case 58: // BRTRUE 
                        retVal = -1;
                        idx += 4;
                        break;
                    case 103: // CONV_I1 
                    case 104: // CONV_I2
                    case 105: // CONV_I4 
                    case 106: // CONV_I8 
                    case 109: // CONV_U4
                    case 110: // CONV_U8 
                        break;
                    case 140: // BOX
                    case 141: // NEWARR
                        idx += 4; 
                        break;
                    case 162: // STELEM_REF 
                        break; 
                    case 254: // PREFIX
                        idx++; 
                        // Covers the CEQ instructions used in debug code for some reason.
                        if (idx >= instrs.Length || instrs[idx] >= 6)
                            goto default;
                        break; 
                    default:
 //                       Contract.Assert(false, "Warning: User validation code sub-optimial: Unsuported opcode " + instrs[idx] + 
 //                          " at " + idx + " in method " + method.Name); 
                        return -1;
                } 
                idx++;
            }
            return -1;
        } 

        // private instance state 
        private string m_name;                          // My friendly name (privided in ctor) 
        internal int m_id;                              // A small integer that is unique to this instance.
        private Guid m_guid;                            // GUID representing the ETW providerBase to the OS. 
        internal EventData[] m_eventData;               // All per-event data
        private byte[] m_rawManifest;                   // Bytes to send out representing the event schema

        // Enabling bits 
        private bool m_providerEnabled;                 // am I enabled (any of my events are enabled)
        private bool m_ETWManifestSent;              // we could not send the ETW manifest as an event in the callback 
        private bool m_completelyInited;                // The EventProviderBase constructor has returned. 
        internal EventLevel m_level;            // lowest level enabled by any output stream
        internal EventKeywords m_matchAnyKeyword;// the logical OR of all levels enabled by any output stream. 

        private EventWrittenEventArgs m_eventCallbackArgs; // Passed to the callback (Basically reusing the instance again and again)

        internal EventProviderDataStream m_OutputStreams;  // Linked list of streams we write the data to (we also do ETW specially) 
#if ETW_SUPPORTED
        private OverideEventProvider m_provider;        // We have special support for ETW. 
#endif 

        // m_eventsByName is on used for error checking, when DoDebugChecks indicate validation 
        // should be done.  In this case m_eventsByName is just a set of events names that
        // currently exist (value is same as key).
        Dictionary<string, string> m_eventsByName;
        #endregion 
    }
 
    /// <summary> 
    /// An EventDataStream represents the 'output' stream generated from all subclasses of EventProviderBase in the
    /// current appdomain.  The delegate that was passed to the 'Register' method will be called back each 
    /// time an event is logged if the provider has been turned on for that stream.
    ///
    /// Each stream is logically independent from the other streams.   Each stream can send commands to the
    /// providers (using the SendCommand method), and in those commands set filtering criteria.   The 
    /// filtering criteria from one stream does not affect other streams.
    /// 
    /// Logging be turned off by using 'SendCommand'.  When the stream is no longer going to be used the 'UnRegister' 
    /// method will disconnect the stream from the event providers.
    /// </summary> 
    internal class EventProviderDataStream : IDisposable
    {
        public static EventProviderDataStream Register(Action<EventWrittenEventArgs> callBack)
        { 
            lock (EventProviderStreamsLock)
            { 
                // Add the outputStream to the global list of EventProviderDataStreams. 
                EventProviderDataStream ret = new EventProviderDataStream(callBack, s_Streams, null, null);
                s_Streams = ret; 

                // Add the outputStream  to each existing EventProvider.
                foreach (WeakReference providerRef in s_providers)
                { 
                    EventProviderBase providerBase = providerRef.Target as EventProviderBase;
                    if (providerBase != null) 
                        providerBase.m_OutputStreams = new EventProviderDataStream(callBack, providerBase.m_OutputStreams, new bool[providerBase.EventIdLimit], ret); 
                }
 
                return ret;
            }
        }
 
        public void UnRegister()
        { 
            lock (EventProviderStreamsLock) 
            {
                if (s_Streams != null) 
                {
                    if (this == s_Streams)
                        s_Streams = this.m_Next;
                    else 
                    {
                        // Remove 'this' from the s_Streams linked list. 
                        EventProviderDataStream prev = s_Streams; 
                        for (; ; )
                        { 
                            EventProviderDataStream cur = prev.m_Next;
                            if (cur == null)
                                break;
                            if (cur == this) 
                            {
                                prev.m_Next = cur.m_Next;       // Remove entry. 
                                RemoveStreamFromProviders(cur); 
                                break;
                            } 
                            prev = cur;
                        }
                    }
                } 
            }
        } 
 
        /// <summary>
        /// A small integer (suitable for indexing in an array) identifying this provider.  It is unique 
        /// per-appdomain.  This allows the callback registered in 'Register' to efficiently attach addition
        /// information to a provider.
        /// </summary>
        public static int ProviderId(EventProviderBase provider) { return provider.m_id; } 

        /// <summary> 
        /// Send a command to a provider (from a particular stream).  The most important command is simply 
        /// to turn a particular provider on or off.   However particular providers might implement
        /// additional commands (like dumping particular data structures in response to a command. 
        /// </summary>
        /// <param name="providerGuid">The GUID for the event provider that the command is to be sent to.
        /// Guid.Empty is defined to be a wildcard that means all providers.  </param>
        /// <param name="enable">Indicates if the provider is to be turned on or off.  The rest of the 
        /// arguments are only appicable if enabled=true.</param>
        /// <param name="level">The level (verbosity) of the logging desired.</param> 
        /// <param name="matchAnyKeyword">Providers define groups of events and each such group is given a 
        /// bit in a 64 bit enumeration.  This parameter indicates which groups should be truned on.  Events
        /// have are not assigned any group are never filtered by the matchAnyKeyword value.</param> 
        /// <param name="command">The command to be given to the provider.  Typically it is Command.Update.
        /// Individual providers can define their own commands</param>
        /// <param name="commandArguments">This is a set of key-value pairs that are interpreted by the
        /// provider.  It is a way of passing arbitrary arguments to the provider when issuing a command</param> 
        public void SendCommand(Guid providerGuid, bool enable, EventLevel level, EventKeywords matchAnyKeyword, ControllerCommand command, IDictionary<string, string> commandArguments)
        { 
            // Copy it to list so I can make the callback without holding the EventProviderStreamsLock. 
            List<EventProviderBase> providerBases = new List<EventProviderBase>();
            lock (EventProviderStreamsLock) 
            {
                foreach (WeakReference providerRef in s_providers)
                {
                    EventProviderBase providerBase = providerRef.Target as EventProviderBase; 
                    if (providerBase != null && (providerBase.Guid == providerGuid || providerGuid == Guid.Empty))
                        providerBases.Add(providerBase); 
                } 
            }
 
            foreach (EventProviderBase providerBase in providerBases)
                providerBase.SendCommand(this, enable, level, matchAnyKeyword, command, commandArguments);
        }
        public void SendCommand(Guid providerGuid, bool enable, EventLevel level) 
        {
            SendCommand(providerGuid, enable, level, EventKeywords.None, ControllerCommand.Update, null); 
        } 

        /// <summary> 
        /// The code:EventProviderCreated is fired whenever provider (a subclass of
        /// code:EventProviderBase) is created.   Notifications from this event are 'retroactive,
        /// meaning that when a new handler is added, a callback is immediately issued for all
        /// providers that were created before the callback was registered. 
        /// </summary>
        public static event EventHandler<EventProviderCreatedEventArgs> EventProviderCreated 
        { 
            add
            { 
                List<EventProviderBase> providerBases = new List<EventProviderBase>();
                lock (EventProviderStreamsLock)
                {
                    // Add new delegate to the 
                    s_EventProviderCreated = (EventHandler<EventProviderCreatedEventArgs>)Delegate.Combine(s_EventProviderCreated, value);
 
                    // Make a copy so I can send events outside the lock 
                    foreach (WeakReference providerRef in s_providers)
                    { 
                        EventProviderBase providerBase = providerRef.Target as EventProviderBase;
                        if (providerBase != null)
                            providerBases.Add(providerBase);
                    } 
                }
                // Send the callback for all existing eventsProviders to 'catch up' (outside the lock) 
                foreach (EventProviderBase providerBase in providerBases) 
                    value(null, new EventProviderCreatedEventArgs { Provider = providerBase });
            } 
            remove
            {
                s_EventProviderCreated = (EventHandler<EventProviderCreatedEventArgs>)Delegate.Remove(s_EventProviderCreated, value);
            } 
        }
 
        #region private 
        void IDisposable.Dispose()
        { 
            UnRegister();
        }
        /// <summary>
        /// This routine adds this to the global list of providers, it also assigns the ID to the 
        /// provider (which is simply the oridinal in the global list)
        /// </summary> 
        /// <param name="newProvider"></param> 
        internal static void AddProvider(EventProviderBase newProvider)
        { 
            lock (EventProviderStreamsLock)
            {
                if (s_providers == null)
                    s_providers = new List<WeakReference>(2); 

                // Periodically search the list for existing entries to reuse, this avoids 
                // unbounded memory use if we keep recycling providers (an unlikely thing). 
                int newIndex = -1;
                if (s_providers.Count % 64 == 63) 
                {
                    for (int i = 0; i < s_providers.Count; i++)
                    {
                        WeakReference weakRef = s_providers[i]; 
                        if (!weakRef.IsAlive)
                        { 
                            newIndex = i; 
                            weakRef.Target = newProvider;
                            break; 
                        }
                    }
                }
                if (newIndex < 0) 
                {
                    newIndex = s_providers.Count; 
                    s_providers.Add(new WeakReference(newProvider)); 
                }
                newProvider.m_id = newIndex; 

                // Add every existing outputStream to the new Provider
                for (EventProviderDataStream outputStream = s_Streams; outputStream != null; outputStream = outputStream.m_Next)
                    newProvider.m_OutputStreams = new EventProviderDataStream(outputStream.m_Callback, newProvider.m_OutputStreams, new bool[newProvider.EventIdLimit], outputStream); 

                // Put in a local to avoid ---- with null check. 
                EventHandler<EventProviderCreatedEventArgs> callback = s_EventProviderCreated; 
                if (callback != null)
                    callback(null, new EventProviderCreatedEventArgs { Provider = newProvider }); 
            }
        }
        private void RemoveStreamFromProviders(EventProviderDataStream streamToRemove)
        { 
            // Foreach existing Provider
            foreach (WeakReference providerRef in s_providers) 
            { 
                EventProviderBase providerBase = providerRef.Target as EventProviderBase;
                if (providerBase != null) 
                {
                    // Is the first output outputStream the outputStream we are removing?
                    if (providerBase.m_OutputStreams.m_MasterStream == streamToRemove)
                        providerBase.m_OutputStreams = providerBase.m_OutputStreams.m_Next; 
                    else
                    { 
                        // Remove 'streamToRemove' from the providerBase.m_OutputStreams linked list. 
                        EventProviderDataStream prev = providerBase.m_OutputStreams;
                        for (; ; ) 
                        {
                            EventProviderDataStream cur = prev.m_Next;
                            if (cur == null)
                            { 
                                Contract.Assert(false, "Provider did not have a registers EventProviderDataStream!");
                                break; 
                            } 
                            if (cur.m_MasterStream == streamToRemove)
                            { 
                                prev.m_Next = cur.m_Next;       // Remove entry.
                                break;
                            }
                            prev = cur; 
                        }
                    } 
                } 
            }
        } 

        // These conditions need to hold most of the time.
        [Conditional("DEBUG")]
        static void Validate() 
        {
            // 
        } 
        internal EventProviderDataStream(Action<EventWrittenEventArgs> callBack, EventProviderDataStream next, bool[] eventEnabled, EventProviderDataStream masterStream)
        { 
            m_Callback = callBack;
            m_Next = next;
            m_EventEnabled = eventEnabled;
            m_MasterStream = masterStream; 
        }
 
        internal static object EventProviderStreamsLock 
        {
            get 
            {
                if (s_Lock == null)
                    Interlocked.CompareExchange(ref s_Lock, new object(), null);
                return s_Lock; 
            }
        } 
 
        // Instance fields
        internal Action<EventWrittenEventArgs> m_Callback; 
        internal EventProviderDataStream m_Next;               // These form a linked list
        internal bool m_ManifestSent;                          // Have we sent the manifest?

        internal EventProviderDataStream m_MasterStream;       // If this is a per-Provider outputStream, this is a link 
        // to the outputStream in the code:s_Streams list
        internal bool[] m_EventEnabled;                        // For every event in a Provider, 
 
        // static fields
        internal static EventProviderDataStream s_Streams;     // list of all EventProviderDataStreams in the appdomain 
        internal static object s_Lock;                         // lock for s_streams;
        private static List<WeakReference> s_providers;        // all EventProviders in the appdomain

        static EventHandler<EventProviderCreatedEventArgs> s_EventProviderCreated;  // delegate for creating events. 
        #endregion
    } 
 
    /// <summary>
    /// code:EventWrittenEventArgs is passed when the callback given in code:EventProviderDataStream.Register is 
    /// fired.
    /// </summary>
    internal class EventWrittenEventArgs : EventArgs
    { 
        public int EventId { get; internal set; }
        public object[] Payload { get; internal set; } 
        public string Message 
        {
            get 
            {
                string message = null;
                if ((uint)EventId < (uint)m_providerBase.m_eventData.Length)
                    message = m_providerBase.m_eventData[EventId].Message; 
                if (message != null)
                    return string.Format(CultureInfo.InvariantCulture, message, Payload);        // 
                else if (EventId == 0 && Payload.Length == 1 && Payload[0].GetType() == typeof(string)) // This is for WriteMessage 
                    return Payload[0].ToString();
                else 
                    return null;
            }
        }
        public EventProviderBase Provider { get { return m_providerBase; } } 
        public EventDescriptorInternal Descriptor { get { return m_providerBase.m_eventData[EventId].Descriptor; } }
        public EventLevel Level 
        { 
            get {
                if ((uint)EventId >= (uint)m_providerBase.m_eventData.Length) 
                    return EventLevel.LogAlways;
            return (EventLevel)m_providerBase.m_eventData[EventId].Descriptor.Level; }
        }
        #region private 
        internal EventWrittenEventArgs(EventProviderBase providerBase)
        { 
            m_providerBase = providerBase; 
        }
        private EventProviderBase m_providerBase; 
        #endregion
    }

    /// <summary> 
    /// code:EventProviderCreatedEventArgs is passed when the code:EventProviderDataStream.EventProviderCreated
    /// is fired. 
    /// </summary> 
    internal class EventProviderCreatedEventArgs : EventArgs
    { 
        public EventProviderBase Provider;
    }

    /// <summary> 
    /// All instance methods in a class that subclasses code:EventProviderBasethat and return void
    /// are assumed to be methods that generate an event.  Enough information can be deduced from the name 
    /// of the method and its signature to generate basic schema information for the event.  The 
    /// code:EventAttribute allows you to specify additional event schema information for an event if
    /// desired. 
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    [System.Runtime.CompilerServices.FriendAccessAllowed]
    internal sealed class EventAttribute : Attribute 
    {
        public EventAttribute(int eventId) { this.EventId = eventId; } 
        public int EventId { get; internal set; } 
        public string Name { get; set; }
        public EventLevel Level { get; set; } 
        public EventKeywords Keywords { get; set; }
        public EventOpcode Opcode { get; set; }
        public EventTask Task { get; set; }
        public EventChannel Channel { get; set; } 
        public byte Version { get; set; }
        public bool CaptureStack { get; set; } 
 
        /// <summary>
        /// This is also used for m_OutputStreams compatabilty.  If code:EventProviderBase.TraceSourceSupport is 
        /// on events will also be logged a tracesource with the same name as the providerBase.  If this
        /// property is set then the payload will go to code:TraceSource.TraceEvent, and this string
        /// will be used as the message.  if this property is not set not set it goes to
        /// code:TraceSource.TraceData 
        /// </summary>
        public string Message { get; set; } 
        /// <summary> 
        /// Like Message, but you specify the resource ID of the string you wish to fetch.  It will use the
        /// ResourceManager to look this resource up in the resources associated with the providerBase type 
        /// </summary>
        public string MessageResourceId { get; set; }
    }
 
    /// <summary>
    /// By default all instance methods in a class that subclasses code:EventProviderBasethat and return void 
    /// are assumed to be methods that generate an event. This default can be overriden by specifying the 
    /// code:NonEventAttribute
    /// </summary> 
    [AttributeUsage(AttributeTargets.Method)]
    [System.Runtime.CompilerServices.FriendAccessAllowed]
    internal sealed class NonEventAttribute : Attribute
    { 
        public NonEventAttribute() { }
    } 
 
    #region private classes
    /// <summary> 
    /// ManifestBuilder is designed to isolate the details of the message of the event from the
    /// rest of EventProviderBase.  This one happens to create XML.
    /// </summary>
    internal class ManifestBuilder 
    {
        public ManifestBuilder(string providerName, Guid providerGuid, string dllName) 
        { 
            this.providerGuid = providerGuid;
            sb = new StringBuilder(); 
            events = new StringBuilder();
            templates = new StringBuilder();
            opcodeTab = new Dictionary<int, string>();
 
            sb.Append("<provider name=\"").Append(providerName).
               Append("\" guid=\"{").Append(providerGuid.ToString()).Append("}"); 
            if (dllName != null) 
                sb.Append("\" resourceFileName=\"").Append(dllName).Append("\" messageFileName=\"").Append(dllName);
            sb.Append("\" symbol=\"").Append(providerName). 
               Append("\" >").AppendLine();
        }

        public void AddOpcode(string name, int value) 
        {
            opcodeTab[value] = name; 
        } 
        public void AddTask(string name, int value)
        { 
            if (taskTab == null)
                taskTab = new Dictionary<int, string>();
            taskTab[value] = name;
        } 
        public void AddKeyword(string name, ulong value)
        { 
            if ((value & (value - 1)) != 0)   // Is it a power of 2? 
                throw new ArgumentException("Value " + value.ToString("x", CultureInfo.CurrentCulture) + " for keyword " + name + " needs to be a power of 2.");
            if (keywordTab == null) 
                keywordTab = new Dictionary<ulong, string>();
            keywordTab[value] = name;
        }
        public void AddChannel(string name, int value) 
        {
            if (channelTab == null) 
                channelTab = new Dictionary<int, string>(); 
            channelTab[value] = name;
        } 

        public void StartEvent(string eventName, EventAttribute eventAttribute)
        {
            Contract.Assert(numParams == 0); 
            Contract.Assert(templateName == null);
            templateName = eventName + "Args"; 
            numParams = 0; 

            events.Append("  <event name=\"").Append(eventName).Append("\""). 
                // Append(" symbol=\"").Append(eventName).Append("\"").
                // Symbols have to be unique across all items (opcodes, tasks ...)
                //
                 Append(" value=\"").Append(eventAttribute.EventId).Append("\""). 
                 Append(" version=\"").Append(eventAttribute.Version).Append("\"").
                 Append(" level=\"").Append(GetLevelName(eventAttribute.Level)).Append("\""); 
            if (eventAttribute.Keywords != 0) 
                events.Append(" keywords=\"").Append(GetKeywords((ulong)eventAttribute.Keywords, eventName)).Append("\"");
            if (eventAttribute.Opcode != 0) 
                events.Append(" opcode=\"").Append(GetOpcodeName(eventAttribute.Opcode, eventName)).Append("\"");
            if (eventAttribute.Task != 0)
                events.Append(" task=\"").Append(GetTaskName(eventAttribute.Task, eventName)).Append("\"");
            if (eventAttribute.Channel != 0) 
                events.Append(" channel=\"").Append(GetChannelName(eventAttribute.Channel, eventName)).Append("\"");
        } 
        public void AddEventParameter(Type type, string name) 
        {
            if (numParams == 0) 
                templates.Append("  <template tid=\"").Append(templateName).Append("\">").AppendLine();
            numParams++;
            templates.Append("   <data name=\"").Append(name).Append("\" inType=\"").Append(GetTypeName(type)).Append("\"/>").AppendLine();
        } 
        public void EndEvent()
        { 
            if (numParams > 0) 
            {
                templates.Append("  </template>").AppendLine(); 
                events.Append(" template=\"").Append(templateName).Append("\"");
            }
            events.Append("/>").AppendLine();
 
            templateName = null;
            numParams = 0; 
        } 

        public byte[] CreateManifest() 
        {
            // Write out the channels
            if (channelTab != null)
            { 
                sb.Append(" <channels>").AppendLine();
                foreach (int channel in channelTab.Keys) 
                    sb.Append("  <channel name=\"").Append(channelTab[channel]).Append("\" value=\"").Append(channel).Append("\"/>").AppendLine(); 
                sb.Append(" </channels>").AppendLine();
            } 

            // Write out the tasks
            if (taskTab != null)
            { 

                sb.Append(" <tasks>").AppendLine(); 
                foreach (int task in taskTab.Keys) 
                {
                    Guid taskGuid = EventProvider.GenTaskGuidFromProviderGuid(providerGuid, (ushort)task); 
                    sb.Append("  <task name=\"").Append(taskTab[task]).
                        Append("\" eventGUID=\"{").Append(taskGuid.ToString()).Append("}").
                        Append("\" value=\"").Append(task).
                        Append("\"/>").AppendLine(); 
                }
                sb.Append(" </tasks>").AppendLine(); 
            } 

            // Write out the opcodes 
            sb.Append(" <opcodes>").AppendLine();
            foreach (int opcode in opcodeTab.Keys)
                sb.Append("  <opcode name=\"").Append(opcodeTab[opcode]).
                    Append("\" value=\"").Append(opcode). 
                    Append("\"/>").AppendLine();
            sb.Append(" </opcodes>").AppendLine(); 
 
            // Write out the keywords
            if (keywordTab != null) 
            {
                sb.Append(" <keywords>").AppendLine();
                foreach (ulong keyword in keywordTab.Keys)
                    sb.Append("  <keyword name=\"").Append(keywordTab[keyword]).Append("\" mask=\"").Append(keyword.ToString("x", CultureInfo.InvariantCulture)).Append("\"/>").AppendLine(); 
                sb.Append(" </keywords>").AppendLine();
            } 
 
            sb.Append(" <events>").AppendLine();
            sb.Append(events); 
            sb.Append(" </events>").AppendLine();

            if (templates.Length > 0)
            { 
                sb.Append(" <templates>").AppendLine();
                sb.Append(templates); 
                sb.Append(" </templates>").AppendLine(); 
            }
 
            sb.Append("</provider>").AppendLine();
            return Encoding.UTF8.GetBytes(sb.ToString());
        }
 
        #region private
 
        private static string GetLevelName(EventLevel level) 
        {
            // 
            return "win:" + level.ToString();
        }

        private string GetChannelName(EventChannel channel, string eventName) 
        {
            string ret = null; 
            if (channelTab == null || !channelTab.TryGetValue((int)channel, out ret)) 
                throw new ArgumentException("Use of undefined channel value " + channel + " for event " + eventName);
            return ret; 
        }

        private string GetTaskName(EventTask task, string eventName)
        { 
            string ret = null;
            if (taskTab == null || !taskTab.TryGetValue((int)task, out ret)) 
                throw new ArgumentException("Use of undefined task value " + task + " for event " + eventName); 
            return ret;
        } 

        private string GetOpcodeName(EventOpcode opcode, string eventName)
        {
            switch (opcode) 
            {
                case EventOpcode.Info: 
                    return "win:Info"; 
                case EventOpcode.Start:
                    return "win:Start"; 
                case EventOpcode.Stop:
                    return "win:Stop";
                case EventOpcode.DataCollectionStart:
                    return "win:DC_Start"; 
                case EventOpcode.DataCollectionStop:
                    return "win:DC_Stop"; 
                case EventOpcode.Extension: 
                    return "win:Extension";
                case EventOpcode.Reply: 
                    return "win:Reply";
                case EventOpcode.Resume:
                    return "win:Resume";
                case EventOpcode.Suspend: 
                    return "win:Suspend";
                case EventOpcode.Send: 
                    return "win:Send"; 
                case EventOpcode.Receive:
                    return "win:Receive"; 
            }
            //
            string ret = null;
            if (opcodeTab == null) 
                opcodeTab = new Dictionary<int, string>();
            if (!opcodeTab.TryGetValue((int)opcode, out ret)) 
                opcodeTab[(int)opcode] = eventName; 
            return eventName;
        } 

        private string GetKeywords(ulong keywords, string eventName)
        {
            string ret = ""; 
            for (ulong bit = 1; bit != 0; bit <<= 1)
            { 
                if ((keywords & bit) != 0) 
                {
                    string keyword; 
                    if (keywordTab == null || !keywordTab.TryGetValue(bit, out keyword))
                        throw new ArgumentException("Use of undefined keyword value " + bit.ToString("x", CultureInfo.CurrentCulture) + " for event " + eventName);
                    if (ret.Length != 0)
                        ret = ret + " "; 
                    ret = ret + keyword;
                } 
            } 
            return ret;
        } 
        private static string GetTypeName(Type type)
        {
            switch (Type.GetTypeCode(type))
            { 
                case TypeCode.Boolean:
                    return "win:Boolean"; 
                case TypeCode.Byte: 
                    return "win:Uint8";
                case TypeCode.UInt16: 
                    return "win:UInt16";
                case TypeCode.UInt32:
                    return "win:UInt32";
                case TypeCode.UInt64: 
                    return "win:UInt64";
                case TypeCode.SByte: 
                    return "win:Int8"; 
                case TypeCode.Int16:
                    return "win:Int16"; 
                case TypeCode.Int32:
                    return "win:Int32";
                case TypeCode.Int64:
                    return "win:Int64"; 
                case TypeCode.String:
                    return "win:UnicodeString"; 
                case TypeCode.Single: 
                    return "win:Float";
                case TypeCode.Double: 
                    return "win:Double";
                default:
                    if (type == typeof(Guid))
                        return "win:GUID"; 
                    throw new ArgumentException("Unsupported type " + type.Name);
            } 
        } 

        Dictionary<int, string> opcodeTab; 
        Dictionary<int, string> taskTab;
        Dictionary<int, string> channelTab;
        Dictionary<ulong, string> keywordTab;
 
        StringBuilder sb;
        StringBuilder events; 
        StringBuilder templates; 

        Guid providerGuid; 
        string templateName;
        int numParams;
        #endregion
    } 

    /// <summary> 
    /// Used to send the m_rawManifest into the event outputStream as a series of events. 
    /// </summary>
    internal struct ManifestEnvelope 
    {
        public const int MaxChunkSize = 0xFF00;
        public enum ManifestFormats : byte
        { 
            SimpleXmlFormat = 1,          // Simply dump what is under the <proivider> tag in an XML manifest
        } 
 
        public ManifestFormats Format;
        public byte MajorVersion; 
        public byte MinorVersion;
        public byte Magic;
        public ushort TotalChunks;
        public ushort ChunkNumber; 
    };
 
    #endregion 
}
 

