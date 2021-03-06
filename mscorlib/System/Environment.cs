// ==++== 
//
//   Copyright (c) Microsoft Corporation.  All rights reserved.
//
// ==--== 
/*============================================================
** 
** Class: Environment 
**
** 
** Purpose: Provides some basic access to some environment
** functionality.
**
** 
============================================================*/
namespace System { 
    using System.IO; 
    using System.Security;
    using System.Resources; 
    using System.Globalization;
    using System.Collections;
    using System.Security.Permissions;
    using System.Text; 
    using System.Configuration.Assemblies;
    using System.Runtime.InteropServices; 
    using System.Reflection; 
    using System.Diagnostics;
    using Microsoft.Win32; 
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Runtime.ConstrainedExecution;
    using System.Runtime.Versioning; 
    using System.Diagnostics.Contracts;
 
#if !FEATURE_PAL 
    [ComVisible(true)]
    public enum EnvironmentVariableTarget { 
        Process = 0,
#if FEATURE_WIN32_REGISTRY
        User = 1,
        Machine = 2, 
#endif
    } 
#endif 

#if FEATURE_SPLIT_RESOURCES 
    // See code:#splitResourceFeature
    internal enum ResourceHelperState {
        UNITIALIZED = 0,
        HAVE_RESOURCES = 1, 
        NO_RESOURCES = 2,
    } 
#endif // FEATURE_SPLIT_RESOURCES 

    [ComVisible(true)] 
    public static class Environment {

        // Assume the following constants include the terminating '\0' - use <, not <=
        const int MaxEnvVariableValueLength = 32767;  // maximum length for environment variable name and value 
        // System environment variables are stored in the registry, and have
        // a size restriction that is separate from both normal environment 
        // variables and registry value name lengths, according to MSDN. 
        // MSDN doesn't detail whether the name is limited to 1024, or whether
        // that includes the contents of the environment variable. 
        const int MaxSystemEnvVariableLength = 1024;
        const int MaxUserEnvVariableLength = 255;

        internal sealed class ResourceHelper 
        {
            internal ResourceHelper(String name) { 
                m_name = name; 
            }
 
#if FEATURE_SPLIT_RESOURCES
            // See code:#splitResourceFeature
            internal ResourceHelper(String name, bool isDebug) : this(name)
            { 
                m_isDebug = isDebug;
                if (!isDebug) { 
                    m_state = (int)ResourceHelperState.HAVE_RESOURCES; 
                }
            } 
#endif // FEATURE_SPLIT_RESOURCES

            private String m_name;
            private ResourceManager SystemResMgr; 

            // To avoid infinite loops when calling GetResourceString.  See comments 
            // in GetResourceString for this field. 
            private Stack currentlyLoading;
 
            // process-wide state (since this is only used in one domain),
            // used to avoid the TypeInitialization infinite recusion
            // in GetResourceStringCode
            internal bool resourceManagerInited = false; 

#if FEATURE_SPLIT_RESOURCES 
 
            private bool m_isDebug;
            private int m_state = (int)ResourceHelperState.UNITIALIZED; 

            internal bool UseFallback() {
                // this always returns false for runtime resources because its state can never be NO_RESOURCES
                return m_state == (int)ResourceHelperState.NO_RESOURCES; 
            }
 
#endif // FEATURE_SPLIT_RESOURCES 

            internal class GetResourceStringUserData 
            {
                public ResourceHelper m_resourceHelper;
                public String m_key;
                public CultureInfo m_culture; 
                public String m_retVal;
                public bool m_lockWasTaken; 
 
                public GetResourceStringUserData(ResourceHelper resourceHelper, String key, CultureInfo culture)
                { 
                    m_resourceHelper = resourceHelper;
                    m_key = key;
                    m_culture = culture;
                } 
            }
 
            [System.Security.SecuritySafeCritical]  // auto-generated 
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
            internal String GetResourceString(String key) { 
                if (key == null || key.Length == 0) {
                    Contract.Assert(false, "Environment::GetResourceString with null or empty key.  Bug in caller, or weird recursive loading problem?");
                    return "[Resource lookup failed - null or empty resource name]";
                } 
                return GetResourceString(key, null);
            } 
 
            [System.Security.SecuritySafeCritical]  // auto-generated
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)] 
            internal String GetResourceString(String key, CultureInfo culture)  {
                if (key == null || key.Length == 0) {
                    BCLDebug.Assert(false, "Environment::GetResourceString with null or empty key.  Bug in caller, or weird recursive loading problem?");
                    return "[Resource lookup failed - null or empty resource name]"; 
                }
 
#if FEATURE_SPLIT_RESOURCES 
                if (UseFallback()) {
                    return null; 
                }
#endif // FEATURE_SPLIT_RESOURCES

                // We have a somewhat common potential for infinite 
                // loops with mscorlib's ResourceManager.  If "potentially dangerous"
                // code throws an exception, we will get into an infinite loop 
                // inside the ResourceManager and this "potentially dangerous" code. 
                // Potentially dangerous code includes the IO package, CultureInfo,
                // parts of the loader, some parts of Reflection, Security (including 
                // custom user-written permissions that may parse an XML file at
                // class load time), assembly load event handlers, etc.  Essentially,
                // this is not a bounded set of code, and we need to fix the problem.
                // Fortunately, this is limited to mscorlib's error lookups and is NOT 
                // a general problem for all user code using the ResourceManager.
 
                // The solution is to make sure only one thread at a time can call 
                // GetResourceString.  Also, since resource lookups can be
                // reentrant, if the same thread comes into GetResourceString 
                // twice looking for the exact same resource name before
                // returning, we're going into an infinite loop and we should
                // return a bogus string.
 
                GetResourceStringUserData userData = new GetResourceStringUserData(this, key, culture);
 
                RuntimeHelpers.TryCode tryCode = new RuntimeHelpers.TryCode(GetResourceStringCode); 
                RuntimeHelpers.CleanupCode cleanupCode = new RuntimeHelpers.CleanupCode(GetResourceStringBackoutCode);
 
                RuntimeHelpers.ExecuteCodeWithGuaranteedCleanup(tryCode, cleanupCode, userData);
                return userData.m_retVal;

            } 

            [System.Security.SecuritySafeCritical]  // auto-generated 
            private void GetResourceStringCode(Object userDataIn) 
            {
                GetResourceStringUserData userData = (GetResourceStringUserData) userDataIn; 
                ResourceHelper rh = userData.m_resourceHelper;
                String key = userData.m_key;
                CultureInfo culture = userData.m_culture;
 
                Monitor.Enter(rh, ref userData.m_lockWasTaken);
 
                // Are we recursively looking up the same resource? 
                if (rh.currentlyLoading != null && rh.currentlyLoading.Count > 0 && rh.currentlyLoading.Contains(key)) {
                    // This is often a bug in the BCL, security, NLS+ code, 
                    // or the loader somewhere.  However, this could also
                    // be a setup problem - check whether mscorlib &
                    // mscorwks are both of the same build flavor.
                    String stackTrace = "[Couldn't get a stack trace]"; 
                    try
                    { 
                        StackTrace st = new StackTrace(true); 
                        // Don't attempt to localize strings in this stack trace, otherwise it could cause
                        // infinite recursion. This stack trace is used for an Assert message only, and 
                        // so the lack of localization should not be an issue.
                        stackTrace = st.ToString( System.Diagnostics.StackTrace.TraceFormat.NoResourceLookup );
                    }
                    catch(StackOverflowException) {} 
                    catch(NullReferenceException) {}
                    catch(OutOfMemoryException) {} 
 
                    Contract.Assert(false, "Infinite recursion during resource lookup.  Resource name: " + key + Environment.NewLine + stackTrace);
 
                    // Note: can't append the key name, since that may require
                    // an extra allocation...
                    userData.m_retVal = "[Resource lookup failed - infinite recursion or critical failure detected.]";
                    return; 
                }
                if (rh.currentlyLoading == null) 
                    rh.currentlyLoading = new Stack(4); 

                // Call class constructors preemptively, so that we cannot get into an infinite 
                // loop constructing a TypeInitializationException.  If this were omitted,
                // we could get the Infinite recursion assert above by failing type initialization
                // between the Push and Pop calls below.
 
                if (!rh.resourceManagerInited)
                { 
                    // process-critical code here.  No ThreadAbortExceptions 
                    // can be thrown here.  Other exceptions percolate as normal.
                    RuntimeHelpers.PrepareConstrainedRegions(); 
                    try {
                    }
                    finally {
                        RuntimeHelpers.RunClassConstructor(typeof(ResourceManager).TypeHandle); 
                        RuntimeHelpers.RunClassConstructor(typeof(ResourceReader).TypeHandle);
                        RuntimeHelpers.RunClassConstructor(typeof(RuntimeResourceSet).TypeHandle); 
                        RuntimeHelpers.RunClassConstructor(typeof(BinaryReader).TypeHandle); 
                        rh.resourceManagerInited = true;
                    } 

                }

                rh.currentlyLoading.Push(key); 

#if FEATURE_SPLIT_RESOURCES 
                if (rh.SystemResMgr == null) { 
                    rh.SystemResMgr = new ResourceManager(m_name, typeof(Object).Assembly, m_isDebug);
                } 
                String s = rh.SystemResMgr.GetString(key, culture);
                rh.currentlyLoading.Pop();

                if (rh.m_isDebug) { 
                    int detectedState = (s == null) ? (int)ResourceHelperState.NO_RESOURCES : (int)ResourceHelperState.HAVE_RESOURCES;
                    // update state only if it's currently in the UNITIALIZED state 
                    int currentState = Interlocked.CompareExchange(ref m_state, detectedState, (int)ResourceHelperState.UNITIALIZED); 
                }
                else { 
                    Contract.Assert(s!=null, "Managed resource string lookup failed.  Was your resource name misspelled?  Did you rebuild mscorlib after adding a resource to resources.txt?  Debug this w/ cordbg and bug whoever owns the code that called Environment.GetResourceString.  Resource name was: \""+key+"\"");
                }

#else 
                if (rh.SystemResMgr == null) {
                    rh.SystemResMgr = new ResourceManager(m_name, typeof(Object).Assembly); 
                } 
                String s = rh.SystemResMgr.GetString(key, null);
                rh.currentlyLoading.Pop(); 

                Contract.Assert(s!=null, "Managed resource string lookup failed.  Was your resource name misspelled?  Did you rebuild mscorlib after adding a resource to resources.txt?  Debug this w/ cordbg and bug whoever owns the code that called Environment.GetResourceString.  Resource name was: \""+key+"\"");

#endif // !FEATURE_SPLIT_RESOURCES 

                userData.m_retVal = s; 
            } 

            [PrePrepareMethod] 
            private void GetResourceStringBackoutCode(Object userDataIn, bool exceptionThrown)
            {
                GetResourceStringUserData userData = (GetResourceStringUserData) userDataIn;
                ResourceHelper rh = userData.m_resourceHelper; 

                if (exceptionThrown) 
                { 
                    if (userData.m_lockWasTaken)
                    { 
                        // Backout code - throw away potentially corrupt state
                        rh.SystemResMgr = null;
                        rh.currentlyLoading = null;
                    } 
                }
                // Release the lock, if we took it. 
                if (userData.m_lockWasTaken) 
                {
                    Monitor.Exit(rh); 
                }
            }

        } 

        // #splitResourceFeature 
        // 
        // Overview:
        // FEATURE_SPLIT_RESOURCES is enabled only in coreclr builds. With this feature, resources are split into 
        // runtime (critical) and debug resources. There are <10 runtime resources, and these are handled with the
        // runtime resource helper. Debug resources will only be present in debug packs so we allow fallback in
        // case these resources aren't present. If they aren't present, we return a general resource string.
        // 
        // Impact to GetResourceString callers:
        // To minimize impact of this feature on the codebase, the typical resource helper is co-opted for debug 
        // resources and a new runtime resource helper is introduced to handle the runtime resources. To save 
        // lookup time, if FEATURE_SPLIT_RESOURCES is enabled, callers looking for runtime resources will call the
        // XRuntime overloads. (There were <10 such callers.) 
        //
        // Some exception classes need to know if fallback was used, so they can do something other than their
        // typical exception message formatting. So Environment now provides some internal overloads to allow
        // these callers to detect this. 
        //
        // Differences in resource lookup when FEATURE_SPLIT_RESOURCES is enabled: 
        // You'll need some historical information first, otherwise the changes probably won't make sense, so... 
        //
        // A bit of history: 
        // Historically (Orcas and before), mscorlib's resources were embedded in mscorlib.dll. Furthermore,
        // its resources were loaded in the default appdomain. This latter characteristic is yet another way in
        // which mscorlib is distinct from other framework assemblies. This is achieved by the fcall
        // GetResourceFromDefault, which transitions into the default appdomain and then calls Environment's 
        // GetResourceStringLocal.
        // 
        // The changes -- deployment: 
        // The <10 runtime resources are still embedded in mscorlib, but all others are pulled out into a
        // satellite assembly. To reserve the right to have all mscorlib's resources in a satellite assembly in 
        // thefuture (and given that these are optional), this optional satellite assembly is called
        // mscorlib.debug.resources.dll. To achieve this, we added some special casing in the VM to accept this
        // new name as an mscorlib satellite assembly. When attempting to load this satellite assembly,
        // localization is performed in the normal way: if mscorlib.dll, etc are stored in <setup_dir>, then you 
        // can place the satellite assembly under <setup_dir>\en-US, or <setup_dir>\<current_culture> in general
        // and we'll grab the assembly for the current culture. The ResourceManager does the usual fallback magic 
        // in case the current culture's resources aren't present. 
        //
        // The changes -- lookup: 
        // Resources are still loaded in the default appdomain so we added extra magic below to do the transitions
        // for runtime and optional resource messages. If the optional resource set isn't found on first lookup,
        // we flag that and return the default fallback message (which is a runtime resource string). We avoid
        // this work subsequently via the new field ResourceHelper.m_useFallbackMessage. 
#if FEATURE_SPLIT_RESOURCES
        private static ResourceHelper m_runtimeResHelper;  // Doesn't need to be initialized as they're zero-init. 
#endif // FEATURE_SPLIT_RESOURCES 
        private static ResourceHelper m_resHelper;  // Doesn't need to be initialized as they're zero-init.
 
        private static bool s_IsWindowsVista;
        private static bool s_CheckedOSType;

        private static bool s_IsW2k3; 
        private static volatile bool s_CheckedOSW2k3;
 
        private const  int    MaxMachineNameLength = 256; 

        // Private object for locking instead of locking on a public type for SQL reliability work. 
        private static Object s_InternalSyncObject;
        private static Object InternalSyncObject {
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
            get { 
                if (s_InternalSyncObject == null) {
                    Object o = new Object(); 
                    Interlocked.CompareExchange<Object>(ref s_InternalSyncObject, o, null); 
                }
                return s_InternalSyncObject; 
            }
        }

 
        private static OperatingSystem m_os;  // Cached OperatingSystem value
        private static OSName m_osname; 
 
        /*==================================TickCount===================================
        **Action: Gets the number of ticks since the system was started. 
        **Returns: The number of ticks since the system was started.
        **Arguments: None
        **Exceptions: None
        ==============================================================================*/ 
        public static extern int TickCount {
        [System.Security.SecuritySafeCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
            get; 
            }

        // Terminates this process with the given exit code.
        [System.Security.SecurityCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.Process)]
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)] 
        [SuppressUnmanagedCodeSecurity] 
        internal static extern void _Exit(int exitCode);
 
        [System.Security.SecuritySafeCritical]  // auto-generated
        [ResourceExposure(ResourceScope.Process)]
        [ResourceConsumption(ResourceScope.Process)]
        [SecurityPermissionAttribute(SecurityAction.Demand, Flags=SecurityPermissionFlag.UnmanagedCode)] 
        public static void Exit(int exitCode) {
            _Exit(exitCode); 
        } 

 
        public static extern int ExitCode {
        [System.Security.SecuritySafeCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
            get;
 
        [System.Security.SecuritySafeCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
            set;
        }

        // This overload of FailFast will allow you to specify the exception object 
        // whose bucket details *could* be used when undergoing the failfast process.
        // To be specific: 
        // 
        // 1) When invoked from within a managed EH clause (fault/finally/catch),
        //    if the exception object is preallocated, the runtime will try to find its buckets 
        //    and use them. If the exception object is not preallocated, it will use the bucket
        //    details contained in the object (if any).
        //
        // 2) When invoked from outside the managed EH clauses (fault/finally/catch), 
        //    if the exception object is preallocated, the runtime will use the callsite's
        //    IP for bucketing. If the exception object is not preallocated, it will use the bucket 
        //    details contained in the object (if any). 
        [System.Security.SecurityCritical]  // auto-generated_required
        [ResourceExposure(ResourceScope.Process)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        public static extern void FailFast(String message);

        [System.Security.SecurityCritical] 
        [ResourceExposure(ResourceScope.Process)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        public static extern void FailFast(String message, Exception exception); 

        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)] 
        [SecurityCritical]  // Our security team doesn't yet allow safe-critical P/Invoke methods.
        [ResourceExposure(ResourceScope.None)]
        [SuppressUnmanagedCodeSecurity]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)] 
        internal static extern void TriggerCodeContractFailure(ContractFailureKind failureKind, String message, String condition, String exceptionAsString);
 
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)] 
        [SecurityCritical]  // Our security team doesn't yet allow safe-critical P/Invoke methods.
        [ResourceExposure(ResourceScope.None)] 
        [SuppressUnmanagedCodeSecurity]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetIsCLRHosted();
 
        internal static bool IsCLRHosted {
            [SecuritySafeCritical] 
            get { return GetIsCLRHosted(); } 
        }
 
        public static String CommandLine {
            [System.Security.SecuritySafeCritical]  // auto-generated
            get {
                new EnvironmentPermission(EnvironmentPermissionAccess.Read, "Path").Demand(); 

                String commandLine = null; 
                GetCommandLine(JitHelpers.GetStringHandleOnStack(ref commandLine)); 
                return commandLine;
            } 
        }

        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)] 
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode), SuppressUnmanagedCodeSecurity]
        private static extern void GetCommandLine(StringHandleOnStack retString); 
 
        /*===============================CurrentDirectory===============================
        **Action:  Provides a getter and setter for the current directory.  The original 
        **         current directory is the one from which the process was started.
        **Returns: The current directory (from the getter).  Void from the setter.
        **Arguments: The current directory to which to switch to the setter.
        **Exceptions: 
        ==============================================================================*/
        public static String CurrentDirectory { 
            [ResourceExposure(ResourceScope.Machine)] 
            [ResourceConsumption(ResourceScope.Machine)]
            get{ 
                return Directory.GetCurrentDirectory();
            }

            [ResourceExposure(ResourceScope.Machine)] 
            [ResourceConsumption(ResourceScope.Machine)]
            set { 
                Directory.SetCurrentDirectory(value); 
            }
        } 

 #if !FEATURE_PAL || !FEATURE_CORECLR
        // Returns the system directory (ie, C:\WinNT\System32).
        public static String SystemDirectory { 
            [System.Security.SecuritySafeCritical]  // auto-generated
            [ResourceExposure(ResourceScope.Machine)] 
            [ResourceConsumption(ResourceScope.Machine)] 
            get {
                StringBuilder sb = new StringBuilder(Path.MAX_PATH); 
                int r = Win32Native.GetSystemDirectory(sb, Path.MAX_PATH);
                Contract.Assert(r < Path.MAX_PATH, "r < Path.MAX_PATH");
                if (r==0) __Error.WinIOError();
                String path = sb.ToString(); 

                // Do security check 
                new FileIOPermission(FileIOPermissionAccess.PathDiscovery, path).Demand(); 

                return path; 
            }
        }
#endif // !FEATURE_PAL || !FEATURE_CORECLR
 
#if !FEATURE_PAL
        // Returns the windows directory (ie, C:\WinNT). 
        // Used by NLS+ custom culures only at the moment. 
        internal static String InternalWindowsDirectory {
            [System.Security.SecurityCritical]  // auto-generated 
            [ResourceExposure(ResourceScope.Machine)]
            [ResourceConsumption(ResourceScope.Machine)]
            get {
                StringBuilder sb = new StringBuilder(Path.MAX_PATH); 
                int r = Win32Native.GetWindowsDirectory(sb, Path.MAX_PATH);
                Contract.Assert(r < Path.MAX_PATH, "r < Path.MAX_PATH"); 
                if (r==0) __Error.WinIOError(); 
                String path = sb.ToString();
 
                return path;
            }
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated
        public static String ExpandEnvironmentVariables(String name) 
        { 
            if (name == null)
                throw new ArgumentNullException("name"); 
            Contract.EndContractBlock();

            if (name.Length == 0) {
                return name; 
            }
 
            bool isFullTrust; 
#if FEATURE_CORECLR
            isFullTrust = false; 
#else
            isFullTrust = CodeAccessSecurityEngine.QuickCheckForAllDemands();
#endif // FEATURE_CORECLR
 
            // Do a security check to guarantee we can read each of the
            // individual environment variables requested here. 
            String[] varArray = name.Split(new char[] {'%'}); 
            StringBuilder vars = isFullTrust ? null : new StringBuilder();
 
            int currentSize = 100;
            StringBuilder blob = new StringBuilder(currentSize); // A somewhat reasonable default size
            int size;
            bool fJustExpanded = false; // to accommodate expansion alg. 

            for(int i=1; i<varArray.Length-1; i++) { // Skip first and last tokens 
                // ExpandEnvironmentStrings' greedy algorithm expands every 
                // non-boundary %-delimited substring, provided the previous
                // has not been expanded. 
                // if "foo" is not expandable, and "PATH" is, then both
                // %foo%PATH% and %foo%foo%PATH% will expand PATH, but
                // %PATH%PATH% will expand only once.
                // Therefore, if we've just expanded, skip this substring. 
                if (varArray[i].Length == 0 || fJustExpanded == true)
                { 
                    fJustExpanded = false; 
                    continue; // Nothing to expand
                } 
                // Guess a somewhat reasonable initial size, call the method, then if
                // it fails (ie, the return value is larger than our buffer size),
                // make a new buffer & try again.
                blob.Length = 0; 
                String envVar = "%" + varArray[i] + "%";
                size = Win32Native.ExpandEnvironmentStrings(envVar, blob, currentSize); 
                if (size == 0) 
                    Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
 
                // some environment variable might be changed while this function is called
                while (size > currentSize) {
                    currentSize = size;
                    blob.Capacity = currentSize; 
                    blob.Length = 0;
                    size = Win32Native.ExpandEnvironmentStrings(envVar, blob, currentSize); 
                    if (size == 0) 
                        Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                } 

                if (!isFullTrust) {
                    String temp = blob.ToString();
                    fJustExpanded = (temp != envVar); 
                    if (fJustExpanded) { // We expanded successfully, we need to do String comparison here
                        // since %FOO% can become %FOOD 
                        vars.Append(varArray[i]); 
                        vars.Append(';');
                    } 
                }
            }

            if (!isFullTrust) 
                new EnvironmentPermission(EnvironmentPermissionAccess.Read, vars.ToString()).Demand();
 
            blob.Length = 0; 
            size = Win32Native.ExpandEnvironmentStrings(name, blob, currentSize);
            if (size == 0) 
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());

            while (size > currentSize) {
                currentSize = size; 
                blob.Capacity = currentSize;
                blob.Length = 0; 
 
                size = Win32Native.ExpandEnvironmentStrings(name, blob, currentSize);
                if (size == 0) 
                    Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
            }

            return blob.ToString(); 
        }
#endif // FEATURE_PAL 
 
        public static String MachineName {
            [System.Security.SecuritySafeCritical]  // auto-generated 
            get {
                // In future release of operating systems, you might be able to rename a machine without
                // rebooting.  Therefore, don't cache this machine name.
                new EnvironmentPermission(EnvironmentPermissionAccess.Read, "COMPUTERNAME").Demand(); 
                StringBuilder buf = new StringBuilder(MaxMachineNameLength);
                int len = MaxMachineNameLength; 
                if (Win32Native.GetComputerName(buf, ref len) == 0) 
                    throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_ComputerName"));
                return buf.ToString(); 
            }
        }

        public static int ProcessorCount { 
            [System.Security.SecuritySafeCritical]  // auto-generated
            get { 
                Win32Native.SYSTEM_INFO info = new Win32Native.SYSTEM_INFO(); 
                Win32Native.GetSystemInfo( ref info );
                return info.dwNumberOfProcessors; 
            }
        }

        public static int SystemPageSize { 
            [System.Security.SecuritySafeCritical]  // auto-generated
            get { 
                (new EnvironmentPermission(PermissionState.Unrestricted)).Demand(); 
                Win32Native.SYSTEM_INFO info = new Win32Native.SYSTEM_INFO();
                Win32Native.GetSystemInfo(ref info); 
                return info.dwPageSize;
            }
        }
 
        /*==============================GetCommandLineArgs==============================
        **Action: Gets the command line and splits it appropriately to deal with whitespace, 
        **        quotes, and escape characters. 
        **Returns: A string array containing your command line arguments.
        **Arguments: None 
        **Exceptions: None.
        ==============================================================================*/
        [System.Security.SecuritySafeCritical]  // auto-generated
        public static String[] GetCommandLineArgs() { 
            new EnvironmentPermission(EnvironmentPermissionAccess.Read, "Path").Demand();
            return GetCommandLineArgsNative(); 
        } 

        [System.Security.SecurityCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        private static extern String[] GetCommandLineArgsNative();
 
        // We need to keep this Fcall since it is used in AppDomain.cs.
        // If we call GetEnvironmentVariable from AppDomain.cs, we will use StringBuilder class. 
        // That has side effect to change the ApartmentState of the calling Thread to MTA. 
        // So runtime can't change the ApartmentState of calling thread any more.
        [System.Security.SecurityCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.Process)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal static extern String nativeGetEnvironmentVariable(String variable);
 
        /*============================GetEnvironmentVariable============================
        **Action: 
        **Returns: 
        **Arguments:
        **Exceptions: 
        ==============================================================================*/
        [System.Security.SecuritySafeCritical]  // auto-generated
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)] 
        public static String GetEnvironmentVariable(String variable)
        { 
            if (variable == null) 
                throw new ArgumentNullException("variable");
            Contract.EndContractBlock(); 
            (new EnvironmentPermission(EnvironmentPermissionAccess.Read, variable)).Demand();

            StringBuilder blob = new StringBuilder(128); // A somewhat reasonable default size
            int requiredSize = Win32Native.GetEnvironmentVariable(variable, blob, blob.Capacity); 

            if( requiredSize == 0) {  //  GetEnvironmentVariable failed 
                if( Marshal.GetLastWin32Error() == Win32Native.ERROR_ENVVAR_NOT_FOUND) 
                    return null;
            } 

            while (requiredSize > blob.Capacity) { // need to retry since the environment variable might be changed
                blob.Capacity = requiredSize;
                blob.Length = 0; 
                requiredSize = Win32Native.GetEnvironmentVariable(variable, blob, blob.Capacity);
            } 
            return blob.ToString(); 
        }
 
#if !FEATURE_PAL
        [System.Security.SecuritySafeCritical]  // auto-generated
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)] 
        public static string GetEnvironmentVariable( string variable, EnvironmentVariableTarget target)
        { 
            if (variable == null) 
            {
                throw new ArgumentNullException("variable"); 
            }
            Contract.EndContractBlock();

            if (target == EnvironmentVariableTarget.Process) 
            {
                return GetEnvironmentVariable(variable); 
            } 

#if FEATURE_WIN32_REGISTRY 
            (new EnvironmentPermission(PermissionState.Unrestricted)).Demand();

            if( target == EnvironmentVariableTarget.Machine) {
                using (RegistryKey environmentKey = 
                       Registry.LocalMachine.OpenSubKey(@"System\CurrentControlSet\Control\Session Manager\Environment", false)) {
 
                   Contract.Assert(environmentKey != null, @"HKLM\System\CurrentControlSet\Control\Session Manager\Environment is missing!"); 
                   if (environmentKey == null) {
                       return null; 
                   }

                   string value = environmentKey.GetValue(variable) as string;
                   return value; 
                }
            } 
            else if( target == EnvironmentVariableTarget.User) { 
                using (RegistryKey environmentKey =
                       Registry.CurrentUser.OpenSubKey("Environment", false)) { 

                   Contract.Assert(environmentKey != null, @"HKCU\Environment is missing!");
                   if (environmentKey == null) {
                       return null; 
                   }
 
                   string value = environmentKey.GetValue(variable) as string; 
                   return value;
                } 
            }
            else
#endif // FEATURE_WIN32_REGISTRY
                { 
                throw new ArgumentException(Environment.GetResourceString("Arg_EnumIllegalVal", (int)target));
            } 
        } 
#endif
 
        /*===========================GetEnvironmentVariables============================
        **Action: Returns an IDictionary containing all enviroment variables and their values.
        **Returns: An IDictionary containing all environment variables and their values.
        **Arguments: None. 
        **Exceptions: None.
        ==============================================================================*/ 
        [System.Security.SecurityCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.Machine)]
        private unsafe static char[] GetEnvironmentCharArray() 
        {
            char[] block = null;

            // Make sure pStrings is not leaked with async exceptions 
            RuntimeHelpers.PrepareConstrainedRegions();
            try { 
            } 
            finally {
                char * pStrings = null; 

                try
                {
                    pStrings = Win32Native.GetEnvironmentStrings(); 
                    if (pStrings == null) {
                        throw new OutOfMemoryException(); 
                    } 

                    // Format for GetEnvironmentStrings is: 
                    // [=HiddenVar=value\0]* [Variable=value\0]* \0
                    // See the description of Environment Blocks in MSDN's
                    // CreateProcess page (null-terminated array of null-terminated strings).
 
                    // Search for terminating \0\0 (two unicode \0's).
                    char * p = pStrings; 
                    while (!(*p == '\0' && *(p + 1) == '\0')) 
                        p++;
 
                    int len = (int)(p - pStrings + 1);
                    block = new char[len];

                    fixed (char* pBlock = block) 
                        Buffer.memcpy(pStrings, 0, pBlock, 0, len);
                } 
                finally 
                {
                    if (pStrings != null) 
                        Win32Native.FreeEnvironmentStrings(pStrings);
                }
            }
 
            return block;
        } 
 
        [System.Security.SecuritySafeCritical]  // auto-generated
        [ResourceExposure(ResourceScope.Machine)] 
        [ResourceConsumption(ResourceScope.Machine)]
        public static IDictionary GetEnvironmentVariables()
        {
            bool isFullTrust; 
#if FEATURE_CORECLR
            isFullTrust = false; 
#else 
            isFullTrust = CodeAccessSecurityEngine.QuickCheckForAllDemands();
#endif // FEATURE_CORECLR 

            char[] block = GetEnvironmentCharArray();

            Hashtable table = new Hashtable(20); 
            StringBuilder vars = isFullTrust ? null : new StringBuilder();
            // Copy strings out, parsing into pairs and inserting into the table. 
            // The first few environment variable entries start with an '='! 
            // The current working directory of every drive (except for those drives
            // you haven't cd'ed into in your DOS window) are stored in the 
            // environment block (as =C:=pwd) and the program's exit code is
            // as well (=ExitCode=00000000)  Skip all that start with =.
            // Read docs about Environment Blocks on MSDN's CreateProcess page.
 
            // Format for GetEnvironmentStrings is:
            // (=HiddenVar=value\0 | Variable=value\0)* \0 
            // See the description of Environment Blocks in MSDN's 
            // CreateProcess page (null-terminated array of null-terminated strings).
            // Note the =HiddenVar's aren't always at the beginning. 

            bool first = true;
            for(int i=0; i<block.Length; i++) {
                int startKey = i; 
                // Skip to key
                // On some old OS, the environment block can be corrupted. 
                // Someline will not have '=', so we need to check for '\0'. 
                while(block[i]!='=' && block[i] != '\0') {
                    i++; 
                }

                if(block[i] == '\0') {
                    continue; 
                }
 
                // Skip over environment variables starting with '=' 
                if (i-startKey==0) {
                    while(block[i]!=0) { 
                        i++;
                    }
                    continue;
                } 
                String key = new String(block, startKey, i-startKey);
                i++;  // skip over '=' 
                int startValue = i; 
                while(block[i]!=0) {
                    // Read to end of this entry 
                    i++;
                }

                String value = new String(block, startValue, i-startValue); 
                // skip over 0 handled by for loop's i++
                table[key]=value; 
 
                if (!isFullTrust) {
                    if( first) { 
                        first = false;
                    }
                    else {
                        vars.Append(';'); 
                    }
                    vars.Append(key); 
                } 
            }
 
            if (!isFullTrust)
                new EnvironmentPermission(EnvironmentPermissionAccess.Read, vars.ToString()).Demand();
            return table;
        } 

#if !FEATURE_PAL 
        internal static IDictionary GetRegistryKeyNameValuePairs(RegistryKey registryKey) { 
            Hashtable table = new Hashtable(20);
 
            if (registryKey != null) {
                string[] names = registryKey.GetValueNames();
                foreach( string name in names) {
                    string value = registryKey.GetValue(name, "").ToString(); 
                    table.Add(name, value);
                } 
            } 
            return table;
        } 

        [System.Security.SecuritySafeCritical]  // auto-generated
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)] 
        public static IDictionary GetEnvironmentVariables( EnvironmentVariableTarget target) {
            if( target == EnvironmentVariableTarget.Process) { 
                return GetEnvironmentVariables(); 
            }
 
#if FEATURE_WIN32_REGISTRY
            (new EnvironmentPermission(PermissionState.Unrestricted)).Demand();

            if( target == EnvironmentVariableTarget.Machine) { 
                using (RegistryKey environmentKey =
                       Registry.LocalMachine.OpenSubKey(@"System\CurrentControlSet\Control\Session Manager\Environment", false)) { 
 
                   return GetRegistryKeyNameValuePairs(environmentKey);
                } 
            }
            else if( target == EnvironmentVariableTarget.User) {
                using (RegistryKey environmentKey =
                       Registry.CurrentUser.OpenSubKey("Environment", false)) { 
                   return GetRegistryKeyNameValuePairs(environmentKey);
                } 
            } 
            else
#endif // FEATURE_WIN32_REGISTRY 
                {
                throw new ArgumentException(Environment.GetResourceString("Arg_EnumIllegalVal", (int)target));
            }
        } 
#endif
 
        [System.Security.SecuritySafeCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)] 
        public static void SetEnvironmentVariable(string variable, string value) {
            CheckEnvironmentVariableName(variable);

            new EnvironmentPermission(PermissionState.Unrestricted).Demand(); 
            // explicitly null out value if is the empty string.
            if (String.IsNullOrEmpty(value) || value[0] == '\0') { 
                value = null; 
            }
            else { 
                if( value.Length >= MaxEnvVariableValueLength) {
                    throw new ArgumentException(Environment.GetResourceString("Argument_LongEnvVarValue"));
                }
            } 

            if(!Win32Native.SetEnvironmentVariable(variable, value)) { 
                int errorCode = Marshal.GetLastWin32Error(); 

                // Allow user to try to clear a environment variable 
                if( errorCode == Win32Native.ERROR_ENVVAR_NOT_FOUND) {
                    return;
                }
 
                // The error message from Win32 is "The filename or extension is too long",
                // which is not accurate. 
                if( errorCode == Win32Native.ERROR_FILENAME_EXCED_RANGE) { 
                    throw new ArgumentException(Environment.GetResourceString("Argument_LongEnvVarValue"));
                } 

                throw new ArgumentException(Win32Native.GetMessage(errorCode));
            }
        } 

        private static void CheckEnvironmentVariableName(string variable) { 
            if (variable == null) { 
                throw new ArgumentNullException("variable");
            } 

            if( variable.Length == 0) {
                throw new ArgumentException(Environment.GetResourceString("Argument_StringZeroLength"), "variable");
            } 

            if( variable[0] == '\0') { 
                throw new ArgumentException(Environment.GetResourceString("Argument_StringFirstCharIsZero"), "variable"); 
            }
 
            // Make sure the environment variable name isn't longer than the
            // max limit on environment variable values.  (MSDN is ambiguous
            // on whether this check is necessary.)
            if( variable.Length >= MaxEnvVariableValueLength ) { 
                throw new ArgumentException(Environment.GetResourceString("Argument_LongEnvVarValue"));
            } 
 
            if( variable.IndexOf('=') != -1) {
                throw new ArgumentException(Environment.GetResourceString("Argument_IllegalEnvVarName")); 
            }
            Contract.EndContractBlock();
        }
 
#if !FEATURE_PAL
        [System.Security.SecuritySafeCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.Machine)] 
        [ResourceConsumption(ResourceScope.Machine)]
        public static void SetEnvironmentVariable(string variable, string value, EnvironmentVariableTarget target) { 
            if( target == EnvironmentVariableTarget.Process) {
                SetEnvironmentVariable(variable, value);
                return;
            } 

            CheckEnvironmentVariableName(variable); 
 
            // System-wide environment variables stored in the registry are
            // limited to 1024 chars for the environment variable name. 
            if (variable.Length >= MaxSystemEnvVariableLength) {
                throw new ArgumentException(Environment.GetResourceString("Argument_LongEnvVarName"));
            }
 
            new EnvironmentPermission(PermissionState.Unrestricted).Demand();
            // explicitly null out value if is the empty string. 
            if (String.IsNullOrEmpty(value) || value[0] == '\0') { 
                value = null;
            } 
#if FEATURE_WIN32_REGISTRY
            if( target == EnvironmentVariableTarget.Machine) {
                using (RegistryKey environmentKey =
                       Registry.LocalMachine.OpenSubKey(@"System\CurrentControlSet\Control\Session Manager\Environment", true)) { 

                   Contract.Assert(environmentKey != null, @"HKLM\System\CurrentControlSet\Control\Session Manager\Environment is missing!"); 
                   if (environmentKey != null) { 
                       if (value == null)
                           environmentKey.DeleteValue(variable, false); 
                       else
                           environmentKey.SetValue(variable, value);
                   }
                } 
            }
            else if( target == EnvironmentVariableTarget.User) { 
                // User-wide environment variables stored in the registry are 
                // limited to 255 chars for the environment variable name.
                if (variable.Length >= MaxUserEnvVariableLength) { 
                    throw new ArgumentException(Environment.GetResourceString("Argument_LongEnvVarValue"));
                }
                using (RegistryKey environmentKey =
                       Registry.CurrentUser.OpenSubKey("Environment", true)) { 
                   Contract.Assert(environmentKey != null, @"HKCU\Environment is missing!");
                   if (environmentKey != null) { 
                      if (value == null) 
                          environmentKey.DeleteValue(variable, false);
                      else 
                          environmentKey.SetValue(variable, value);
                   }
                }
            } 
            else
            { 
                throw new ArgumentException(Environment.GetResourceString("Arg_EnumIllegalVal", (int)target)); 
            }
            // send a WM_SETTINGCHANGE message to all windows 
            IntPtr r = Win32Native.SendMessageTimeout(new IntPtr(Win32Native.HWND_BROADCAST), Win32Native.WM_SETTINGCHANGE, IntPtr.Zero, "Environment", 0, 1000, IntPtr.Zero);

            if (r == IntPtr.Zero) BCLDebug.Assert(false, "SetEnvironmentVariable failed: " + Marshal.GetLastWin32Error());
 
#else // FEATURE_WIN32_REGISTRY
            throw new ArgumentException(Environment.GetResourceString("Arg_EnumIllegalVal", (int)target)); 
#endif 
        }
#endif 


        /*===============================GetLogicalDrives===============================
        **Action: Retrieves the names of the logical drives on this machine in the  form "C:\". 
        **Arguments:   None.
        **Exceptions:  IOException. 
        **Permissions: SystemInfo Permission. 
        ==============================================================================*/
        [System.Security.SecuritySafeCritical]  // auto-generated 
        public static String[] GetLogicalDrives() {
#if !PLATFORM_UNIX
            new EnvironmentPermission(PermissionState.Unrestricted).Demand();
 
            int drives = Win32Native.GetLogicalDrives();
            if (drives==0) 
                __Error.WinIOError(); 
            uint d = (uint)drives;
            int count = 0; 
            while (d != 0) {
                if (((int)d & 1) != 0) count++;
                d >>= 1;
            } 
            String[] result = new String[count];
            char[] root = new char[] {'A', ':', '\\'}; 
            d = (uint)drives; 
            count = 0;
            while (d != 0) { 
                if (((int)d & 1) != 0) {
                    result[count++] = new String(root);
                }
                d >>= 1; 
                root[0]++;
            } 
            return result; 
#else
            return new String[0]; 
#endif // !PLATFORM_UNIX
        }

        /*===================================NewLine==================================== 
        **Action: A property which returns the appropriate newline string for the given
        **        platform. 
        **Returns: \r\n on Win32. 
        **Arguments: None.
        **Exceptions: None. 
        ==============================================================================*/
        public static String NewLine {
            get {
                Contract.Ensures(Contract.Result<String>() != null); 
#if !PLATFORM_UNIX
                return "\r\n"; 
#else 
                return "\n";
#endif // !PLATFORM_UNIX 
            }
        }

 
        /*===================================Version====================================
        **Action: Returns the COM+ version struct, describing the build number. 
        **Returns: 
        **Arguments:
        **Exceptions: 
        ==============================================================================*/
        public static Version Version {
            get {
                return new Version(ThisAssembly.InformationalVersion); 
            }
        } 
 

        /*==================================WorkingSet================================== 
        **Action:
        **Returns:
        **Arguments:
        **Exceptions: 
        ==============================================================================*/
        [System.Security.SecurityCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.None)] 
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode), SuppressUnmanagedCodeSecurity]
        private static extern long GetWorkingSet(); 

        public static long WorkingSet {
            [System.Security.SecuritySafeCritical]  // auto-generated
            get { 
                new EnvironmentPermission(PermissionState.Unrestricted).Demand();
                return GetWorkingSet(); 
            } 
        }
 

        /*==================================OSVersion===================================
        **Action:
        **Returns: 
        **Arguments:
        **Exceptions: 
        ==============================================================================*/ 
        public static OperatingSystem OSVersion {
            [System.Security.SecuritySafeCritical]  // auto-generated 
            get {
                if (m_os==null) { // We avoid the lock since we don't care if two threads will set this at the same time.
                    Microsoft.Win32.Win32Native.OSVERSIONINFO osvi = new Microsoft.Win32.Win32Native.OSVERSIONINFO();
                    if (!GetVersion(osvi)) { 
                        throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_GetVersion"));
                    } 
 
                    PlatformID id;
                    Boolean getServicePackInfo; 
                    switch (osvi.PlatformId) {
                    case Win32Native.VER_PLATFORM_WIN32_NT:
                        id = PlatformID.Win32NT;
                        getServicePackInfo = true; 
                        break;
 
                    case Win32Native.VER_PLATFORM_UNIX: 
                        id = PlatformID.Unix;
                        getServicePackInfo = false; 
                        break;

                    case Win32Native.VER_PLATFORM_MACOSX:
                        id = PlatformID.MacOSX; 
                        getServicePackInfo = false;
                        break; 
 
                    default:
                        Contract.Assert(false, "Unsupported platform!"); 
                        throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_InvalidPlatformID"));
                    }

                    // for OS other than unix or mac, we need to get Service pack information 
                    Microsoft.Win32.Win32Native.OSVERSIONINFOEX osviEx = new Microsoft.Win32.Win32Native.OSVERSIONINFOEX();
                    if (getServicePackInfo && !GetVersionEx(osviEx)) 
                        throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_GetVersion")); 

                    Version v =  new Version(osvi.MajorVersion, osvi.MinorVersion, osvi.BuildNumber, (osviEx.ServicePackMajor << 16) |osviEx.ServicePackMinor); 
                    m_os = new OperatingSystem(id, v, osvi.CSDVersion);
                }
                Contract.Assert(m_os != null, "m_os != null");
                return m_os; 
            }
        } 
 
        internal static bool IsWindowsVista {
            get { 
                if (!s_CheckedOSType) {
                    OperatingSystem OS = Environment.OSVersion;
                    s_IsWindowsVista = OS.Platform == PlatformID.Win32NT && OS.Version.Major >= 6;
                    s_CheckedOSType = true; 
                }
                return s_IsWindowsVista; 
            } 
        }
 
        internal static bool IsW2k3 {
            get {
                if (!s_CheckedOSW2k3) {
                    OperatingSystem OS = Environment.OSVersion; 
                    s_IsW2k3 = ( (OS.Platform == PlatformID.Win32NT) && (OS.Version.Major == 5) && (OS.Version.Minor == 2));
                    s_CheckedOSW2k3 = true; 
                } 
                return s_IsW2k3;
            } 
        }


        internal static bool RunningOnWinNT { 
            get {
                return OSVersion.Platform == PlatformID.Win32NT; 
            } 
        }
 
        [Serializable]
        internal enum OSName
        {
            Invalid = 0, 
            Unknown = 1,
            WinNT = 0x80, 
            Nt4   = 1 | WinNT, 
            Win2k   = 2 | WinNT,
            MacOSX = 0x100, 
            Tiger = 1 | MacOSX,
            Leopard = 2 | MacOSX
        }
 
        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        internal static extern bool GetVersion(Microsoft.Win32.Win32Native.OSVERSIONINFO  osVer);
 
        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal static extern bool GetVersionEx(Microsoft.Win32.Win32Native.OSVERSIONINFOEX  osVer); 

 
        internal static OSName OSInfo 
        {
            [System.Security.SecuritySafeCritical]  // auto-generated 
            get
            {
                if (m_osname == OSName.Invalid)
                { 
                    lock(InternalSyncObject)
                    { 
                        if (m_osname == OSName.Invalid) 
                        {
                            Microsoft.Win32.Win32Native.OSVERSIONINFO osvi = new Microsoft.Win32.Win32Native.OSVERSIONINFO(); 
                            bool r = GetVersion(osvi);
                            if (!r)
                            {
                                Contract.Assert(r, "OSVersion native call failed."); 
                                throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_GetVersion"));
                            } 
                            switch (osvi.PlatformId) 
                                {
                                case Win32Native.VER_PLATFORM_WIN32_NT: 
                                    switch(osvi.MajorVersion)
                                    {
                                        case 5:
                                            m_osname = OSName.Win2k; 
                                            break;
                                        case 4: 
                                            Contract.Assert(false, "NT4 is no longer a supported platform!"); 
                                            m_osname = OSName.Unknown; // Unknown OS
                                            break; 
                                        default:
                                            m_osname = OSName.WinNT;
                                            break;
                                    } 
                                    break;
 
                                case Win32Native.VER_PLATFORM_WIN32_WINDOWS: 
                                    Contract.Assert(false, "Win9x is no longer a supported platform!");
                                    m_osname = OSName.Unknown; // Unknown OS 
                                   break;

                                case Win32Native.VER_PLATFORM_MACOSX:
                                    if (osvi.MajorVersion == 10) 
                                    {
                                        switch (osvi.MinorVersion) 
                                        { 
                                            case 5:
                                                m_osname = OSName.Leopard; 
                                                break;
                                            case 4:
                                                m_osname = OSName.Tiger;
                                                break; 
                                            default:
                                                m_osname = OSName.MacOSX; 
                                                break; 
                                        }
                                    } 
                                    else
                                        m_osname = OSName.MacOSX; // Well, at least Macintosh.
                                    break;
 
                                default:
                                    m_osname = OSName.Unknown; // Unknown OS 
                                    break; 

                            } 
                        }
                    }
                }
                return m_osname; 
            }
        } 
 
        /*==================================StackTrace==================================
        **Action: 
        **Returns:
        **Arguments:
        **Exceptions:
        ==============================================================================*/ 
        public static String StackTrace {
            [System.Security.SecuritySafeCritical]  // auto-generated 
            get { 
                new EnvironmentPermission(PermissionState.Unrestricted).Demand();
                return GetStackTrace(null, true); 
            }
        }

        internal static String GetStackTrace(Exception e, bool needFileInfo) 
        {
            // Note: Setting needFileInfo to true will start up COM and set our 
            // apartment state.  Try to not call this when passing "true" 
            // before the EE's ExecuteMainMethod has had a chance to set up the
            // apartment state.  -- 
            StackTrace st;
            if (e == null)
                st = new StackTrace(needFileInfo);
            else 
                st = new StackTrace(e, needFileInfo);
 
            // Do no include a trailing newline for backwards compatibility 
            return st.ToString( System.Diagnostics.StackTrace.TraceFormat.Normal );
        } 

        [System.Security.SecuritySafeCritical]  // auto-generated
        private static void InitResourceHelper() {
            // Only the default AppDomain should have a ResourceHelper.  All calls to 
            // GetResourceString from any AppDomain delegate to GetResourceStringLocal
            // in the default AppDomain via the fcall GetResourceFromDefault. 
 
            bool tookLock = false;
            RuntimeHelpers.PrepareConstrainedRegions(); 
            try {

                Monitor.Enter(Environment.InternalSyncObject, ref tookLock);
 
                if (m_resHelper == null) {
#if FEATURE_SPLIT_RESOURCES 
                    // See code:#splitResourceFeature 
                    ResourceHelper rh = new ResourceHelper("mscorlib.debug", true);
#else 
                    ResourceHelper rh = new ResourceHelper("mscorlib");
#endif // FEATURE_SPLIT_RESOURCES

                    System.Threading.Thread.MemoryBarrier(); 
                    m_resHelper =rh;
                } 
            } 
            finally {
                if (tookLock) 
                    Monitor.Exit(Environment.InternalSyncObject);
            }
        }
 
        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        internal extern static String GetResourceFromDefault(String key);
 
        // Looks up the resource string value for key.
        //
        // if you change this method's signature then you must change the code that calls it
        // in excep.cpp and probably you will have to visit mscorlib.h to add the new signature 
        // as well as metasig.h to create the new signature type
        internal static String GetResourceStringLocal(String key) { 
            if (m_resHelper == null) 
                InitResourceHelper();
 
#if FEATURE_SPLIT_RESOURCES
            // See code:#splitResourceFeature
            // Go ahead and make sure the runtime resource helper is initialized. We'll most likely
            // need it, for either the fallback resource or words like "at", which are needed in 
            // stack traces
            if (m_runtimeResHelper == null) 
                InitRuntimeResourceHelper(); 
#endif // FEATURE_SPLIT_RESOURCES
 
            String s = m_resHelper.GetResourceString(key);
#if FEATURE_SPLIT_RESOURCES
            // See code:#splitResourceFeature
            if (m_resHelper.UseFallback()) { 
                s = m_runtimeResHelper.GetResourceString("NoDebugResources");
                // note: only source of these calls are from vm; mscorlib calls use the usedFallback 
                // overload if FEATURE_SPLIT_RESOURCES is enabled. Let's go ahead and format for VM. 
                // Also note that no VM callers call any of the params overloads.
                s = FormatFallbackMessage(s, key, null); 
            }
#endif // FEATURE_SPLIT_RESOURCES
            return s;
 
        }
 
        // #threadCultureInfo 
        // Currently in silverlight, CurrentCulture and CurrentUICulture are isolated
        // within an AppDomain. This is in contrast to the desktop, in which cultures 
        // leak across AppDomain boundaries with the thread.
        //
        // Note that mscorlib transitions to the default domain to perform resource
        // lookup. This causes problems for the silverlight changes: since culture isn't 
        // passed, resource string lookup won't necessarily use the culture of the thread
        // originating the request. To get around that problem, we pass the CultureInfo 
        // so that the ResourceManager GetString(x, cultureInfo) overload can be used. 
        // We first perform the same check as in CultureInfo to make sure it's safe to
        // let the CultureInfo travel across AppDomains. 
        [System.Security.SecuritySafeCritical]  // auto-generated
        [Pure]
        [ResourceExposure(ResourceScope.None)]
        internal static String GetResourceString(String key) { 
#if FEATURE_SPLIT_RESOURCES
            bool usedFallback = false; 
            CultureInfo lookupCulture = GetResourceLookupCulture(); 
            String s = GetResourceFromDefaultUsedFallback(key, lookupCulture, ref usedFallback);
            if (usedFallback) { 
                s = FormatFallbackMessage(s, key, null);
            }
            return s;
#else 
            return GetResourceFromDefault(key);
#endif 
        } 

        [System.Security.SecuritySafeCritical]  // auto-generated 
        [Pure]
        [ResourceExposure(ResourceScope.None)]
        internal static String GetResourceString(String key, params Object[] values) {
#if FEATURE_SPLIT_RESOURCES 
            bool usedFallback = false;
            CultureInfo lookupCulture = GetResourceLookupCulture(); 
            String s = GetResourceFromDefaultUsedFallback(key, lookupCulture, ref usedFallback); 
            if (usedFallback) {
                s = FormatFallbackMessage(s, key, values); 
                return s;
            }
#else
            String s = GetResourceFromDefault(key); 
#endif
            return String.Format(CultureInfo.CurrentCulture, s, values); 
        } 

        [System.Security.SecuritySafeCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.None)]
        internal static String GetRuntimeResourceString(String key) {
#if FEATURE_SPLIT_RESOURCES
            CultureInfo lookupCulture = GetResourceLookupCulture(); 
            return GetRuntimeResourceFromDefault(key,lookupCulture);
#else 
            return GetResourceFromDefault(key); 
#endif // FEATURE_SPLIT_RESOURCES
        } 

        [System.Security.SecuritySafeCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)]
        internal static String GetRuntimeResourceString(String key, params Object[] values) { 
#if FEATURE_SPLIT_RESOURCES
            CultureInfo lookupCulture = GetResourceLookupCulture(); 
            String s = GetRuntimeResourceFromDefault(key, lookupCulture); 
#else
            String s = GetResourceFromDefault(key); 
#endif // FEATURE_SPLIT_RESOURCES
            return String.Format(CultureInfo.CurrentCulture, s, values);
        }
 
#if FEATURE_SPLIT_RESOURCES
        // See code:#splitResourceFeature 
        private static string FormatFallbackMessage(String fallbackMessage, String key, params Object[] values) { 
            if (fallbackMessage == null) {
                // couldn't even find fallbackMessage. As a last-ditch effort, just return the key 
                return key;
            }

            // build up arg string 
            String argStr = null;
            if (values != null) { 
                StringBuilder sb = new StringBuilder(); 
                for (int i = 0; i < values.Length; i ++) {
                    if (values[i] != null) { 
                        String value = values[i].ToString();
                        if (value != null) {
                            sb.Append(value);
                            if (i < values.Length - 1) { 
                                sb.Append(CultureInfo.CurrentCulture.TextInfo.ListSeparator);
                            } 
                        } 
                    }
                } 
                argStr = sb.ToString();
            }
            if (argStr == null) argStr = "";
 
            // don't uri-encode key; instead burden is on mscorlib resource keys to not conflict
            return String.Format(CultureInfo.CurrentCulture, fallbackMessage, key, argStr, GetAssemblyFileVersion(), "mscorlib.dll", key); 
        } 

        private static string GetAssemblyFileVersion() { 
            Object[] attributes = typeof(Object).Assembly.GetCustomAttributes(typeof(AssemblyFileVersionAttribute), false);
            if (attributes.Length != 1) {
                return "";
            } 

            AssemblyFileVersionAttribute fileVersionAttribute = attributes[0] as AssemblyFileVersionAttribute; 
            if (fileVersionAttribute == null) { 
                return "";
            } 

            return fileVersionAttribute.Version;
        }
 
        [Pure]
        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        internal extern static String GetRuntimeResourceFromDefault(String key, CultureInfo culture);
 
        [Pure]
        internal static String GetRuntimeResourceStringLocal(String key, CultureInfo culture) {
            if (m_runtimeResHelper == null)
                InitRuntimeResourceHelper(); 

            // runtime resources always have to be present (they're embedded in mscorlib) 
            // so we don't have to fall back. 
            return m_runtimeResHelper.GetResourceString(key, culture);
        } 

        private static void InitRuntimeResourceHelper() {
            // Only the default AppDomain should have a ResourceHelper.  All calls to
            // GetResourceString from any AppDomain delegate to GetResourceStringLocal 
            // in the default AppDomain via the fcall GetResourceFromDefault.
 
            bool tookLock = false; 
            RuntimeHelpers.PrepareConstrainedRegions();
            try { 

                Monitor.Enter(Environment.InternalSyncObject, ref tookLock);

                if (m_runtimeResHelper == null) { 
                    ResourceHelper rh = new ResourceHelper("mscorlib", false);
                    System.Threading.Thread.MemoryBarrier(); 
                    m_runtimeResHelper = rh; 
                }
            } 
            finally {
                if (tookLock)
                    Monitor.Exit(Environment.InternalSyncObject);
            } 
        }
 
        // The following methods specify whether the fallback resource message was used, which 
        // would happen if the debug satellite assembly isn't present. The only callers that
        // need to know this are some Exception classes, which don't want to do their usual 
        // formatting with the fallback resource meessage.

        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        internal extern static String GetResourceFromDefaultUsedFallback(String key, CultureInfo culture, ref bool usedFallback);
 
        internal static String GetResourceStringLocalUsedFallback(String key, CultureInfo culture, ref bool usedFallback) { 
            if (m_resHelper == null)
                InitResourceHelper(); 

            // See code:#splitResourceFeature
            // Go ahead and make sure the runtime resource helper is initialized. We'll most likely
            // need it, for either the fallback resource or words like "at", which are needed in 
            // stack traces
            if (m_runtimeResHelper == null) 
                InitRuntimeResourceHelper(); 

            String s = m_resHelper.GetResourceString(key, culture); 
            usedFallback = m_resHelper.UseFallback();
            if (usedFallback) {
                s = m_runtimeResHelper.GetResourceString("NoDebugResources", culture);
            } 
            return s;
        } 
 
        private static CultureInfo GetResourceLookupCulture() {
            CultureInfo currentUICulture = CultureInfo.CurrentUICulture; 
            if (currentUICulture.CanSendCrossDomain())
            {
                return currentUICulture;
            } 
            return null;
        } 
 
#endif // FEATURE_SPLIT_RESOURCES
 
        public static bool Is64BitProcess {
            get {
                #if WIN32
                    return false; 
                #else
                    return true; 
                #endif 
            }
        } 

        public static bool Is64BitOperatingSystem {
            [System.Security.SecuritySafeCritical]
            get { 
                #if WIN32
                    bool isWow64; // WinXP SP2+ and Win2k3 SP1+ 
                    return Win32Native.DoesWin32MethodExist(Win32Native.KERNEL32, "IsWow64Process") 
                        && Win32Native.IsWow64Process(Win32Native.GetCurrentProcess(), out isWow64)
                        && isWow64; 
                #else
                    // 64-bit programs run only on 64-bit
                    //<STRIP>This will have to change for Mac if we add this API to Silverlight</STRIP>
                    return true; 
                #endif
            } 
        } 

        public static extern bool HasShutdownStarted { 
        [System.Security.SecuritySafeCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
            get; 
        }
 
        // This is the temporary Whidbey stub for compatibility flags 
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        [System.Security.SecurityCritical]
        internal static extern bool GetCompatibilityFlag(CompatibilityFlag flag);

        public static string UserName { 
            [System.Security.SecuritySafeCritical]  // auto-generated
            get { 
                new EnvironmentPermission(EnvironmentPermissionAccess.Read,"UserName").Demand(); 

                StringBuilder sb = new StringBuilder(256); 
                int size = sb.Capacity;
                Win32Native.GetUserName(sb, ref size);
                return sb.ToString();
            } 
        }
 
#if !FEATURE_PAL 
        // Note that this is a handle to a process window station, but it does
        // not need to be closed.  CloseWindowStation would ignore this handle. 
        // We also do handle equality checking as well.  This isn't a great fit
        // for SafeHandle.  We don't gain anything by using SafeHandle here.
        private static IntPtr processWinStation;        // Doesn't need to be initialized as they're zero-init.
        private static bool isUserNonInteractive; 

        public static bool UserInteractive { 
            [System.Security.SecuritySafeCritical]  // auto-generated 
            [ResourceExposure(ResourceScope.None)]
            [ResourceConsumption(ResourceScope.Machine, ResourceScope.Machine)] 
            get {
                if ((OSInfo & OSName.WinNT) == OSName.WinNT) { // On WinNT
                    IntPtr hwinsta = Win32Native.GetProcessWindowStation();
                    if (hwinsta != IntPtr.Zero && processWinStation != hwinsta) { 
                        int lengthNeeded = 0;
                        Win32Native.USEROBJECTFLAGS flags = new Win32Native.USEROBJECTFLAGS(); 
                        if (Win32Native.GetUserObjectInformation(hwinsta, Win32Native.UOI_FLAGS, flags, Marshal.SizeOf(flags),ref lengthNeeded)) { 
                            if ((flags.dwFlags & Win32Native.WSF_VISIBLE) == 0) {
                                isUserNonInteractive = true; 
                            }
                        }
                        processWinStation = hwinsta;
                    } 
                }
                // The logic is reversed to avoid static initialization to true 
                return !isUserNonInteractive; 
            }
        } 
#endif // !FEATURE_PAL

        [System.Security.SecuritySafeCritical]  // auto-generated
        [ResourceExposure(ResourceScope.Machine)] 
        [ResourceConsumption(ResourceScope.Machine)]
        public static string GetFolderPath(SpecialFolder folder) { 
            return GetFolderPath(folder, SpecialFolderOption.None); 
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        public static string GetFolderPath(SpecialFolder folder, SpecialFolderOption option) { 
            if (!Enum.IsDefined(typeof(SpecialFolder),folder))
                throw new ArgumentException(Environment.GetResourceString("Arg_EnumIllegalVal", (int)folder)); 
            if (!Enum.IsDefined(typeof(SpecialFolderOption),option)) 
                throw new ArgumentException(Environment.GetResourceString("Arg_EnumIllegalVal", (int)option));
            Contract.EndContractBlock(); 

            if (option == SpecialFolderOption.Create) {
                FileIOPermission createPermission = new FileIOPermission(PermissionState.None);
                createPermission.AllFiles = FileIOPermissionAccess.Write; 
                createPermission.Demand();
            } 
 
            StringBuilder sb = new StringBuilder(Path.MAX_PATH);
            int hresult = Win32Native.SHGetFolderPath(IntPtr.Zero,                    /* hwndOwner: [in] Reserved */ 
                                                      ((int)folder | (int)option),    /* nFolder:   [in] CSIDL    */
                                                      IntPtr.Zero,                    /* hToken:    [in] access token */
                                                      Win32Native.SHGFP_TYPE_CURRENT, /* dwFlags:   [in] retrieve current path */
                                                      sb);                            /* pszPath:   [out]resultant path */ 
            if (hresult < 0)
            { 
                switch (hresult) 
                {
                default: 
                    // The previous incarnation threw away all errors. In order to limit
                    // breaking changes, we will be permissive about these errors
                    // instead of calling ThowExceptionForHR.
                    //Runtime.InteropServices.Marshal.ThrowExceptionForHR(hresult); 
                    break;
                case __HResults.COR_E_PLATFORMNOTSUPPORTED: 
                    // This one error is the one we do want to throw. 
                    // <STRIP>
 
                    throw new PlatformNotSupportedException();
                }
            }
            String s =  sb.ToString(); 
            new FileIOPermission( FileIOPermissionAccess.PathDiscovery, s ).Demand();
            return s; 
        } 

#if !FEATURE_PAL 
        public static string UserDomainName
        {
                [System.Security.SecuritySafeCritical]  // auto-generated
                get { 
                    new EnvironmentPermission(EnvironmentPermissionAccess.Read,"UserDomain").Demand();
 
                    byte[] sid = new byte[1024]; 
                    int sidLen = sid.Length;
                    StringBuilder domainName = new StringBuilder(1024); 
                    int domainNameLen = domainName.Capacity;
                    int peUse;

                    byte ret = Win32Native.GetUserNameEx(Win32Native.NameSamCompatible, domainName, ref domainNameLen); 
                        if (ret == 1) {
                            string samName = domainName.ToString(); 
                            int index = samName.IndexOf('\\'); 
                            if( index != -1) {
                                return samName.Substring(0, index); 
                            }
                        }
                        domainNameLen = domainName.Capacity;
 
                    bool success = Win32Native.LookupAccountName(null, UserName, sid, ref sidLen, domainName, ref domainNameLen, out peUse);
                    if (!success)  { 
                        int errorCode = Marshal.GetLastWin32Error(); 
                        throw new InvalidOperationException(Win32Native.GetMessage(errorCode));
                    } 

                    return domainName.ToString();
                }
            } 
#endif // !FEATURE_PAL
        public enum SpecialFolderOption { 
            None        = 0, 
            Create      = Win32Native.CSIDL_FLAG_CREATE,
            DoNotVerify = Win32Native.CSIDL_FLAG_DONT_VERIFY, 
        }

//////!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!////////
//////!!!!!! Keep the following locations synchronized            !!!!!!//////// 
//////!!!!!! 1) ndp\clr\src\BCL\Microsoft\Win32\Win32Native.cs    !!!!!!////////
//////!!!!!! 2) ndp\clr\src\BCL\System\Environment.cs             !!!!!!//////// 
//////!!!!!! 3) rotor\pal\inc\rotor_pal.h                         !!!!!!//////// 
//////!!!!!! 4) rotor\pal\corunix\shfolder\shfolder.cpp           !!!!!!////////
//////!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!//////// 
        [ComVisible(true)]
        public enum SpecialFolder {
            //
            //      Represents the file system directory that serves as a common repository for 
            //       application-specific data for the current, roaming user.
            //     A roaming user works on more than one computer on a network. A roaming user's 
            //       profile is kept on a server on the network and is loaded onto a system when the 
            //       user logs on.
            // 
            ApplicationData =  Win32Native.CSIDL_APPDATA,
            //
            //      Represents the file system directory that serves as a common repository for application-specific data that
            //       is used by all users. 
            //
            CommonApplicationData =  Win32Native.CSIDL_COMMON_APPDATA, 
            // 
            //     Represents the file system directory that serves as a common repository for application specific data that
            //       is used by the current, non-roaming user. 
            //
            LocalApplicationData =  Win32Native.CSIDL_LOCAL_APPDATA,
            //
            //     Represents the file system directory that serves as a common repository for Internet 
            //       cookies.
            // 
            Cookies =  Win32Native.CSIDL_COOKIES, 
            Desktop = Win32Native.CSIDL_DESKTOP,
            // 
            //     Represents the file system directory that serves as a common repository for the user's
            //       favorite items.
            //
            Favorites =  Win32Native.CSIDL_FAVORITES, 
#if FEATURE_MULTIPLATFORM || !FEATURE_PAL
            // 
            //     Represents the file system directory that serves as a common repository for Internet 
            //       history items.
            // 
#if FEATURE_CORECLR
            [SupportedPlatforms(~(Platforms.MacOSX | Platforms.Unix))]
#endif // FEATURE_CORECLR
            History =  Win32Native.CSIDL_HISTORY, 
#endif // FEATURE_MULTIPLATFORM || !FEATURE_PAL
            // 
            //     Represents the file system directory that serves as a common repository for temporary 
            //       Internet files.
            // 
            InternetCache =  Win32Native.CSIDL_INTERNET_CACHE,
            //
            //      Represents the file system directory that contains
            //       the user's program groups. 
            //
            Programs =  Win32Native.CSIDL_PROGRAMS, 
            MyComputer =  Win32Native.CSIDL_DRIVES, 
            MyMusic =  Win32Native.CSIDL_MYMUSIC,
            MyPictures = Win32Native.CSIDL_MYPICTURES, 
#if FEATURE_MULTIPLATFORM || !FEATURE_PAL
            //
            //     Represents the file system directory that contains the user's most recently used
            //       documents. 
            //
#if FEATURE_CORECLR 
            [SupportedPlatforms(~(Platforms.MacOSX | Platforms.Unix))] 
#endif // FEATURE_CORECLR
            Recent =  Win32Native.CSIDL_RECENT, 
            //
            //     Represents the file system directory that contains Send To menu items.
            //
#if FEATURE_CORECLR 
            [SupportedPlatforms(~(Platforms.MacOSX | Platforms.Unix))]
#endif // FEATURE_CORECLR 
            SendTo =  Win32Native.CSIDL_SENDTO, 
            //
            //     Represents the file system directory that contains the Start menu items. 
            //
#if FEATURE_CORECLR
            [SupportedPlatforms(~(Platforms.MacOSX | Platforms.Unix))]
#endif // FEATURE_CORECLR 
            StartMenu =  Win32Native.CSIDL_STARTMENU,
            // 
            //     Represents the file system directory that corresponds to the user's Startup program group. The system 
            //       starts these programs whenever any user logs on to Windows NT, or
            //       starts Windows 95 or Windows 98. 
            //
#if FEATURE_CORECLR
            [SupportedPlatforms(~(Platforms.MacOSX | Platforms.Unix))]
#endif // FEATURE_CORECLR 
            Startup =  Win32Native.CSIDL_STARTUP,
            // 
            //     System directory. 
            //
#if FEATURE_CORECLR 
            [SupportedPlatforms(~(Platforms.MacOSX | Platforms.Unix))]
#endif // FEATURE_CORECLR
            System =  Win32Native.CSIDL_SYSTEM,
            // 
            //     Represents the file system directory that serves as a common repository for document
            //       templates. 
            // 
#if FEATURE_CORECLR
            [SupportedPlatforms(~(Platforms.MacOSX | Platforms.Unix))] 
#endif // FEATURE_CORECLR
            Templates =  Win32Native.CSIDL_TEMPLATES,
            //
            //     Represents the file system directory used to physically store file objects on the desktop. 
            //       This should not be confused with the desktop folder itself, which is
            //       a virtual folder. 
            // 
#endif // FEATURE_MULTIPLATFORM || !FEATURE_PAL
            DesktopDirectory =  Win32Native.CSIDL_DESKTOPDIRECTORY, 
            //
            //     Represents the file system directory that serves as a common repository for documents.
            //
            Personal =  Win32Native.CSIDL_PERSONAL, 
            //
            // "MyDocuments" is a better name than "Personal" 
            // 
            MyDocuments = Win32Native.CSIDL_PERSONAL,
            // 
            //     Represents the program files folder.
            //
            ProgramFiles =  Win32Native.CSIDL_PROGRAM_FILES,
#if FEATURE_MULTIPLATFORM || !FEATURE_PAL 
            //
            //     Represents the folder for components that are shared across applications. 
            // 
#if FEATURE_CORECLR
            [SupportedPlatforms(~(Platforms.MacOSX | Platforms.Unix))] 
#endif // FEATURE_CORECLR
            CommonProgramFiles =  Win32Native.CSIDL_PROGRAM_FILES_COMMON,
#endif // FEATURE_MULTIPLATFORM || !FEATURE_PAL
#if !FEATURE_CORECLR 
            //
            //      <user name>\Start Menu\Programs\Administrative Tools 
            // 
            AdminTools             = Win32Native.CSIDL_ADMINTOOLS,
            // 
            //      USERPROFILE\Local Settings\Application Data\Microsoft\CD Burning
            //
            CDBurning              = Win32Native.CSIDL_CDBURN_AREA,
            // 
            //      All Users\Start Menu\Programs\Administrative Tools
            // 
            CommonAdminTools       = Win32Native.CSIDL_COMMON_ADMINTOOLS, 
            //
            //      All Users\Documents 
            //
            CommonDocuments        = Win32Native.CSIDL_COMMON_DOCUMENTS,
            //
            //      All Users\My Music 
            //
            CommonMusic            = Win32Native.CSIDL_COMMON_MUSIC, 
            // 
            //      Links to All Users OEM specific apps
            // 
            CommonOemLinks         = Win32Native.CSIDL_COMMON_OEM_LINKS,
            //
            //      All Users\My Pictures
            // 
            CommonPictures         = Win32Native.CSIDL_COMMON_PICTURES,
            // 
            //      All Users\Start Menu 
            //
            CommonStartMenu        = Win32Native.CSIDL_COMMON_STARTMENU, 
            //
            //      All Users\Start Menu\Programs
            //
            CommonPrograms         = Win32Native.CSIDL_COMMON_PROGRAMS, 
            //
            //     All Users\Startup 
            // 
            CommonStartup          = Win32Native.CSIDL_COMMON_STARTUP,
            // 
            //      All Users\Desktop
            //
            CommonDesktopDirectory = Win32Native.CSIDL_COMMON_DESKTOPDIRECTORY,
            // 
            //      All Users\Templates
            // 
            CommonTemplates        = Win32Native.CSIDL_COMMON_TEMPLATES, 
            //
            //      All Users\My Video 
            //
            CommonVideos           = Win32Native.CSIDL_COMMON_VIDEO,
            //
            //      windows\fonts 
            //
            Fonts                  = Win32Native.CSIDL_FONTS, 
            // 
            //      "My Videos" folder
            // 
            MyVideos               = Win32Native.CSIDL_MYVIDEO,
            //
            //      %APPDATA%\Microsoft\Windows\Network Shortcuts
            // 
            NetworkShortcuts       = Win32Native.CSIDL_NETHOOD,
            // 
            //      %APPDATA%\Microsoft\Windows\Printer Shortcuts 
            //
            PrinterShortcuts       = Win32Native.CSIDL_PRINTHOOD, 
            //
            //      USERPROFILE
            //
            UserProfile            = Win32Native.CSIDL_PROFILE, 
            //
            //      x86 Program Files\Common on RISC 
            // 
            CommonProgramFilesX86  = Win32Native.CSIDL_PROGRAM_FILES_COMMONX86,
            // 
            //      x86 C:\Program Files on RISC
            //
            ProgramFilesX86        = Win32Native.CSIDL_PROGRAM_FILESX86,
            // 
            //      Resource Directory
            // 
            Resources              = Win32Native.CSIDL_RESOURCES, 
            //
            //      Localized Resource Directory 
            //
            LocalizedResources     = Win32Native.CSIDL_RESOURCES_LOCALIZED,
            //
            //      %windir%\System32 or %windir%\syswow64 
            //
            SystemX86               = Win32Native.CSIDL_SYSTEMX86, 
            // 
            //      GetWindowsDirectory()
            // 
            Windows                = Win32Native.CSIDL_WINDOWS,
#endif // !FEATURE_CORECLR
        }
    } 
}

