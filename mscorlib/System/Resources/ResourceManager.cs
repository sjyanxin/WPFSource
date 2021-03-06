// ==++== 
//
//   Copyright (c) Microsoft Corporation.  All rights reserved.
//
// ==--== 
/*============================================================
** 
** Class:  ResourceManager 
**
** <OWNER>[....]</OWNER> 
**
**
** Purpose: Default way to access String and Object resources
** from an assembly. 
**
** 
===========================================================*/ 

namespace System.Resources { 
    using System;
    using System.IO;
    using System.Globalization;
    using System.Collections; 
    using System.Text;
    using System.Reflection; 
    using System.Runtime.Serialization; 
    using System.Security.Permissions;
    using System.Threading; 
    using System.Runtime.InteropServices;
    using System.Runtime.CompilerServices;
    using Microsoft.Win32;
    using System.Collections.Generic; 
    using System.Runtime.Versioning;
    using System.Diagnostics.Contracts; 
#if !FEATURE_CORECLR 
    using System.Diagnostics.Eventing;
#endif 

    // Resource Manager exposes an assembly's resources to an application for
    // the correct CultureInfo.  An example would be localizing text for a
    // user-visible message.  Create a set of resource files listing a name 
    // for a message and its value, compile them using ResGen, put them in
    // an appropriate place (your assembly manifest(?)), then create a Resource 
    // Manager and query for the name of the message you want.  The Resource 
    // Manager will use CultureInfo.GetCurrentUICulture() to look
    // up a resource for your user's locale settings. 
    //
    // Users should ideally create a resource file for every culture, or
    // at least a meaningful subset.  The filenames will follow the naming
    // scheme: 
    //
    // basename.culture name.resources 
    // 
    // The base name can be the name of your application, or depending on
    // the granularity desired, possibly the name of each class.  The culture 
    // name is determined from CultureInfo's Name property.
    // An example file name may be MyApp.en-US.resources for
    // MyApp's US English resources.
    // 
    // -----------------
    // Refactoring Notes 
    // ----------------- 
    // In Feb 08, began first step of refactoring ResourceManager to improve
    // maintainability (sd changelist 3012100). This resulted in breaking 
    // apart the InternalGetResourceSet "big loop" so that the file-based
    // and manifest-based lookup was located in separate methods.
    // In Apr 08, continued refactoring so that file-based and manifest-based
    // concerns are encapsulated by separate classes. At construction, the 
    // ResourceManager creates one of these classes based on whether the
    // RM will need to use file-based or manifest-based resources, and 
    // afterwards refers to this through the interface IResourceGroveler. 
    //
    // Serialization Compat: Ideally, we could have refactored further but 
    // this would have broken serialization compat. For example, the
    // ResourceManager member UseManifest and UseSatelliteAssem are no
    // longer relevant on ResourceManager. Similarly, other members could
    // ideally be moved to the file-based or manifest-based classes 
    // because they are only relevant for those types of lookup.
    // 
    // Solution now / in the future: 
    // For now, we simply use a mediator class so that we can keep these
    // members on ResourceManager but allow the file-based and manifest- 
    // based classes to access/set these members in a uniform way. See
    // ResourceManagerMediator.
    // We encapsulate fallback logic in a fallback iterator class, so that
    // this logic isn't duplicated in several methods. 
    //
    // In the future, we can look into either breaking serialization if we 
    // decide this doesn't make sense for ResourceManager (i.e. how common 
    // is the scenario), manually make serialization work by providing
    // appropriate OnSerialization, Deserialization methods. We can also 
    // look into further factoring and better design of IResourceGroveler
    // interface to accommodate unused parameters that don't make sense
    // for either file-based or manifest-based lookup paths.
    // 
    // Benefits of this refactoring:
    // - Makes it possible to understand what the ResourceManager does, 
    // which is key for maintainability. 
    // - Makes the ResourceManager more extensible by identifying and
    // encapsulating what varies 
    // - Unearthed a bug that's been lurking a while in file-based
    // lookup paths for InternalGetResourceSet if createIfNotExists is
    // false.
    // - Reuses logic, e.g. by breaking apart the culture fallback into 
    // the fallback iterator class, we don't have to repeat the
    // sometimes confusing fallback logic across multiple methods 
    // - Fxcop violations reduced to 1/5th of original count. Most 
    // importantly, code complexity violations disappeared.
    // - Finally, it got rid of dead code paths. Because the big loop was 
    // so confusing, it masked unused chunks of code. Also, dividing
    // between file-based and manifest-based allowed functionaliy
    // unused in silverlight to fall out.
    // 
    // Note: this type is integral to the construction of exception objects,
    // and sometimes this has to be done in low memory situtations (OOM) or 
    // to create TypeInitializationExceptions due to failure of a static class 
    // constructor. This type needs to be extremely careful and assume that
    // any type it references may have previously failed to construct, so statics 
    // belonging to that type may not be initialized. FrameworkEventSource.Log
    // is one such example.
    //
    [Serializable] 
    [System.Runtime.InteropServices.ComVisible(true)]
    public class ResourceManager 
    { 
        protected String BaseNameField;
        // Sets is a many-to-one table of CultureInfos mapped to ResourceSets. 
        // Don't synchronize ResourceSets - too fine-grained a lock to be effective
        [Obsolete("call InternalGetResourceSet instead")]
        protected Hashtable ResourceSets;
 
        // don't serialize the cache of ResourceSets
        [NonSerialized] 
        private Dictionary <String,ResourceSet> _resourceSets; 
        private String moduleDir;      // For assembly-ignorant directory location
        protected Assembly MainAssembly;   // Need the assembly manifest sometimes. 
        private Type _locationInfo;    // For Assembly or type-based directory layout
        private Type _userResourceSet;  // Which ResourceSet instance to create
        private CultureInfo _neutralResourcesCulture;  // For perf optimizations.
 
        private bool _ignoreCase;   // Whether case matters in GetString & GetObject
 
        private bool UseManifest;  // Use Assembly manifest, or grovel disk. 

        // unused! But need to keep for serialization 
        [OptionalField(VersionAdded = 1)]
        private bool UseSatelliteAssem;  // Are all the .resources files in the
                  // main assembly, or in satellite assemblies for each culture?
#if FEATURE_SPLIT_RESOURCES 
        private bool _isDebugSatellite;
#endif // FEATURE_SPLIT_RESOURCES 
#if RESOURCE_SATELLITE_CONFIG 
        private static Hashtable _installedSatelliteInfo;  // Give the user the option
               // to prevent certain satellite assembly probes via a config file. 
        // Note that config files are per-appdomain, not per-assembly nor process
        private static bool _checkedConfigFile;  // Did we read the app's config file?
#endif
 
        // Whether to fall back to the main assembly or a particular
        // satellite for the neutral resources. 
        [OptionalField] 
        private UltimateResourceFallbackLocation _fallbackLoc;
        // Version number of satellite assemblies to look for.  May be null. 
        [OptionalField]
        private Version _satelliteContractVersion;
        [OptionalField]
        private bool _lookedForSatelliteContractVersion; 

        // unused! But need to keep for serialization 
        [OptionalField(VersionAdded = 1)] 
        private Assembly _callingAssembly;  // Assembly who created the ResMgr.
 
        // replaces _callingAssembly
        [OptionalField(VersionAdded = 4)]
        private RuntimeAssembly m_callingAssembly;  // Assembly who created the ResMgr.
 
        // no need to serialize this; just create a new one on deserialization
        [NonSerialized] 
        private IResourceGroveler resourceGroveler; 

        public static readonly int MagicNumber = unchecked((int)0xBEEFCACE);  // If only hex had a K... 

        // Version number so ResMgr can get the ideal set of classes for you.
        // ResMgr header is:
        // 1) MagicNumber (little endian Int32) 
        // 2) HeaderVersionNumber (little endian Int32)
        // 3) Num Bytes to skip past ResMgr header (little endian Int32) 
        // 4) IResourceReader type name for this file (bytelength-prefixed UTF-8 String) 
        // 5) ResourceSet type name for this file (bytelength-prefixed UTF8 String)
        public static readonly int HeaderVersionNumber = 1; 

        //
        //It would be better if we could use _neutralCulture instead of calling
        //CultureInfo.InvariantCulture everywhere, but we run into problems with the .cctor.  CultureInfo 
        //initializes assembly, which initializes ResourceManager, which tries to get a CultureInfo which isn't
        //there yet because CultureInfo's class initializer hasn't finished.  If we move SystemResMgr off of 
        //Assembly (or at least make it an internal property) we should be able to circumvent this problem. 
        //
        //      private static CultureInfo _neutralCulture = null; 

        // This is our min required ResourceSet type.
        private static readonly Type _minResourceSet = typeof(ResourceSet);
        // These Strings are used to avoid using Reflection in CreateResourceSet. 
        // The first set are used by ResourceWriter.  The second are used by
        // InternalResGen. 
        internal static readonly String ResReaderTypeName = typeof(ResourceReader).FullName; 
        internal static readonly String ResSetTypeName = typeof(RuntimeResourceSet).FullName;
        internal static readonly String MscorlibName = typeof(ResourceReader).Assembly.FullName; 
        internal const String ResFileExtension = ".resources";
        internal const int ResFileExtensionLength = 10;

        // My private debugging aid.  Set to 5 or 6 for verbose output.  Set to 3 
        // for summary level information.
        internal static readonly int DEBUG = 0; //Making this const causes C# to consider all of the code that it guards unreachable. 
 

        [System.Security.SecuritySafeCritical]  // auto-generated 
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable
        private void Init()
        {
            m_callingAssembly = (RuntimeAssembly)Assembly.GetCallingAssembly(); 
        }
 
        protected ResourceManager() 
        {
            Init(); 
            ResourceManagerMediator mediator = new ResourceManagerMediator(this);
            resourceGroveler = new ManifestBasedResourceGroveler(mediator);

        } 

        // Constructs a Resource Manager for files beginning with 
        // baseName in the directory specified by resourceDir 
        // or in the current directory.  This Assembly-ignorant constructor is
        // mostly useful for testing your own ResourceSet implementation. 
        //
        // A good example of a baseName might be "Strings".  BaseName
        // should not end in ".resources".
        // 
        // Note: System.Windows.Forms uses this method at design time.
        // 
        [ResourceExposure(ResourceScope.Machine)] 
        [ResourceConsumption(ResourceScope.Machine)]
        private ResourceManager(String baseName, String resourceDir, Type usingResourceSet) { 

            if (null==baseName)
                throw new ArgumentNullException("baseName");
            if (null==resourceDir) 
                throw new ArgumentNullException("resourceDir");
            Contract.EndContractBlock(); 
 
            BaseNameField = baseName;
 
            moduleDir = resourceDir;
            _userResourceSet = usingResourceSet;
#pragma warning disable 618
            ResourceSets = new Hashtable(); // for backward compatibility 
#pragma warning restore 618
            _resourceSets = new Dictionary<String, ResourceSet>(); 
            UseManifest = false; 

            ResourceManagerMediator mediator = new ResourceManagerMediator(this); 
            resourceGroveler = new FileBasedResourceGroveler(mediator);

#if !FEATURE_CORECLR   // PAL doesn't support eventing, and we don't compile event providers for coreclr
            if (FrameworkEventSource.IsInitialized && FrameworkEventSource.Log.IsEnabled()) { 
                CultureInfo culture = CultureInfo.InvariantCulture;
                String defaultResName = GetResourceFileName(culture); 
 
                if (resourceGroveler.HasNeutralResources(culture, defaultResName)) {
                    FrameworkEventSource.Log.ResourceManagerNeutralResourcesFound(BaseNameField, MainAssembly, defaultResName); 
                }
                else {
                    FrameworkEventSource.Log.ResourceManagerNeutralResourcesNotFound(BaseNameField, MainAssembly, defaultResName);
                } 
            }
#endif 
        } 

        [System.Security.SecuritySafeCritical]  // auto-generated 
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var have to be marked non-inlineable
        public ResourceManager(String baseName, Assembly assembly)
        {
            if (null==baseName) 
                throw new ArgumentNullException("baseName");
            if (null==assembly) 
                throw new ArgumentNullException("assembly"); 
            Contract.EndContractBlock();
 
            if (!(assembly is RuntimeAssembly))
                throw new ArgumentException(Environment.GetResourceString("Argument_MustBeRuntimeAssembly"));

            MainAssembly = assembly; 
            BaseNameField = baseName;
 
            CommonSatelliteAssemblyInit(); 
            m_callingAssembly = (RuntimeAssembly)Assembly.GetCallingAssembly();
            // Special case for mscorlib - protect mscorlib's private resources. 
            // This isn't for security reasons, but to ensure we can make
            // breaking changes to mscorlib's internal resources without
            // assuming users may have taken a dependency on them.
            if (assembly == typeof(Object).Assembly && m_callingAssembly != assembly) 
            {
                m_callingAssembly = null; 
            } 
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable
        public ResourceManager(String baseName, Assembly assembly, Type usingResourceSet)
        { 
            if (null==baseName)
                throw new ArgumentNullException("baseName"); 
            if (null==assembly) 
                throw new ArgumentNullException("assembly");
            Contract.EndContractBlock(); 

            if (!(assembly is RuntimeAssembly))
                throw new ArgumentException(Environment.GetResourceString("Argument_MustBeRuntimeAssembly"));
 
            MainAssembly = assembly;
            BaseNameField = baseName; 
 
            if (usingResourceSet != null && (usingResourceSet != _minResourceSet) && !(usingResourceSet.IsSubclassOf(_minResourceSet)))
                throw new ArgumentException(Environment.GetResourceString("Arg_ResMgrNotResSet"), "usingResourceSet"); 
            _userResourceSet = usingResourceSet;

            CommonSatelliteAssemblyInit();
            m_callingAssembly = (RuntimeAssembly)Assembly.GetCallingAssembly(); 
            // Special case for mscorlib - protect mscorlib's private resources.
            // This isn't for security reasons, but to ensure we can make 
            // breaking changes to mscorlib's internal resources without 
            // assuming users may have taken a dependency on them.
            if (assembly == typeof(Object).Assembly && m_callingAssembly != assembly) 
                m_callingAssembly = null;
        }

        [System.Security.SecuritySafeCritical]  // auto-generated 
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable
        public ResourceManager(Type resourceSource) 
        { 
            if (null==resourceSource)
                throw new ArgumentNullException("resourceSource"); 
            Contract.EndContractBlock();

            if (!(resourceSource is RuntimeType))
                throw new ArgumentException(Environment.GetResourceString("Argument_MustBeRuntimeType")); 

            _locationInfo = resourceSource; 
            MainAssembly = _locationInfo.Assembly; 
            BaseNameField = resourceSource.Name;
 
            CommonSatelliteAssemblyInit();
            m_callingAssembly = (RuntimeAssembly)Assembly.GetCallingAssembly();
            // Special case for mscorlib - protect mscorlib's private resources.
            if (MainAssembly == typeof(Object).Assembly && m_callingAssembly != MainAssembly) 
            {
                m_callingAssembly = null; 
            } 
        }
 
#if FEATURE_SPLIT_RESOURCES
        internal ResourceManager(String baseName, Assembly assembly, bool isDebugSatellite) : this(baseName, assembly) {
            _isDebugSatellite = isDebugSatellite;
        } 
#endif // FEATURE_SPLIT_RESOURCES
 
        [OnDeserializing] 
        private void OnDeserializing(StreamingContext ctx)
        { 
            this._resourceSets = null;
            this.resourceGroveler = null;
        }
 
        [System.Security.SecuritySafeCritical]
        [OnDeserialized] 
        private void OnDeserialized(StreamingContext ctx) 
        {
            _resourceSets = new Dictionary<String, ResourceSet>(); 

            // set up resource groveler, depending on whether this ResourceManager
            // is looking for files or assemblies
            ResourceManagerMediator mediator = new ResourceManagerMediator(this); 
            if (UseManifest)
            { 
                resourceGroveler = new ManifestBasedResourceGroveler(mediator); 
            }
            else 
            {
                resourceGroveler = new FileBasedResourceGroveler(mediator);
            }
 
            // correct callingAssembly for v2
            if (this.m_callingAssembly == null) 
            { 
                this.m_callingAssembly = (RuntimeAssembly)_callingAssembly;
            } 

            // v2 does this lazily
            if (UseManifest && this._neutralResourcesCulture == null)
            { 
                _neutralResourcesCulture = ManifestBasedResourceGroveler.GetNeutralResourcesLanguage(MainAssembly, ref _fallbackLoc);
            } 
        } 

        [OnSerializing] 
        private void OnSerializing(StreamingContext ctx)
        {
            // Initialize the fields Whidbey expects
            _callingAssembly = m_callingAssembly; 
            UseSatelliteAssem = UseManifest;
#pragma warning disable 618 
            ResourceSets = new Hashtable(); // for backward compatibility 
#pragma warning restore 618
        } 


        // Trying to unify code as much as possible, even though having to do a
        // security check in each constructor prevents it. 
        [System.Security.SecuritySafeCritical]
        private void CommonSatelliteAssemblyInit() 
        { 
            UseManifest = true;
 
            _resourceSets = new Dictionary<String,ResourceSet>();

            _fallbackLoc = UltimateResourceFallbackLocation.MainAssembly;
 
            ResourceManagerMediator mediator = new ResourceManagerMediator(this);
            resourceGroveler = new ManifestBasedResourceGroveler(mediator); 
 
            _neutralResourcesCulture = ManifestBasedResourceGroveler.GetNeutralResourcesLanguage(MainAssembly, ref _fallbackLoc);
 
#if !FEATURE_CORECLR   // PAL doesn't support eventing, and we don't compile event providers for coreclr
            if (FrameworkEventSource.IsInitialized && FrameworkEventSource.Log.IsEnabled()) {
                CultureInfo culture = CultureInfo.InvariantCulture;
                String defaultResName = GetResourceFileName(culture); 

                if (resourceGroveler.HasNeutralResources(culture, defaultResName)) { 
                    FrameworkEventSource.Log.ResourceManagerNeutralResourcesFound(BaseNameField, MainAssembly, defaultResName); 
                }
                else { 
                    String outputResName = defaultResName;
                    if (_locationInfo != null && _locationInfo.Namespace != null)
                        outputResName = _locationInfo.Namespace + Type.Delimiter + defaultResName;
                    FrameworkEventSource.Log.ResourceManagerNeutralResourcesNotFound(BaseNameField, MainAssembly, outputResName); 
                }
            } 
 
#pragma warning disable 618
            ResourceSets = new Hashtable(); // for backward compatibility 
#pragma warning restore 618
#endif
        }
 
        // Gets the base name for the ResourceManager.
        public virtual String BaseName { 
            get { return BaseNameField; } 
        }
 
        // Whether we should ignore the capitalization of resources when calling
        // GetString or GetObject.
        public virtual bool IgnoreCase {
            get { return _ignoreCase; } 
            set { _ignoreCase = value; }
        } 
 
        // Returns the Type of the ResourceSet the ResourceManager uses
        // to construct ResourceSets. 
        public virtual Type ResourceSetType {
            get { return (_userResourceSet == null) ? typeof(RuntimeResourceSet) : _userResourceSet; }
        }
 
        protected UltimateResourceFallbackLocation FallbackLocation
        { 
            get { return _fallbackLoc; } 
            set { _fallbackLoc = value; }
        } 

        // Tells the ResourceManager to call Close on all ResourceSets and
        // release all resources.  This will shrink your working set by
        // potentially a substantial amount in a running application.  Any 
        // future resource lookups on this ResourceManager will be as
        // expensive as the very first lookup, since it will need to search 
        // for files and load resources again. 
        //
        // This may be useful in some complex threading scenarios, where 
        // creating a new ResourceManager isn't quite the correct behavior.
        public virtual void ReleaseAllResources()
        {
#if !FEATURE_CORECLR 
            if (FrameworkEventSource.IsInitialized)
            { 
                FrameworkEventSource.Log.ResourceManagerReleasingResources(BaseNameField, MainAssembly); 
            }
#endif 
            IDictionaryEnumerator setEnum = _resourceSets.GetEnumerator();

            // If any calls to Close throw, at least leave ourselves in a
            // consistent state. 
            _resourceSets = new Dictionary<String,ResourceSet>();
 
#if !FEATURE_CORECLR 
            IDictionaryEnumerator setEnum2 = null;
#pragma warning disable 618 
            if (ResourceSets != null) {
                setEnum2 = ResourceSets.GetEnumerator();
            }
            ResourceSets = new Hashtable(); // for backwards compat 
#pragma warning restore 618
#endif 
 
            while (setEnum.MoveNext()) {
                ((ResourceSet)setEnum.Value).Close(); 
            }

#if !FEATURE_CORECLR
            if (setEnum2 != null) { 
                while (setEnum2.MoveNext()) {
                    ((ResourceSet)setEnum2.Value).Close(); 
                } 
            }
#endif 
        }

        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)] 
        public static ResourceManager CreateFileBasedResourceManager(String baseName, String resourceDir, Type usingResourceSet)
        { 
            return new ResourceManager(baseName, resourceDir, usingResourceSet); 
        }
 
        // Given a CultureInfo, GetResourceFileName generates the name for
        // the binary file for the given CultureInfo.  This method uses
        // CultureInfo's Name property as part of the file name for all cultures
        // other than the invariant culture.  This method does not touch the disk, 
        // and is used only to construct what a resource file name (suitable for
        // passing to the ResourceReader constructor) or a manifest resource file 
        // name should look like. 
        //
        // This method can be overriden to look for a different extension, 
        // such as ".ResX", or a completely different format for naming files.
        protected virtual String GetResourceFileName(CultureInfo culture) {
            StringBuilder sb = new StringBuilder(255);
            sb.Append(BaseNameField); 
            // If this is the neutral culture, don't append culture name.
            if (!culture.HasInvariantCultureName) 
            { 
                CultureInfo.VerifyCultureName(culture.Name, true);
                sb.Append('.'); 
                sb.Append(culture.Name);
            }
            sb.Append(ResFileExtension);
            return sb.ToString(); 
        }
 
        // Looks up a set of resources for a particular CultureInfo.  This is 
        // not useful for most users of the ResourceManager - call
        // GetString() or GetObject() instead. 
        //
        // The parameters let you control whether the ResourceSet is created
        // if it hasn't yet been loaded and if parent CultureInfos should be
        // loaded as well for resource inheritance. 
        //
        [System.Security.SecuritySafeCritical]  // auto-generated 
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var have to be marked non-inlineable 
        public virtual ResourceSet GetResourceSet(CultureInfo culture, bool createIfNotExists, bool tryParents) {
            if (null==culture) 
                throw new ArgumentNullException("culture");
            Contract.EndContractBlock();

            Dictionary<String,ResourceSet> localResourceSets = _resourceSets; 
            ResourceSet rs;
            if (localResourceSets != null) { 
                lock (localResourceSets) { 
                    if (localResourceSets.TryGetValue(culture.Name, out rs))
                        return rs; 
                }
            }

            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller; 

            if (UseManifest && culture.HasInvariantCultureName) { 
                string fileName = GetResourceFileName(culture); 
                RuntimeAssembly mainAssembly = (RuntimeAssembly)MainAssembly;
                Stream stream = mainAssembly.GetManifestResourceStream(_locationInfo, fileName, m_callingAssembly == MainAssembly, ref stackMark); 
                if (createIfNotExists && stream!=null) {
                    rs = ((ManifestBasedResourceGroveler)resourceGroveler).CreateResourceSet(stream, MainAssembly);
                    AddResourceSet(localResourceSets, culture.Name, ref rs);
                    return rs; 
                }
            } 
 
            // Note: ideally we could plumb through the stack crawl mark here, but we must
            // call the virtual 3-argument InternalGetResourceSet method for compatibility. 
            // Security-wise, we're not overly interested in protecting access to resources,
            // since full-trust callers can get them already and most resources are public.
            // Also, the JIT inliner could always inline a caller into another assembly's
            // method, so the entire idea of a security check written this way is ----. 
            // So if we happen to return some resources in cases where we should really be
            // doing a demand for member access permissions, we're not overly concerned. 
            // <STRIP>I verified this with our CAS expert.  -- Brian, 2/11/2010</STRIP> 
            return InternalGetResourceSet(culture, createIfNotExists, tryParents);
        } 

        // InternalGetResourceSet is a non-threadsafe method where all the logic
        // for getting a resource set lives.  Access to it is controlled by
        // threadsafe methods such as GetResourceSet, GetString, & GetObject. 
        // This will take a minimal number of locks.
        [System.Security.SecuritySafeCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.None)] 
        [ResourceConsumption(ResourceScope.Machine, ResourceScope.Machine)]
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable 
        protected virtual ResourceSet InternalGetResourceSet(CultureInfo culture, bool createIfNotExists, bool tryParents)
        {
            Contract.Assert(culture != null, "culture != null");
 
            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller;
            return InternalGetResourceSet(culture,createIfNotExists,tryParents, ref stackMark); 
        } 

        // InternalGetResourceSet is a non-threadsafe method where all the logic 
        // for getting a resource set lives.  Access to it is controlled by
        // threadsafe methods such as GetResourceSet, GetString, & GetObject.
        // This will take a minimal number of locks.
        [System.Security.SecurityCritical] 
        [ResourceExposure(ResourceScope.None)]
        [ResourceConsumption(ResourceScope.Machine, ResourceScope.Machine)] 
        private ResourceSet InternalGetResourceSet(CultureInfo requestedCulture, bool createIfNotExists, bool tryParents, ref StackCrawlMark stackMark) 
        {
            Dictionary<String, ResourceSet> localResourceSets = _resourceSets; 
            ResourceFallbackManager mgr = new ResourceFallbackManager(requestedCulture, _neutralResourcesCulture, tryParents);
            ResourceSet rs = null;
            CultureInfo foundCulture = null;
 
            foreach (CultureInfo currentCultureInfo in mgr)
            { 
#if !FEATURE_CORECLR 
                if (FrameworkEventSource.IsInitialized)
                { 
                    FrameworkEventSource.Log.ResourceManagerLookingForResourceSet(BaseNameField, MainAssembly, currentCultureInfo.Name);
                }
#endif
                lock(localResourceSets) { 
                    if (localResourceSets.TryGetValue(currentCultureInfo.Name, out rs)) {
#if !FEATURE_CORECLR 
                        if (FrameworkEventSource.IsInitialized) 
                        {
                            FrameworkEventSource.Log.ResourceManagerFoundResourceSetInCache(BaseNameField, MainAssembly, currentCultureInfo.Name); 
                        }
#endif
                        break;
                    } 
                }
 
                // InternalGetResourceSet will never be threadsafe.  However, it must 
                // be protected against reentrancy from the SAME THREAD.  (ie, calling
                // GetSatelliteAssembly may send some window messages or trigger the 
                // Assembly load event, which could fail then call back into the
                // ResourceManager).  It's happened.

                rs = resourceGroveler.GrovelForResourceSet(currentCultureInfo, localResourceSets, 
                                                           tryParents, createIfNotExists, ref stackMark);
 
                // found a ResourceSet; we're done 
                if (rs != null)
                { 
                    foundCulture = currentCultureInfo;
                    break;
                }
 
            }
 
            if (rs != null && foundCulture != null) 
            {
                // add entries to the cache for the cultures we have gone through 

                // currentCultureInfo now refers to the culture that had resources.
                // update cultures starting from requested culture up to the culture
                // that had resources. 
                foreach (CultureInfo updateCultureInfo in mgr)
                { 
                    AddResourceSet(localResourceSets, updateCultureInfo.Name, ref rs); 

                    // stop when we've added current or reached invariant (top of chain) 
                    if (updateCultureInfo == foundCulture)
                    {
                        break;
                    } 
                }
            } 
 
            return rs;
        } 

        // Simple helper to ease maintenance and improve readability.
        private static void AddResourceSet(Dictionary<String,ResourceSet> localResourceSets, String cultureName, ref ResourceSet rs)
        { 
            // InternalGetResourceSet is both recursive and reentrant -
            // assembly load callbacks in particular are a way we can call 
            // back into the ResourceManager in unexpectedly on the same thread. 
            lock(localResourceSets) {
                // If another thread added this culture, return that. 
                ResourceSet lost----;
                if (localResourceSets.TryGetValue(cultureName, out lost----)) {
                    if (!Object.ReferenceEquals(lost----, rs)) {
                        // Note: In certain cases, we can be trying to add a ResourceSet for multiple 
                        // cultures on one thread, while a second thread added another ResourceSet for one
                        // of those cultures.  So when we lose the ----, we must make sure our ResourceSet 
                        // isn't in our dictionary before closing it. 
                        if (!localResourceSets.ContainsValue(rs))
 	                        rs.Dispose(); 
                        rs = lost----;
                    }
                }
                else { 
                    localResourceSets.Add(cultureName, rs);
                } 
            } 
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated
        protected static Version GetSatelliteContractVersion(Assembly a)
        {
            // Ensure that the assembly reference is not null 
            if (a == null) {
                throw new ArgumentNullException("a", Environment.GetResourceString("ArgumentNull_Assembly")); 
            } 
            Contract.EndContractBlock();
 
            String v = null;
            if (a.ReflectionOnly) {
                foreach (CustomAttributeData data in CustomAttributeData.GetCustomAttributes(a)) {
                    if (data.Constructor.DeclaringType == typeof(SatelliteContractVersionAttribute)) { 
                        v = (String)data.ConstructorArguments[0].Value;
                        break; 
                    } 
                }
 
                if (v == null)
                    return null;
            }
            else { 
                Object[] attrs = a.GetCustomAttributes(typeof(SatelliteContractVersionAttribute), false);
                if (attrs.Length == 0) 
                    return null; 
                Contract.Assert(attrs.Length == 1, "Cannot have multiple instances of SatelliteContractVersionAttribute on an assembly!");
                v = ((SatelliteContractVersionAttribute)attrs[0]).Version; 
            }
            Version ver;
            try {
                ver = new Version(v); 
            }
            catch(ArgumentOutOfRangeException e) { 
                // Note we are prone to hitting infinite loops if mscorlib's 
                // SatelliteContractVersionAttribute contains bogus values.
                // If this assert fires, please fix the build process for the 
                // BCL directory.
                if (a == typeof(Object).Assembly) {
                    Contract.Assert(false, "mscorlib's SatelliteContractVersionAttribute is a malformed version string!");
                    return null; 
                }
 
                throw new ArgumentException(Environment.GetResourceString("Arg_InvalidSatelliteContract_Asm_Ver", a.ToString(), v), e); 
            }
            return ver; 
        }

        [System.Security.SecuritySafeCritical]  // auto-generated
        protected static CultureInfo GetNeutralResourcesLanguage(Assembly a) 
        {
            // This method should be obsolete - replace it with the one below. 
            // Unfortunately, we made it protected. 
            UltimateResourceFallbackLocation ignoringUsefulData = UltimateResourceFallbackLocation.MainAssembly;
            CultureInfo culture = ManifestBasedResourceGroveler.GetNeutralResourcesLanguage(a, ref ignoringUsefulData); 
            return culture;
        }

        // IGNORES VERSION 
        internal static bool CompareNames(String asmTypeName1,
                                          String typeName2, 
                                          AssemblyName asmName2) 
        {
            Contract.Assert(asmTypeName1 != null, "asmTypeName1 was unexpectedly null"); 

            // First, compare type names
            int comma = asmTypeName1.IndexOf(',');
            if (((comma == -1) ? asmTypeName1.Length : comma) != typeName2.Length) 
                return false;
 
            // case sensitive 
            if (String.Compare(asmTypeName1, 0, typeName2, 0, typeName2.Length, StringComparison.Ordinal) != 0)
                return false; 
            if (comma == -1)
                return true;

            // Now, compare assembly display names (IGNORES VERSION AND PROCESSORARCHITECTURE) 
            // also, for  mscorlib ignores everything, since that's what the binder is going to do
            while(Char.IsWhiteSpace(asmTypeName1[++comma])); 
 
            // case insensitive
            AssemblyName an1 = new AssemblyName(asmTypeName1.Substring(comma)); 
            if (String.Compare(an1.Name, asmName2.Name, StringComparison.OrdinalIgnoreCase) != 0)
                return false;

            // to match IsMscorlib() in VM 
            if (String.Compare(an1.Name, "mscorlib", StringComparison.OrdinalIgnoreCase) == 0)
                return true; 
 

            if ((an1.CultureInfo != null) && (asmName2.CultureInfo != null) && 
#if FEATURE_USE_LCID
                (an1.CultureInfo.LCID != asmName2.CultureInfo.LCID)
#else
                (an1.CultureInfo.Name != asmName2.CultureInfo.Name) 
#endif
                ) 
                return false; 

            byte[] pkt1 = an1.GetPublicKeyToken(); 
            byte[] pkt2 = asmName2.GetPublicKeyToken();
            if ((pkt1 != null) && (pkt2 != null)) {
                if (pkt1.Length != pkt2.Length)
                    return false; 

                for(int i=0; i < pkt1.Length; i++) { 
                    if(pkt1[i] != pkt2[i]) 
                        return false;
                } 
            }

            return true;
        } 

        // Looks up a resource value for a particular name.  Looks in the 
        // current thread's CultureInfo, and if not found, all parent CultureInfos. 
        // Returns null if the resource wasn't found.
        // 
        public virtual String GetString(String name) {
            return GetString(name, (CultureInfo)null);
        }
 
        // Looks up a resource value for a particular name.  Looks in the
        // specified CultureInfo, and if not found, all parent CultureInfos. 
        // Returns null if the resource wasn't found. 
        //
        [System.Security.SecuritySafeCritical]  // auto-generated 
        public virtual String GetString(String name, CultureInfo culture) {
            if (null==name)
                throw new ArgumentNullException("name");
            if (null==culture) { 
                culture = CultureInfo.CurrentUICulture;
            } 
 
#if !FEATURE_CORECLR
            if (FrameworkEventSource.IsInitialized) 
            {
                FrameworkEventSource.Log.ResourceManagerLookupStarted(BaseNameField, MainAssembly, culture.Name);
            }
#endif 

            // This is the CultureInfo hierarchy traversal code for resource 
            // lookups, similar but necessarily orthogonal to the ResourceSet 
            // lookup logic.
            ResourceFallbackManager mgr = new ResourceFallbackManager(culture, _neutralResourcesCulture, true); 
            ResourceSet last = null;
            foreach (CultureInfo currentCultureInfo in mgr) {
                ResourceSet rs = InternalGetResourceSet(currentCultureInfo, true, true);
                if (rs == null) 
                    break;
 
                if (rs != last) { 
                    String value = rs.GetString(name, _ignoreCase);
                    if (value != null) 
                        return value;

                    last = rs;
                } 
            }
 
#if !FEATURE_CORECLR 
            if (FrameworkEventSource.IsInitialized)
            { 
                FrameworkEventSource.Log.ResourceManagerLookupFailed(BaseNameField, MainAssembly, culture.Name);
            }
#endif
 
            return null;
        } 
 

        // Looks up a resource value for a particular name.  Looks in the 
        // current thread's CultureInfo, and if not found, all parent CultureInfos.
        // Returns null if the resource wasn't found.
        //
        public virtual Object GetObject(String name) { 
            return GetObject(name, (CultureInfo)null, true);
        } 
 
        // Looks up a resource value for a particular name.  Looks in the
        // specified CultureInfo, and if not found, all parent CultureInfos. 
        // Returns null if the resource wasn't found.
        public virtual Object GetObject(String name, CultureInfo culture) {
            return GetObject(name, culture, true);
        } 

        [System.Security.SecuritySafeCritical]  // auto-generated 
        private Object GetObject(String name, CultureInfo culture, bool wrapUnmanagedMemStream) 
        {
            if (null==name) 
                throw new ArgumentNullException("name");
            if (null==culture) {
                culture = CultureInfo.CurrentUICulture;
            } 

#if !FEATURE_CORECLR 
            if (FrameworkEventSource.IsInitialized) 
            {
                FrameworkEventSource.Log.ResourceManagerLookupStarted(BaseNameField, MainAssembly, culture.Name); 
            }
#endif

            // This is the CultureInfo hierarchy traversal code for resource 
            // lookups, similar but necessarily orthogonal to the ResourceSet
            // lookup logic. 
            ResourceFallbackManager mgr = new ResourceFallbackManager(culture, _neutralResourcesCulture, true); 
            ResourceSet last = null;
            foreach (CultureInfo currentCultureInfo in mgr) { 
                // Note: Technically this method should be passed in a stack crawl mark that we then pass
                // to InternalGetResourceSet for ensuring we demand permissions to read your private resources
                // if you're reading resources from an assembly other than yourself.  But, we must call our
                // three argument overload (without the stack crawl mark) for compatibility.  After 
                // consideration, we aren't worried about the security impact.
                ResourceSet rs = InternalGetResourceSet(currentCultureInfo, true, true); 
                if (rs == null) 
                    break;
 
                if (rs != last) {
                    Object value = rs.GetObject(name, _ignoreCase);
                    if (value != null) {
                        UnmanagedMemoryStream stream = value as UnmanagedMemoryStream; 
                        if (stream != null && wrapUnmanagedMemStream)
                            return new UnmanagedMemoryStreamWrapper(stream); 
                        else 
                            return value;
                    } 

                    last = rs;
                }
            } 

#if !FEATURE_CORECLR 
            if (FrameworkEventSource.IsInitialized) 
            {
                FrameworkEventSource.Log.ResourceManagerLookupFailed(BaseNameField, MainAssembly, culture.Name); 
            }
#endif
            return null;
        } 

        [ComVisible(false)] 
        public UnmanagedMemoryStream GetStream(String name) { 
            return GetStream(name, (CultureInfo)null);
        } 

        [ComVisible(false)]
        public UnmanagedMemoryStream GetStream(String name, CultureInfo culture) {
            Object obj = GetObject(name, culture, false); 
            UnmanagedMemoryStream ums = obj as UnmanagedMemoryStream;
            if (ums == null && obj != null) 
                throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_ResourceNotStream_Name", name)); 
            return ums;
        } 

#if RESOURCE_SATELLITE_CONFIG
        // Internal helper method - gives an end user the ability to prevent
        // satellite assembly probes for certain cultures via a config file. 
        [System.Security.SecurityCritical]  // auto-generated
        private bool TryLookingForSatellite(CultureInfo lookForCulture) 
        { 
            if (!_checkedConfigFile) {
                lock (this) { 
                    if (!_checkedConfigFile) {
                        _checkedConfigFile = true;
                        _installedSatelliteInfo = GetSatelliteAssembliesFromConfig();
                    } 
                }
            } 
 
            if (_installedSatelliteInfo == null)
                return true; 

            String[] installedSatellites = (String[]) _installedSatelliteInfo[MainAssembly.FullName];

            if (installedSatellites == null) 
                return true;
 
            // The config file told us what satellites might be installed. 
            int pos = Array.IndexOf(installedSatellites, lookForCulture.Name);
 
#if !FEATURE_CORECLR
            if (FrameworkEventSource.IsInitialized && FrameworkEventSource.Log.IsEnabled()) {
                if (pos < 0) {
                    FrameworkEventSource.Log.ResourceManagerCultureNotFoundInConfigFile(BaseNameField, MainAssembly, lookForCulture.Name); 
                }
                else { 
                    FrameworkEventSource.Log.ResourceManagerCultureFoundInConfigFile(BaseNameField, MainAssembly, lookForCulture.Name); 
                }
            } 
#endif
            return pos >= 0;
        }
 
        // Note: There is one config file per appdomain.  This is not
        // per-process nor per-assembly. 
        [System.Security.SecurityCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.None)]
        [ResourceConsumption(ResourceScope.Machine, ResourceScope.Machine)] 
        private Hashtable GetSatelliteAssembliesFromConfig()
        {
#if FEATURE_FUSION
 
            String fileName = AppDomain.CurrentDomain.FusionStore.ConfigurationFileInternal;
            if (fileName == null) { 
                return null; 
            }
 
            // Don't do a security assert.  We need to support semi-trusted
            // scenarios, but asserting here causes infinite resource lookups
            // while initializing security & looking up mscorlib's config file.
            // Use internal methods to bypass security checks. 

            // If we're dealing with a local file name or a UNC path instead 
            // of a URL, check to see if the file exists here for perf (avoids 
            // throwing a FileNotFoundException).
            if (fileName.Length >= 2 && 
                ((fileName[1] == Path.VolumeSeparatorChar) || (fileName[0] == Path.DirectorySeparatorChar && fileName[1] == Path.DirectorySeparatorChar)) &&
                !File.InternalExists(fileName))
                return null;
 
            ConfigTreeParser parser = new ConfigTreeParser();
            String queryPath = "/configuration/satelliteassemblies"; 
            ConfigNode node = null; 
            // Catch exceptions in case a web app doesn't have a config file.
            try { 
                node = parser.Parse(fileName, queryPath, true);
            }
            catch(Exception) {}
 
            if (node == null) {
                return null; 
            } 

            // The application config file will contain sections like this: 
            // <?xml version="1.0"?>
            // <configuration>
            //     <satelliteassemblies>
            //         <assembly name="mscorlib, Version=..., PublicKeyToken=..."> 
            //             <culture>fr</culture>
            //         </assembly> 
            //         <assembly name="UserAssembly, ..."> 
            //             <culture>fr-FR</culture>
            //             <culture>de-CH</culture> 
            //         </assembly>
            //         <assembly name="UserAssembly2, ...">
            //         </assembly>
            //    </satelliteassemblies> 
            // </configuration>
            Hashtable satelliteInfo = new Hashtable(StringComparer.OrdinalIgnoreCase); 
            foreach(ConfigNode assemblyNode in node.Children) { 
                if (!String.Equals(assemblyNode.Name, "assembly"))
                    throw new ApplicationException(Environment.GetResourceString("XMLSyntax_InvalidSyntaxSatAssemTag", Path.GetFileName(fileName), assemblyNode.Name)); 

                if (assemblyNode.Attributes.Count == 0)
                    throw new ApplicationException(Environment.GetResourceString("XMLSyntax_InvalidSyntaxSatAssemTagNoAttr", Path.GetFileName(fileName)));
 
                DictionaryEntry de = (DictionaryEntry) assemblyNode.Attributes[0];
                String assemblyName = (String) de.Value; 
                if (!String.Equals(de.Key, "name") || String.IsNullOrEmpty(assemblyName) || assemblyNode.Attributes.Count > 1) 
                    throw new ApplicationException(Environment.GetResourceString("XMLSyntax_InvalidSyntaxSatAssemTagBadAttr", Path.GetFileName(fileName), de.Key, de.Value));
 
                ArrayList list = new ArrayList(5);
                foreach(ConfigNode child in assemblyNode.Children)
                    if (child.Value != null)
                        list.Add(child.Value); 

                String[] satellites = new String[list.Count]; 
                for(int i=0; i<satellites.Length; i++) { 
                    String cultureName = (String)list[i];
                    satellites[i] = cultureName; 
#if !FEATURE_CORECLR
                    if (FrameworkEventSource.IsInitialized)
                    {
                        FrameworkEventSource.Log.ResourceManagerAddingCultureFromConfigFile(BaseNameField, MainAssembly, cultureName); 
                    }
#endif 
                } 

                satelliteInfo.Add(assemblyName, satellites); 
            }

            return satelliteInfo;
#else 
            return null;
#endif //FEATURE_FUSION 
 
        }
#endif  // RESOURCE_SATELLITE_CONFIG 

        internal class ResourceManagerMediator
        {
            private ResourceManager _rm; 

            internal ResourceManagerMediator(ResourceManager rm) 
            { 
                if (rm == null)
                { 
                    throw new ArgumentNullException("rm");
                }
                _rm = rm;
            } 

            // NEEDED ONLY BY FILE-BASED 
            internal String ModuleDir 
            {
                get { return _rm.moduleDir; } 
            }

            // NEEDED BOTH BY FILE-BASED  AND ----Y-BASED
            internal Type LocationInfo 
            {
                get { return _rm._locationInfo; } 
            } 

            internal Type UserResourceSet 
            {
                get { return _rm._userResourceSet; }
            }
 
            internal String BaseNameField
            { 
                get { return _rm.BaseNameField; } 
            }
 
            internal CultureInfo NeutralResourcesCulture
            {
                get { return _rm._neutralResourcesCulture; }
                set { _rm._neutralResourcesCulture = value; } 
            }
 
            internal String GetResourceFileName(CultureInfo culture) 
            {
                return _rm.GetResourceFileName(culture); 
            }

            // NEEDED ONLY BY ----Y-BASED
            internal bool LookedForSatelliteContractVersion 
            {
                get { return _rm._lookedForSatelliteContractVersion; } 
                set { _rm._lookedForSatelliteContractVersion = value; } 
            }
 
            internal Version SatelliteContractVersion
            {
                get { return _rm._satelliteContractVersion; }
                set { _rm._satelliteContractVersion = value; } 
            }
 
            internal Version ObtainSatelliteContractVersion(Assembly a) 
            {
                return ResourceManager.GetSatelliteContractVersion(a); 
            }

            internal UltimateResourceFallbackLocation FallbackLoc
            { 
                get { return _rm.FallbackLocation; }
                set { _rm._fallbackLoc = value; } 
            } 

            internal RuntimeAssembly CallingAssembly 
            {
                get { return _rm.m_callingAssembly; }
            }
 
#if FEATURE_SPLIT_RESOURCES
            internal bool IsDebugSatellite 
            { 
                get { return _rm._isDebugSatellite; }
            } 
#endif

            internal RuntimeAssembly MainAssembly
            { 
                get { return (RuntimeAssembly)_rm.MainAssembly; }
            } 
 
            // this is weird because we have BaseNameField accessor above, but we're sticking
            // with it for compat. 
            internal String BaseName
            {
                get { return _rm.BaseName; }
            } 

 
#if RESOURCE_SATELLITE_CONFIG 
            [System.Security.SecurityCritical]  // auto-generated
            internal bool TryLookingForSatellite(CultureInfo lookForCulture) 
            {
                return _rm.TryLookingForSatellite(lookForCulture);
            }
#endif 

        } 
    } 
}

