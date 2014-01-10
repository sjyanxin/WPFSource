// ==++== 
//
//   Copyright (c) Microsoft Corporation.  All rights reserved.
//
// ==--== 
//
// ResourcesEtwProvider.cs 
// 
// <OWNER>[....]</OWNER>
// <OWNER>[....]</OWNER> 
//
// Managed event source for things that can version with MSCORLIB.
//
using System; 
using System.Collections.Generic;
using System.Globalization; 
using System.Reflection; 
using System.Text;
 
namespace System.Diagnostics.Eventing {

    sealed internal class FrameworkEventSource : EventProviderBase {
        // Defines the singleton instance for the Resources ETW provider 
        public static readonly FrameworkEventSource Log = new FrameworkEventSource();
 
        // Task defintions.  Each event belows to a group called a task.  Typically these are subsystems 
        // within the framework
        public const EventTask ResourceManagerTask = (EventTask)1; 
        // public const EventTask Task1        = (EventTask)2;
        // public const EventTask Task3        = (EventTask)3;

        // Keyword definitions.  These represent logical groups of events that can be turned on and off independently 
        // Often each task has a keyword, but where tasks are determined by subsystem, keywords are determined by
        // usefulness to end users to filter.  Generally users don't mind extra events if they are not high volume 
        // so grouping low volume events together in a single keywords is OK (users can post-filter by task if desired) 
        public const EventKeywords Loader = (EventKeywords)0x0001; // This is bit 0
        // public const EventKeywords SubSystem2 =  (EventKeywords)0x0002; 
        // public const EventKeywords SubSystem3 =  (EventKeywords)0x0004;

        // This predicate is used by consumers of this class to deteremine if the class has actually been initialized,
        // and therefore if the public statics are available for use. This is typically not a problem... if the static 
        // class constructor fails, then attempts to access the statics (or even this property) will result in a
        // TypeInitializationException. However, that is not the case while the class loader is actually trying to construct 
        // the TypeInitializationException instance to represent that failure, and some consumers of this class are on 
        // that code path, specifically the resource manager.
        public static bool IsInitialized 
        {
            get
            {
                return Log != null; 
            }
        } 
 
        // The FrameworkEventSource GUID is {8E9F5090-2D75-4d03-8A81-E5AFBF85DAF1}
        private FrameworkEventSource() : base(new Guid(0x8e9f5090, 0x2d75, 0x4d03, 0x8a, 0x81, 0xe5, 0xaf, 0xbf, 0x85, 0xda, 0xf1)) { } 

        // ResourceManager Event Definitions

        [Event(1, Level = EventLevel.Informational, Task = ResourceManagerTask, Keywords = Loader)] 
        public void ResourceManagerLookupStarted(String baseName, String mainAssemblyName, String cultureName) {
            WriteEvent(1, baseName, mainAssemblyName, cultureName); 
        } 

        [Event(2, Level = EventLevel.Informational, Task = ResourceManagerTask, Keywords = Loader)] 
        public void ResourceManagerLookingForResourceSet(String baseName, String mainAssemblyName, String cultureName) {
            if (IsEnabled())
                WriteEvent(2, baseName, mainAssemblyName, cultureName);
        } 

        [Event(3, Level = EventLevel.Informational, Task = ResourceManagerTask, Keywords = Loader)] 
        public void ResourceManagerFoundResourceSetInCache(String baseName, String mainAssemblyName, String cultureName) { 
            if (IsEnabled())
                WriteEvent(3, baseName, mainAssemblyName, cultureName); 
        }

        // After loading a satellite assembly, we already have the ResourceSet for this culture in
        // the cache. This can happen if you have an assembly load callback that called into this 
        // instance of the ResourceManager.
        [Event(4, Level = EventLevel.Warning, Task = ResourceManagerTask, Keywords = Loader)] 
        public void ResourceManagerFoundResourceSetInCacheUnexpected(String baseName, String mainAssemblyName, String cultureName) { 
            if (IsEnabled())
                WriteEvent(4, baseName, mainAssemblyName, cultureName); 
        }

        // manifest resource stream lookup succeeded
        [Event(5, Level = EventLevel.Informational, Task = ResourceManagerTask, Keywords = Loader)] 
        public void ResourceManagerStreamFound(String baseName, String mainAssemblyName, String cultureName, String loadedAssemblyName, String resourceFileName) {
            if (IsEnabled()) 
                WriteEvent(5, baseName, mainAssemblyName, cultureName, loadedAssemblyName, resourceFileName); 
        }
 
        // manifest resource stream lookup failed
        [Event(6, Level = EventLevel.Warning, Task = ResourceManagerTask, Keywords = Loader)]
        public void ResourceManagerStreamNotFound(String baseName, String mainAssemblyName, String cultureName, String loadedAssemblyName, String resourceFileName) {
            if (IsEnabled()) 
                WriteEvent(6, baseName, mainAssemblyName, cultureName, loadedAssemblyName, resourceFileName);
        } 
 
        [Event(7, Level = EventLevel.Informational, Task = ResourceManagerTask, Keywords = Loader)]
        public void ResourceManagerGetSatelliteAssemblySucceeded(String baseName, String mainAssemblyName, String cultureName, String assemblyName) { 
            if (IsEnabled())
                WriteEvent(7, baseName, mainAssemblyName, cultureName, assemblyName);
        }
 
        [Event(8, Level = EventLevel.Warning, Task = ResourceManagerTask, Keywords = Loader)]
        public void ResourceManagerGetSatelliteAssemblyFailed(String baseName, String mainAssemblyName, String cultureName, String assemblyName) { 
            if (IsEnabled()) 
                WriteEvent(8, baseName, mainAssemblyName, cultureName, assemblyName);
        } 

        [Event(9, Level = EventLevel.Informational, Task = ResourceManagerTask, Keywords = Loader)]
        public void ResourceManagerCaseInsensitiveResourceStreamLookupSucceeded(String baseName, String mainAssemblyName, String assemblyName, String resourceFileName) {
            if (IsEnabled()) 
                WriteEvent(9, baseName, mainAssemblyName, assemblyName, resourceFileName);
        } 
 
        [Event(10, Level = EventLevel.Warning, Task = ResourceManagerTask, Keywords = Loader)]
        public void ResourceManagerCaseInsensitiveResourceStreamLookupFailed(String baseName, String mainAssemblyName, String assemblyName, String resourceFileName) { 
            if (IsEnabled())
                WriteEvent(10, baseName, mainAssemblyName, assemblyName, resourceFileName);
        }
 
        // Could not access the manifest resource the assembly
        [Event(11, Level = EventLevel.Error, Task = ResourceManagerTask, Keywords = Loader)] 
        public void ResourceManagerManifestResourceAccessDenied(String baseName, String mainAssemblyName, String assemblyName, String canonicalName) { 
            if (IsEnabled())
                WriteEvent(11, baseName, mainAssemblyName, assemblyName, canonicalName); 
        }

        // Neutral resources are sufficient for this culture. Skipping satellites
        [Event(12, Level = EventLevel.Informational, Task = ResourceManagerTask, Keywords = Loader)] 
        public void ResourceManagerNeutralResourcesSufficient(String baseName, String mainAssemblyName, String cultureName) {
            if (IsEnabled()) 
                WriteEvent(12, baseName, mainAssemblyName, cultureName); 
        }
 
        [Event(13, Level = EventLevel.Warning, Task = ResourceManagerTask, Keywords = Loader)]
        public void ResourceManagerNeutralResourceAttributeMissing(String mainAssemblyName) {
            if (IsEnabled())
                WriteEvent(13, mainAssemblyName); 
        }
 
        [Event(14, Level = EventLevel.Informational, Task = ResourceManagerTask, Keywords = Loader)] 
        public void ResourceManagerCreatingResourceSet(String baseName, String mainAssemblyName, String cultureName, String fileName) {
            if (IsEnabled()) 
                WriteEvent(14, baseName, mainAssemblyName, cultureName, fileName);
        }

        [Event(15, Level = EventLevel.Informational, Task = ResourceManagerTask, Keywords = Loader)] 
        public void ResourceManagerNotCreatingResourceSet(String baseName, String mainAssemblyName, String cultureName) {
            if (IsEnabled()) 
                WriteEvent(15, baseName, mainAssemblyName, cultureName); 
        }
 
        [Event(16, Level = EventLevel.Warning, Task = ResourceManagerTask, Keywords = Loader)]
        public void ResourceManagerLookupFailed(String baseName, String mainAssemblyName, String cultureName) {
            if (IsEnabled())
                WriteEvent(16, baseName, mainAssemblyName, cultureName); 
        }
 
        [Event(17, Level = EventLevel.Informational, Task = ResourceManagerTask, Keywords = Loader)] 
        public void ResourceManagerReleasingResources(String baseName, String mainAssemblyName) {
            if (IsEnabled()) 
                WriteEvent(17, baseName, mainAssemblyName);
        }

        [Event(18, Level = EventLevel.Warning, Task = ResourceManagerTask, Keywords = Loader)] 
        public void ResourceManagerNeutralResourcesNotFound(String baseName, String mainAssemblyName, String resName) {
            if (IsEnabled()) 
                WriteEvent(18, baseName, mainAssemblyName, resName); 
        }
 
        [Event(19, Level = EventLevel.Informational, Task = ResourceManagerTask, Keywords = Loader)]
        public void ResourceManagerNeutralResourcesFound(String baseName, String mainAssemblyName, String resName) {
            if (IsEnabled())
                WriteEvent(19, baseName, mainAssemblyName, resName); 
        }
 
        [Event(20, Level = EventLevel.Informational, Task = ResourceManagerTask, Keywords = Loader)] 
        public void ResourceManagerAddingCultureFromConfigFile(String baseName, String mainAssemblyName, String cultureName) {
            if (IsEnabled()) 
                WriteEvent(20, baseName, mainAssemblyName, cultureName);
        }

        [Event(21, Level = EventLevel.Informational, Task = ResourceManagerTask, Keywords = Loader)] 
        public void ResourceManagerCultureNotFoundInConfigFile(String baseName, String mainAssemblyName, String cultureName) {
            if (IsEnabled()) 
                WriteEvent(21, baseName, mainAssemblyName, cultureName); 
        }
 
        [Event(22, Level = EventLevel.Informational, Task = ResourceManagerTask, Keywords = Loader)]
        public void ResourceManagerCultureFoundInConfigFile(String baseName, String mainAssemblyName, String cultureName) {
            if (IsEnabled())
                WriteEvent(22, baseName, mainAssemblyName, cultureName); 
        }
 
        // ResourceManager Event Wrappers 

        [NonEvent] 
        public void ResourceManagerLookupStarted(String baseName, Assembly mainAssembly, String cultureName) {
            if (IsEnabled())
                ResourceManagerLookupStarted(baseName, GetName(mainAssembly), cultureName);
        } 

        [NonEvent] 
        public void ResourceManagerLookingForResourceSet(String baseName, Assembly mainAssembly, String cultureName) { 
            if (IsEnabled())
                ResourceManagerLookingForResourceSet(baseName, GetName(mainAssembly), cultureName); 
        }

        [NonEvent]
        public void ResourceManagerFoundResourceSetInCache(String baseName, Assembly mainAssembly, String cultureName) { 
            if (IsEnabled())
                ResourceManagerFoundResourceSetInCache(baseName, GetName(mainAssembly), cultureName); 
        } 

        [NonEvent] 
        public void ResourceManagerFoundResourceSetInCacheUnexpected(String baseName, Assembly mainAssembly, String cultureName) {
            if (IsEnabled())
                ResourceManagerFoundResourceSetInCacheUnexpected(baseName, GetName(mainAssembly), cultureName);
        } 

        [NonEvent] 
        public void ResourceManagerStreamFound(String baseName, Assembly mainAssembly, String cultureName, Assembly loadedAssembly, String resourceFileName) { 
            if (IsEnabled())
                ResourceManagerStreamFound(baseName, GetName(mainAssembly), cultureName, GetName(loadedAssembly), resourceFileName); 
        }

        [NonEvent]
        public void ResourceManagerStreamNotFound(String baseName, Assembly mainAssembly, String cultureName, Assembly loadedAssembly, String resourceFileName) { 
            if (IsEnabled())
                ResourceManagerStreamNotFound(baseName, GetName(mainAssembly), cultureName, GetName(loadedAssembly), resourceFileName); 
        } 

        [NonEvent] 
        public void ResourceManagerGetSatelliteAssemblySucceeded(String baseName, Assembly mainAssembly, String cultureName, String assemblyName) {
            if (IsEnabled())
                ResourceManagerGetSatelliteAssemblySucceeded(baseName, GetName(mainAssembly), cultureName, assemblyName);
        } 

        [NonEvent] 
        public void ResourceManagerGetSatelliteAssemblyFailed(String baseName, Assembly mainAssembly, String cultureName, String assemblyName) { 
            if (IsEnabled())
                ResourceManagerGetSatelliteAssemblyFailed(baseName, GetName(mainAssembly), cultureName, assemblyName); 
        }

        [NonEvent]
        public void ResourceManagerCaseInsensitiveResourceStreamLookupSucceeded(String baseName, Assembly mainAssembly, String assemblyName, String resourceFileName) { 
            if (IsEnabled())
                ResourceManagerCaseInsensitiveResourceStreamLookupSucceeded(baseName, GetName(mainAssembly), assemblyName, resourceFileName); 
        } 

        [NonEvent] 
        public void ResourceManagerCaseInsensitiveResourceStreamLookupFailed(String baseName, Assembly mainAssembly, String assemblyName, String resourceFileName) {
            if (IsEnabled())
                ResourceManagerCaseInsensitiveResourceStreamLookupFailed(baseName, GetName(mainAssembly), assemblyName, resourceFileName);
        } 

        [NonEvent] 
        public void ResourceManagerManifestResourceAccessDenied(String baseName, Assembly mainAssembly, String assemblyName, String canonicalName) { 
            if (IsEnabled())
                ResourceManagerManifestResourceAccessDenied(baseName, GetName(mainAssembly), assemblyName, canonicalName); 
        }

        [NonEvent]
        public void ResourceManagerNeutralResourcesSufficient(String baseName, Assembly mainAssembly, String cultureName) { 
            if (IsEnabled())
                ResourceManagerNeutralResourcesSufficient(baseName, GetName(mainAssembly), cultureName); 
        } 

        [NonEvent] 
        public void ResourceManagerNeutralResourceAttributeMissing(Assembly mainAssembly) {
            if (IsEnabled())
                ResourceManagerNeutralResourceAttributeMissing(GetName(mainAssembly));
        } 

        [NonEvent] 
        public void ResourceManagerCreatingResourceSet(String baseName, Assembly mainAssembly, String cultureName, String fileName) { 
            if (IsEnabled())
                ResourceManagerCreatingResourceSet(baseName, GetName(mainAssembly), cultureName, fileName); 
        }

        [NonEvent]
        public void ResourceManagerNotCreatingResourceSet(String baseName, Assembly mainAssembly, String cultureName) { 
            if (IsEnabled())
                ResourceManagerNotCreatingResourceSet(baseName, GetName(mainAssembly), cultureName); 
        } 

        [NonEvent] 
        public void ResourceManagerLookupFailed(String baseName, Assembly mainAssembly, String cultureName) {
            if (IsEnabled())
                ResourceManagerLookupFailed(baseName, GetName(mainAssembly), cultureName);
        } 

        [NonEvent] 
        public void ResourceManagerReleasingResources(String baseName, Assembly mainAssembly) { 
            if (IsEnabled())
                ResourceManagerReleasingResources(baseName, GetName(mainAssembly)); 
        }

        [NonEvent]
        public void ResourceManagerNeutralResourcesNotFound(String baseName, Assembly mainAssembly, String resName) { 
            if (IsEnabled())
                ResourceManagerNeutralResourcesNotFound(baseName, GetName(mainAssembly), resName); 
        } 

        [NonEvent] 
        public void ResourceManagerNeutralResourcesFound(String baseName, Assembly mainAssembly, String resName) {
            if (IsEnabled())
                ResourceManagerNeutralResourcesFound(baseName, GetName(mainAssembly), resName);
        } 

        [NonEvent] 
        public void ResourceManagerAddingCultureFromConfigFile(String baseName, Assembly mainAssembly, String cultureName) { 
            if (IsEnabled())
                ResourceManagerAddingCultureFromConfigFile(baseName, GetName(mainAssembly), cultureName); 
        }

        [NonEvent]
        public void ResourceManagerCultureNotFoundInConfigFile(String baseName, Assembly mainAssembly, String cultureName) { 
            if (IsEnabled())
                ResourceManagerCultureNotFoundInConfigFile(baseName, GetName(mainAssembly), cultureName); 
        } 

        [NonEvent] 
        public void ResourceManagerCultureFoundInConfigFile(String baseName, Assembly mainAssembly, String cultureName) {
            if (IsEnabled())
                ResourceManagerCultureFoundInConfigFile(baseName, GetName(mainAssembly), cultureName);
        } 

        private static string GetName(Assembly assembly) { 
            if (assembly == null) 
                return "<<NULL>>";
            else 
                return assembly.FullName;
        }

    } 

} 
 

