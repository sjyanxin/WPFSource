// ==++== 
//
//   Copyright (c) Microsoft Corporation.  All rights reserved.
//
// ==--== 
/*==============================================================================
** 
** Class: Assembly 
**
** <OWNER>[....]</OWNER> 
** <OWNER>[....]</OWNER>
**
**
** Purpose: For Assembly-related stuff. 
**
** 
=============================================================================*/ 

namespace System.Reflection 
{
    using System;
    using System.Collections;
    using System.Collections.Generic; 
    using CultureInfo = System.Globalization.CultureInfo;
    using System.Security; 
    using System.Security.Policy; 
    using System.Security.Permissions;
    using System.IO; 
    using System.Reflection.Cache;
    using StringBuilder = System.Text.StringBuilder;
    using System.Configuration.Assemblies;
    using StackCrawlMark = System.Threading.StackCrawlMark; 
    using System.Runtime.InteropServices;
#if FEATURE_SERIALIZATION 
    using BinaryFormatter = System.Runtime.Serialization.Formatters.Binary.BinaryFormatter; 
#endif // FEATURE_SERIALIZATION
    using System.Runtime.CompilerServices; 
    using SecurityZone = System.Security.SecurityZone;
    using IEvidenceFactory = System.Security.IEvidenceFactory;
    using System.Runtime.Serialization;
    using Microsoft.Win32; 
    using System.Threading;
    using __HResults = System.__HResults; 
    using System.Runtime.Versioning; 
    using System.Diagnostics.Contracts;
 

    [Serializable]
[System.Runtime.InteropServices.ComVisible(true)]
    public delegate Module ModuleResolveEventHandler(Object sender, ResolveEventArgs e); 

 
    [Serializable] 
    [ClassInterface(ClassInterfaceType.None)]
    [ComDefaultInterface(typeof(_Assembly))] 
    [System.Runtime.InteropServices.ComVisible(true)]
    [PermissionSetAttribute(SecurityAction.InheritanceDemand, Unrestricted = true)]
    public abstract class Assembly : _Assembly, IEvidenceFactory, ICustomAttributeProvider, ISerializable
    { 
        #region constructors
        protected Assembly() {} 
        #endregion 

        #region public static methods 

        [ResourceExposure(ResourceScope.None)]
        public static String CreateQualifiedName(String assemblyName, String typeName)
        { 
            return typeName + ", " + assemblyName;
        } 
 
        public static Assembly GetAssembly(Type type)
        { 
            if (type == null)
                throw new ArgumentNullException("type");
            Contract.EndContractBlock();
 
            Module m = type.Module;
            if (m == null) 
                return null; 
            else
                return m.Assembly; 
        }

#if !FEATURE_CORECLR
        public static bool operator ==(Assembly left, Assembly right) 
        {
            if (ReferenceEquals(left, right)) 
                return true; 

            if ((object)left == null || (object)right == null || 
                left is RuntimeAssembly || right is RuntimeAssembly)
            {
                return false;
            } 
            return left.Equals(right);
        } 
 
        public static bool operator !=(Assembly left, Assembly right)
        { 
            return !(left == right);
        }

        public override bool Equals(object o) 
        {
            return base.Equals(o); 
        } 

        public override int GetHashCode() 
        {
            return base.GetHashCode();
        }
#endif // !FEATURE_CORECLR 

        // Locate an assembly by the name of the file containing the manifest. 
        [System.Security.SecuritySafeCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)] 
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable
        public static Assembly LoadFrom(String assemblyFile)
        {
            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller; 

            return RuntimeAssembly.InternalLoadFrom( 
                assemblyFile, 
                null, // securityEvidence
                null, // hashValue 
                AssemblyHashAlgorithm.None,
                false,// forIntrospection
                false,// suppressSecurityChecks
                ref stackMark); 
        }
 
        // Locate an assembly for reflection by the name of the file containing the manifest. 
        [System.Security.SecuritySafeCritical]  // auto-generated
        [ResourceExposure(ResourceScope.Machine)] 
        [ResourceConsumption(ResourceScope.Machine)]
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable
        public static Assembly ReflectionOnlyLoadFrom(String assemblyFile)
        { 
            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller;
 
            return RuntimeAssembly.InternalLoadFrom( 
                assemblyFile,
                null, //securityEvidence 
                null, //hashValue
                AssemblyHashAlgorithm.None,
                true,  //forIntrospection
                false, //suppressSecurityChecks 
                ref stackMark);
        } 
 
        // Evidence is protected in Assembly.Load()
        [System.Security.SecuritySafeCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        [Obsolete("This method is obsolete and will be removed in a future release of the .NET Framework. Please use an overload of LoadFrom which does not take an Evidence parameter. See http://go.microsoft.com/fwlink/?LinkID=155570 for more information.")]
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable 
        public static Assembly LoadFrom(String assemblyFile,
                                        Evidence securityEvidence) 
        { 
            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller;
 
            return RuntimeAssembly.InternalLoadFrom(
                assemblyFile,
                securityEvidence,
                null, // hashValue 
                AssemblyHashAlgorithm.None,
                false,// forIntrospection); 
                false,// suppressSecurityChecks 
                ref stackMark);
        } 

        // Evidence is protected in Assembly.Load()
        [System.Security.SecuritySafeCritical]  // auto-generated
        [ResourceExposure(ResourceScope.Machine)] 
        [ResourceConsumption(ResourceScope.Machine)]
        [Obsolete("This method is obsolete and will be removed in a future release of the .NET Framework. Please use an overload of LoadFrom which does not take an Evidence parameter. See http://go.microsoft.com/fwlink/?LinkID=155570 for more information.")] 
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable 
        public static Assembly LoadFrom(String assemblyFile,
                                        Evidence securityEvidence, 
                                        byte[] hashValue,
                                        AssemblyHashAlgorithm hashAlgorithm)
        {
            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller; 

            return RuntimeAssembly.InternalLoadFrom( 
                assemblyFile, 
                securityEvidence,
                hashValue, 
                hashAlgorithm,
                false,
                false,
                ref stackMark); 
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)] 
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable
        public static Assembly LoadFrom(String assemblyFile,
                                        byte[] hashValue,
                                        AssemblyHashAlgorithm hashAlgorithm) 
        {
            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller; 
 
            return RuntimeAssembly.InternalLoadFrom(
                assemblyFile, 
                null,
                hashValue,
                hashAlgorithm,
                false, 
                false,
                ref stackMark); 
        } 

#if FEATURE_CAS_POLICY 
        // Load an assembly into the LoadFrom context bypassing some security checks
        [SecurityCritical]
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)] 
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable
        public static Assembly UnsafeLoadFrom(string assemblyFile) 
        { 
            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller;
 
            return RuntimeAssembly.InternalLoadFrom(assemblyFile,
                                                    null, // securityEvidence
                                                    null, // hashValue
                                                    AssemblyHashAlgorithm.None, 
                                                    false, // forIntrospection
                                                    true, // suppressSecurityChecks 
                                                    ref stackMark); 
        }
#endif // FEATURE_CAS_POLICY 

        // Locate an assembly by the long form of the assembly name.
        // eg. "Toolbox.dll, version=1.1.10.1220, locale=en, publickey=1234567890123456789012345678901234567890"
        [System.Security.SecuritySafeCritical]  // auto-generated 
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable
        public static Assembly Load(String assemblyString) 
        { 
            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller;
            return RuntimeAssembly.InternalLoad(assemblyString, null, ref stackMark, false /*forIntrospection*/); 
        }

        // Locate an assembly for reflection by the long form of the assembly name.
        // eg. "Toolbox.dll, version=1.1.10.1220, locale=en, publickey=1234567890123456789012345678901234567890" 
        //
        [System.Security.SecuritySafeCritical]  // auto-generated 
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable 
        public static Assembly ReflectionOnlyLoad(String assemblyString)
        { 
            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller;
            return RuntimeAssembly.InternalLoad(assemblyString, null, ref stackMark, true /*forIntrospection*/);
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable 
        [Obsolete("This method is obsolete and will be removed in a future release of the .NET Framework. Please use an overload of Load which does not take an Evidence parameter. See http://go.microsoft.com/fwlink/?LinkID=155570 for more information.")] 
        public static Assembly Load(String assemblyString, Evidence assemblySecurity)
        { 
            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller;
            return RuntimeAssembly.InternalLoad(assemblyString, assemblySecurity, ref stackMark, false /*forIntrospection*/);
        }
 
        // Locate an assembly by its name. The name can be strong or
        // weak. The assembly is loaded into the domain of the caller. 
        [System.Security.SecuritySafeCritical]  // auto-generated 
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable
        public static Assembly Load(AssemblyName assemblyRef) 
        {
            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller;
            return RuntimeAssembly.InternalLoadAssemblyName(assemblyRef, null, ref stackMark, false /*forIntrospection*/, false /*suppressSecurityChecks*/);
        } 

        [System.Security.SecuritySafeCritical]  // auto-generated 
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable 
        [Obsolete("This method is obsolete and will be removed in a future release of the .NET Framework. Please use an overload of Load which does not take an Evidence parameter. See http://go.microsoft.com/fwlink/?LinkID=155570 for more information.")]
        public static Assembly Load(AssemblyName assemblyRef, Evidence assemblySecurity) 
        {
            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller;
            return RuntimeAssembly.InternalLoadAssemblyName(assemblyRef, assemblySecurity, ref stackMark, false /*forIntrospection*/, false /*suppressSecurityChecks*/);
        } 

#if FEATURE_FUSION 
        [System.Security.SecuritySafeCritical]  // auto-generated 
        [Obsolete("This method has been deprecated. Please use Assembly.Load() instead. http://go.microsoft.com/fwlink/?linkid=14202")]
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable 
        public static Assembly LoadWithPartialName(String partialName)
        {
            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller;
            return RuntimeAssembly.LoadWithPartialNameInternal(partialName, null, ref stackMark); 
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated 
        [Obsolete("This method has been deprecated. Please use Assembly.Load() instead. http://go.microsoft.com/fwlink/?linkid=14202")]
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable 
        public static Assembly LoadWithPartialName(String partialName, Evidence securityEvidence)
        {
            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller;
            return RuntimeAssembly.LoadWithPartialNameInternal(partialName, securityEvidence, ref stackMark); 
        }
#endif // FEATURE_FUSION 
 
        // Loads the assembly with a COFF based IMAGE containing
        // an emitted assembly. The assembly is loaded into the domain 
        // of the caller.
        [System.Security.SecuritySafeCritical]  // auto-generated
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable
        public static Assembly Load(byte[] rawAssembly) 
        {
            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller; 
            return RuntimeAssembly.nLoadImage( 
                rawAssembly,
                null, // symbol store 
                null, // evidence
                ref stackMark,
                false,  // fIntrospection
                SecurityContextSource.CurrentAssembly); 
        }
 
        // Loads the assembly for reflection with a COFF based IMAGE containing 
        // an emitted assembly. The assembly is loaded into the domain
        // of the caller. 
        [System.Security.SecuritySafeCritical]  // auto-generated
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable
        public static Assembly ReflectionOnlyLoad(byte[] rawAssembly)
        { 
            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller;
            return RuntimeAssembly.nLoadImage( 
                rawAssembly, 
                null, // symbol store
                null, // evidence 
                ref stackMark,
                true,  // fIntrospection
                SecurityContextSource.CurrentAssembly);
        } 

        // Loads the assembly with a COFF based IMAGE containing 
        // an emitted assembly. The assembly is loaded into the domain 
        // of the caller. The second parameter is the raw bytes
        // representing the symbol store that matches the assembly. 
        [System.Security.SecuritySafeCritical]  // auto-generated
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable
        public static Assembly Load(byte[] rawAssembly,
                                    byte[] rawSymbolStore) 
        {
            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller; 
            return RuntimeAssembly.nLoadImage( 
                rawAssembly,
                rawSymbolStore, 
                null, // evidence
                ref stackMark,
                false,  // fIntrospection
                SecurityContextSource.CurrentAssembly); 
        }
 
        // Load an assembly from a byte array, controlling where the grant set of this assembly is 
        // propigated from.
        [SecuritySafeCritical] 
        [MethodImpl(MethodImplOptions.NoInlining)]  // Due to the stack crawl mark
        public static Assembly Load(byte[] rawAssembly,
                                    byte[] rawSymbolStore,
                                    SecurityContextSource securityContextSource) 
        {
            if (securityContextSource < SecurityContextSource.CurrentAppDomain || 
                securityContextSource > SecurityContextSource.CurrentAssembly) 
            {
                throw new ArgumentOutOfRangeException("securityContextSource"); 
            }

            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller;
            return RuntimeAssembly.nLoadImage(rawAssembly, 
                                              rawSymbolStore,
                                              null,             // evidence 
                                              ref stackMark, 
                                              false,            // fIntrospection
                                              securityContextSource); 
        }

        [System.Security.SecuritySafeCritical]  // auto-generated
        [SecurityPermissionAttribute(SecurityAction.Demand, Flags=SecurityPermissionFlag.ControlEvidence)] 
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable
        [Obsolete("This method is obsolete and will be removed in a future release of the .NET Framework. Please use an overload of Load which does not take an Evidence parameter. See http://go.microsoft.com/fwlink/?LinkID=155570 for more information.")] 
        public static Assembly Load(byte[] rawAssembly, 
                                    byte[] rawSymbolStore,
                                    Evidence securityEvidence) 
        {
#if FEATURE_CAS_POLICY
            if (securityEvidence != null && !AppDomain.CurrentDomain.IsLegacyCasPolicyEnabled)
            { 
                // A zone of MyComputer could not have been used to sandbox, so for compatibility we do not
                // throw an exception when we see it. 
                Zone zone = securityEvidence.GetHostEvidence<Zone>(); 
                if (zone == null || zone.SecurityZone != SecurityZone.MyComputer)
                { 
                    throw new NotSupportedException(Environment.GetResourceString("NotSupported_RequiresCasPolicyImplicit"));
                }
            }
#endif // FEATURE_CAS_POLICY 

            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller; 
            return RuntimeAssembly.nLoadImage( 
                rawAssembly,
                rawSymbolStore, 
                securityEvidence,
                ref stackMark,
                false,  // fIntrospection
                SecurityContextSource.CurrentAssembly); 
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)] 
        public static Assembly LoadFile(String path)
        {
            new FileIOPermission(FileIOPermissionAccess.PathDiscovery | FileIOPermissionAccess.Read, path).Demand();
            return RuntimeAssembly.nLoadFile(path, null); 
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)] 
        [SecurityPermissionAttribute(SecurityAction.Demand, Flags=SecurityPermissionFlag.ControlEvidence)]
        [Obsolete("This method is obsolete and will be removed in a future release of the .NET Framework. Please use an overload of LoadFile which does not take an Evidence parameter. See http://go.microsoft.com/fwlink/?LinkID=155570 for more information.")]
        public static Assembly LoadFile(String path,
                                        Evidence securityEvidence) 
        {
#if FEATURE_CAS_POLICY 
            if (securityEvidence != null && !AppDomain.CurrentDomain.IsLegacyCasPolicyEnabled) 
            {
                throw new NotSupportedException(Environment.GetResourceString("NotSupported_RequiresCasPolicyImplicit")); 
            }
#endif // FEATURE_CAS_POLICY

            new FileIOPermission(FileIOPermissionAccess.PathDiscovery | FileIOPermissionAccess.Read, path).Demand(); 
            return RuntimeAssembly.nLoadFile(path, securityEvidence);
        } 
 
#if FEATURE_CORECLR
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable 
        public static Assembly Load(Stream assemblyStream, Stream pdbStream)
        {
            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller;
            return RuntimeAssembly.InternalLoadFromStream(assemblyStream, pdbStream, ref stackMark); 
        }
 
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable 
        public static Assembly Load(Stream assemblyStream)
        { 
            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller;
            return RuntimeAssembly.InternalLoadFromStream(assemblyStream, null, ref stackMark);
        }
#endif //FEATURE_CORECLR 

        /* 
         * Get the assembly that the current code is running from. 
         */
        [System.Security.SecuritySafeCritical]  // auto-generated 
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable
        public static Assembly GetExecutingAssembly()
        {
            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller; 
            return RuntimeAssembly.GetExecutingAssembly(ref stackMark);
        } 
 
        [System.Security.SecuritySafeCritical]  // auto-generated
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable 
        public static Assembly GetCallingAssembly()
        {
            // LookForMyCallersCaller is not guarantee to return the correct stack frame
            // because of inlining, tail calls, etc. As a result GetCallingAssembly is not 
            // ganranteed to return the correct result. We should document it as such.
            StackCrawlMark stackMark = StackCrawlMark.LookForMyCallersCaller; 
            return RuntimeAssembly.GetExecutingAssembly(ref stackMark); 
        }
 
#if !FEATURE_CORECLR
        [System.Security.SecuritySafeCritical]  // auto-generated
        public static Assembly GetEntryAssembly() {
            AppDomainManager domainManager = AppDomain.CurrentDomain.DomainManager; 
            if (domainManager == null)
                domainManager = new AppDomainManager(); 
            return domainManager.EntryAssembly; 
        }
#endif // !FEATURE_CORECLR 

        #endregion // public static methods

        #region public methods 
        public virtual event ModuleResolveEventHandler ModuleResolve
        { 
            [System.Security.SecurityCritical]  // auto-generated_required 
            add
            { 
                throw new NotImplementedException();
            }
            [System.Security.SecurityCritical]  // auto-generated_required
            remove 
            {
                throw new NotImplementedException(); 
            } 
        }
 
        public virtual String CodeBase
        {
            [ResourceExposure(ResourceScope.Machine)]
            [ResourceConsumption(ResourceScope.Machine)] 
            get
            { 
                throw new NotImplementedException(); 
            }
        } 

        public virtual String EscapedCodeBase
        {
            [System.Security.SecuritySafeCritical]  // auto-generated 
            [ResourceExposure(ResourceScope.Machine)]
            [ResourceConsumption(ResourceScope.Machine)] 
            get 
            {
                return AssemblyName.EscapeCodeBase(CodeBase); 
            }
        }

        public virtual AssemblyName GetName() 
        {
            return GetName(false); 
        } 

        public virtual AssemblyName GetName(bool copiedName) 
        {
            throw new NotImplementedException();
        }
 
#if FEATURE_APTCA
        // This method is called from the VM when creating conditional APTCA exceptions, in order to include 
        // the text which must be added to the partial trust visible assembly list 
        [SecurityCritical]
        [PermissionSet(SecurityAction.Assert, Unrestricted = true)] 
        private string GetNameForConditionalAptca()
        {
            AssemblyName assemblyName = GetName();
            return assemblyName.GetNameWithPublicKey(); 

        } 
#endif // FEATURE_APTCA 

        public virtual String FullName 
        {
            get
            {
                throw new NotImplementedException(); 
            }
        } 
 
        public virtual MethodInfo EntryPoint
        { 
            get
            {
                throw new NotImplementedException();
            } 
        }
 
        Type _Assembly.GetType() 
        {
            return base.GetType(); 
        }

        public virtual Type GetType(String name)
        { 
            return GetType(name, false, false);
        } 
 
        public virtual Type GetType(String name, bool throwOnError)
        { 
            return GetType(name, throwOnError, false);
        }

        public virtual Type GetType(String name, bool throwOnError, bool ignoreCase) 
        {
            throw new NotImplementedException(); 
        } 

        public virtual Type[] GetExportedTypes() 
        {
            throw new NotImplementedException();
        }
 
        [ResourceExposure(ResourceScope.None)]
        [ResourceConsumption(ResourceScope.Machine | ResourceScope.Assembly, ResourceScope.Machine | ResourceScope.Assembly)] 
        public virtual Type[] GetTypes() 
        {
            Module[] m = GetModules(false); 

            int iNumModules = m.Length;
            int iFinalLength = 0;
            Type[][] ModuleTypes = new Type[iNumModules][]; 

            for (int i = 0; i < iNumModules; i++) 
            { 
                ModuleTypes[i] = m[i].GetTypes();
                iFinalLength += ModuleTypes[i].Length; 
            }

            int iCurrent = 0;
            Type[] ret = new Type[iFinalLength]; 
            for (int i = 0; i < iNumModules; i++)
            { 
                int iLength = ModuleTypes[i].Length; 
                Array.Copy(ModuleTypes[i], 0, ret, iCurrent, iLength);
                iCurrent += iLength; 
            }

            return ret;
        } 

        // Load a resource based on the NameSpace of the type. 
        public virtual Stream GetManifestResourceStream(Type type, String name) 
        {
            throw new NotImplementedException(); 
        }

        public virtual Stream GetManifestResourceStream(String name)
        { 
            throw new NotImplementedException();
        } 
 
        public virtual Assembly GetSatelliteAssembly(CultureInfo culture)
        { 
            throw new NotImplementedException();
        }

        // Useful for binding to a very specific version of a satellite assembly 
        public virtual Assembly GetSatelliteAssembly(CultureInfo culture, Version version)
        { 
            throw new NotImplementedException(); 
        }
 
#if FEATURE_CAS_POLICY
        public virtual Evidence Evidence
        {
            get 
            {
                throw new NotImplementedException(); 
            } 
        }
 
        public virtual PermissionSet PermissionSet
        {
            // SecurityCritical because permissions can contain sensitive information such as paths
            [SecurityCritical] 
            get
            { 
                throw new NotImplementedException(); 
            }
        } 

        public bool IsFullyTrusted
        {
            [SecuritySafeCritical] 
            get
            { 
                return PermissionSet.IsUnrestricted(); 
            }
        } 

        public virtual SecurityRuleSet SecurityRuleSet
        {
            get 
            {
                throw new NotImplementedException(); 
            } 
        }
 
#endif // FEATURE_CAS_POLICY

        // ISerializable implementation
        [System.Security.SecurityCritical]  // auto-generated_required 
        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        { 
            throw new NotImplementedException(); 
        }
 
        [ComVisible(false)]
        public virtual Module ManifestModule
        {
            get 
            {
                // This API was made virtual in V4. Code compiled against V2 might use 
                // "call" rather than "callvirt" to call it. 
                // This makes sure those code still works.
                RuntimeAssembly rtAssembly = this as RuntimeAssembly; 
                if (rtAssembly != null)
                    return rtAssembly.ManifestModule;

                throw new NotImplementedException(); 
            }
        } 
 
        public virtual Object[] GetCustomAttributes(bool inherit)
        { 
            Contract.Ensures(Contract.Result<Object[]>() != null);
            throw new NotImplementedException();
        }
 
        public virtual Object[] GetCustomAttributes(Type attributeType, bool inherit)
        { 
            Contract.Ensures(Contract.Result<Object[]>() != null); 
            throw new NotImplementedException();
        } 

        public virtual bool IsDefined(Type attributeType, bool inherit)
        {
            throw new NotImplementedException(); 
        }
 
        public virtual IList<CustomAttributeData> GetCustomAttributesData() 
        {
            throw new NotImplementedException(); 
        }

        // To not break compatibility with the V1 _Assembly interface we need to make this
        // new member ComVisible(false). 
        [ComVisible(false)]
        public virtual bool ReflectionOnly 
        { 
            get
            { 
                throw new NotImplementedException();
            }
        }
 
#if FEATURE_MULTIMODULE_ASSEMBLIES
 
        [ResourceExposure(ResourceScope.Machine)] 
        [ResourceConsumption(ResourceScope.Machine)]
        public Module LoadModule(String moduleName, 
                                 byte[] rawModule)
        {
            return LoadModule(moduleName, rawModule, null);
        } 

        [ResourceExposure(ResourceScope.Machine)] 
        [ResourceConsumption(ResourceScope.Machine)] 
        public virtual Module LoadModule(String moduleName,
                                 byte[] rawModule, 
                                 byte[] rawSymbolStore)
        {
            throw new NotImplementedException();
        } 
#endif //FEATURE_MULTIMODULE_ASSEMBLIES
 
        // 
        // Locates a type from this assembly and creates an instance of it using
        // the system activator. 
        //
        public Object CreateInstance(String typeName)
        {
            return CreateInstance(typeName, 
                                  false, // ignore case
                                  BindingFlags.Public | BindingFlags.Instance, 
                                  null, // binder 
                                  null, // args
                                  null, // culture 
                                  null); // activation attributes
        }

        public Object CreateInstance(String typeName, 
                                     bool ignoreCase)
        { 
            return CreateInstance(typeName, 
                                  ignoreCase,
                                  BindingFlags.Public | BindingFlags.Instance, 
                                  null, // binder
                                  null, // args
                                  null, // culture
                                  null); // activation attributes 
        }
 
        public virtual Object CreateInstance(String typeName, 
                                     bool ignoreCase,
                                     BindingFlags bindingAttr, 
                                     Binder binder,
                                     Object[] args,
                                     CultureInfo culture,
                                     Object[] activationAttributes) 
        {
            Type t = GetType(typeName, false, ignoreCase); 
            if (t == null) return null; 
            return Activator.CreateInstance(t,
                                            bindingAttr, 
                                            binder,
                                            args,
                                            culture,
                                            activationAttributes); 
        }
 
        [ResourceExposure(ResourceScope.Machine | ResourceScope.Assembly)] 
        [ResourceConsumption(ResourceScope.Machine | ResourceScope.Assembly)]
        public Module[] GetLoadedModules() 
        {
            return GetLoadedModules(false);
        }
 
        [ResourceExposure(ResourceScope.Machine | ResourceScope.Assembly)]
        [ResourceConsumption(ResourceScope.Machine | ResourceScope.Assembly)] 
        public virtual Module[] GetLoadedModules(bool getResourceModules) 
        {
            throw new NotImplementedException(); 
        }

        [ResourceExposure(ResourceScope.Machine | ResourceScope.Assembly)]
        [ResourceConsumption(ResourceScope.Machine | ResourceScope.Assembly)] 
        public Module[] GetModules()
        { 
            return GetModules(false); 
        }
 
        [ResourceExposure(ResourceScope.Machine | ResourceScope.Assembly)]
        [ResourceConsumption(ResourceScope.Machine | ResourceScope.Assembly)]
        public virtual Module[] GetModules(bool getResourceModules)
        { 
            throw new NotImplementedException();
        } 
 
        public virtual Module GetModule(String name)
        { 
            throw new NotImplementedException();
        }

        // Returns the file in the File table of the manifest that matches the 
        // given name.  (Name should not include path.)
        [ResourceExposure(ResourceScope.Machine | ResourceScope.Assembly)] 
        [ResourceConsumption(ResourceScope.Machine | ResourceScope.Assembly)] 
        public virtual FileStream GetFile(String name)
        { 
            throw new NotImplementedException();
        }

        [ResourceExposure(ResourceScope.Machine | ResourceScope.Assembly)] 
        [ResourceConsumption(ResourceScope.Machine | ResourceScope.Assembly)]
        public virtual FileStream[] GetFiles() 
        { 
            return GetFiles(false);
        } 

        [ResourceExposure(ResourceScope.Machine | ResourceScope.Assembly)]
        [ResourceConsumption(ResourceScope.Machine | ResourceScope.Assembly)]
        public virtual FileStream[] GetFiles(bool getResourceModules) 
        {
            throw new NotImplementedException(); 
        } 

        // Returns the names of all the resources 
        public virtual String[] GetManifestResourceNames()
        {
            throw new NotImplementedException();
        } 

        public virtual AssemblyName[] GetReferencedAssemblies() 
        { 
            throw new NotImplementedException();
        } 

        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        public virtual ManifestResourceInfo GetManifestResourceInfo(String resourceName) 
        {
            throw new NotImplementedException(); 
        } 

        public override String ToString() 
        {
            String displayName = FullName;
            if (displayName == null)
                return base.ToString(); 
            else
                return displayName; 
        } 

        public virtual String Location 
        {
            [ResourceExposure(ResourceScope.Machine)]
            [ResourceConsumption(ResourceScope.Machine)]
            get 
            {
                throw new NotImplementedException(); 
            } 
        }
 
        // To not break compatibility with the V1 _Assembly interface we need to make this
        // new member ComVisible(false).
        [ComVisible(false)]
        public virtual String ImageRuntimeVersion 
        {
            get 
            { 
                throw new NotImplementedException();
            } 
        }

        /*
          Returns true if the assembly was loaded from the global assembly cache. 
        */
        public virtual bool GlobalAssemblyCache 
        { 
            get
            { 
                throw new NotImplementedException();
            }
        }
 
        [ComVisible(false)]
        public virtual Int64 HostContext 
        { 
            get
            { 
                // This API was made virtual in V4. Code compiled against V2 might use
                // "call" rather than "callvirt" to call it.
                // This makes sure those code still works.
                RuntimeAssembly rtAssembly = this as RuntimeAssembly; 
                if (rtAssembly != null)
                    return rtAssembly.HostContext; 
 
                throw new NotImplementedException();
            } 
        }

        public virtual bool IsDynamic
        { 
            get
            { 
                return false; 
            }
        } 
        #endregion // public methods

    }
 
    #if !FEATURE_CORECLR
    [System.Runtime.ForceTokenStabilization] 
    #endif //!FEATURE_CORECLR 
    [Serializable]
#if FEATURE_COMINTEROP 
    internal class RuntimeAssembly : Assembly, ICustomQueryInterface
    {
        #region ICustomQueryInterface
        [System.Security.SecurityCritical] 
        CustomQueryInterfaceResult ICustomQueryInterface.GetInterface([In]ref Guid iid, out IntPtr ppv)
        { 
            if (iid == typeof(NativeMethods.IDispatch).GUID) 
            {
                ppv = Marshal.GetComInterfaceForObject(this, typeof(_Assembly)); 
                return CustomQueryInterfaceResult.Handled;
            }

            ppv = IntPtr.Zero; 
            return CustomQueryInterfaceResult.NotHandled;
        } 
        #endregion 

#if false // hack for createBclSmall 
    }
#endif // hack for createBclSmall

#else 
    internal class RuntimeAssembly : Assembly
    { 
#endif // FEATURE_COMINTEROP 
        private const uint COR_E_LOADING_REFERENCE_ASSEMBLY = 0x80131058U;
 
        internal RuntimeAssembly() { throw new NotSupportedException(); }

        #region private data members
        [method: System.Security.SecurityCritical] 
        private event ModuleResolveEventHandler _ModuleResolve;
        private InternalCache m_cachedData; 
        private object m_syncRoot;   // Used to keep collectible types alive and as the syncroot for reflection.emit 
        #if !FEATURE_CORECLR
        [System.Runtime.ForceTokenStabilization] 
        #endif //!FEATURE_CORECLR
        private IntPtr m_assembly;    // slack for ptr datum on unmanaged side
        #endregion
 
        internal object SyncRoot
        { 
            get 
            {
                if (m_syncRoot == null) 
                {
                    Interlocked.CompareExchange<object>(ref m_syncRoot, new object(), null);
                }
                return m_syncRoot; 
            }
        } 
 
        public override event ModuleResolveEventHandler ModuleResolve
        { 
            [System.Security.SecurityCritical]  // auto-generated_required
            add
            {
                _ModuleResolve += value; 
            }
            [System.Security.SecurityCritical]  // auto-generated_required 
            remove 
            {
                _ModuleResolve -= value; 
            }
        }

        private const String s_localFilePrefix = "file:"; 

        [System.Security.SecurityCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.Machine)] 
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)]
 		[SuppressUnmanagedCodeSecurity] 
        private static extern void GetCodeBase(RuntimeAssembly assembly,
                                               bool copiedName,
                                               StringHandleOnStack retString);
 
        [System.Security.SecurityCritical]  // auto-generated
        internal String GetCodeBase(bool copiedName) 
        { 
            String codeBase = null;
            GetCodeBase(GetNativeHandle(), copiedName, JitHelpers.GetStringHandleOnStack(ref codeBase)); 
            return codeBase;
        }

        public override String CodeBase 
        {
            [System.Security.SecuritySafeCritical]  // auto-generated 
            [ResourceExposure(ResourceScope.Machine)] 
            [ResourceConsumption(ResourceScope.Machine)]
            get { 
                String codeBase = GetCodeBase(false);
                VerifyCodeBaseDiscovery(codeBase);
                return codeBase;
            } 
        }
 
        internal RuntimeAssembly GetNativeHandle() 
        {
            return this; 
        }

        // If the assembly is copied before it is loaded, the codebase will be set to the
        // actual file loaded if copiedName is true. If it is false, then the original code base 
        // is returned.
        [System.Security.SecuritySafeCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.None)] 
        [ResourceConsumption(ResourceScope.Machine, ResourceScope.Machine)]
        public override AssemblyName GetName(bool copiedName) 
        {
            AssemblyName an = new AssemblyName();

            String codeBase = GetCodeBase(copiedName); 
            VerifyCodeBaseDiscovery(codeBase);
 
            an.Init(GetSimpleName(), 
                    GetPublicKey(),
                    null, // public key token 
                    GetVersion(),
                    GetLocale(),
                    GetHashAlgorithm(),
                    AssemblyVersionCompatibility.SameMachine, 
                    codeBase,
                    GetFlags() | AssemblyNameFlags.PublicKey, 
                    null); // strong name key pair 

            PortableExecutableKinds pek; 
            ImageFileMachine ifm;

            Module manifestModule = ManifestModule;
            if (manifestModule != null) 
            {
                if (manifestModule.MDStreamVersion > 0x10000) 
                { 
                    ManifestModule.GetPEKind(out pek, out ifm);
                    an.SetProcArchIndex(pek,ifm); 
                }
            }
            return an;
        } 

        [System.Security.SecurityCritical]  // auto-generated 
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)] 
		[SuppressUnmanagedCodeSecurity]
        [ResourceExposure(ResourceScope.None)] 
        private extern static void GetFullName(RuntimeAssembly assembly, StringHandleOnStack retString);

        public override String FullName
        { 
            [System.Security.SecuritySafeCritical]  // auto-generated
            get { 
                // If called by Object.ToString(), return val may be NULL. 
                String s;
                if ((s = (String)Cache[CacheObjType.AssemblyName]) != null) 
                    return s;

                GetFullName(GetNativeHandle(), JitHelpers.GetStringHandleOnStack(ref s));
                if (s != null) 
                    Cache[CacheObjType.AssemblyName] = s;
 
                return s; 
            }
        } 

        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)]
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)] 
        [SuppressUnmanagedCodeSecurity]
        private static extern void GetEntryPoint(RuntimeAssembly assembly, ObjectHandleOnStack retMethod); 
 
        public override MethodInfo EntryPoint
        { 
            [System.Security.SecuritySafeCritical]  // auto-generated
            get {
                IRuntimeMethodInfo methodHandle = null;
                GetEntryPoint(GetNativeHandle(), JitHelpers.GetObjectHandleOnStack(ref methodHandle)); 

                if (methodHandle == null) 
                    return null; 

                    return (MethodInfo)RuntimeType.GetMethodBase(methodHandle); 
            }
        }

        [System.Security.SecurityCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.None)]
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)] 
		[SuppressUnmanagedCodeSecurity] 
        private static extern void GetType(RuntimeAssembly assembly,
                                                        String name, 
                                                        bool throwOnError,
                                                        bool ignoreCase,
                                                        ObjectHandleOnStack type);
 
        [System.Security.SecuritySafeCritical]
        public override Type GetType(String name, bool throwOnError, bool ignoreCase) 
        { 
            // throw on null strings regardless of the value of "throwOnError"
            if (name == null) 
                throw new ArgumentNullException("name");

            RuntimeType type = null;
            GetType(GetNativeHandle(), name, throwOnError, ignoreCase, JitHelpers.GetObjectHandleOnStack(ref type)); 
            return type;
        } 
 
        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)] 
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)]
        [SuppressUnmanagedCodeSecurity]
        internal extern static void GetForwardedTypes(RuntimeAssembly assembly, ObjectHandleOnStack retTypes);
 
        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)] 
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)] 
        [SuppressUnmanagedCodeSecurity]
        private extern static void GetExportedTypes(RuntimeAssembly assembly, ObjectHandleOnStack retTypes); 

        [System.Security.SecuritySafeCritical]  // auto-generated
        public override Type[] GetExportedTypes()
        { 
            Type[] types = null;
            GetExportedTypes(GetNativeHandle(), JitHelpers.GetObjectHandleOnStack(ref types)); 
            return types; 
        }
 
        // Load a resource based on the NameSpace of the type.
        [System.Security.SecuritySafeCritical]  // auto-generated
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable
        public override Stream GetManifestResourceStream(Type type, String name) 
        {
            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller; 
            return GetManifestResourceStream(type, name, false, ref stackMark); 
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable
        public override Stream GetManifestResourceStream(String name)
        { 
            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller;
            return GetManifestResourceStream(name, ref stackMark, false); 
        } 

#if FEATURE_CAS_POLICY 
        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)]
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)]
		[SuppressUnmanagedCodeSecurity] 
        private extern static void GetEvidence(RuntimeAssembly assembly, ObjectHandleOnStack retEvidence);
 
        [SecurityCritical] 
        [ResourceExposure(ResourceScope.None)]
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)] 
        [SuppressUnmanagedCodeSecurity]
        private extern static SecurityRuleSet GetSecurityRuleSet(RuntimeAssembly assembly);

        public override Evidence Evidence 
        {
            [SecuritySafeCritical] 
            [SecurityPermissionAttribute( SecurityAction.Demand, ControlEvidence = true )] 
            get
            { 
                Evidence evidence = EvidenceNoDemand;
                return evidence.Clone();
            }
        } 

        internal Evidence EvidenceNoDemand 
        { 
            [SecurityCritical]
            get 
            {
                Evidence evidence = null;
                GetEvidence(GetNativeHandle(), JitHelpers.GetObjectHandleOnStack(ref evidence));
                return evidence; 
            }
        } 
 
        public override PermissionSet PermissionSet
        { 
            [SecurityCritical]
            get
            {
                PermissionSet grantSet = null; 
                PermissionSet deniedSet = null;
 
                GetGrantSet(out grantSet, out deniedSet); 

                if (grantSet != null) 
                {
                    return grantSet.Copy();
                }
                else 
                {
                    return new PermissionSet(PermissionState.Unrestricted); 
                } 
            }
        } 

        public override SecurityRuleSet SecurityRuleSet
        {
            [SecuritySafeCritical] 
            get
            { 
                return GetSecurityRuleSet(GetNativeHandle()); 
            }
        } 
#endif // FEATURE_CAS_POLICY

        // ISerializable implementation
        [System.Security.SecurityCritical]  // auto-generated_required 
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        { 
            if (info==null) 
                throw new ArgumentNullException("info");
 
            Contract.EndContractBlock();

            UnitySerializationHolder.GetUnitySerializationInfo(info,
                                                               UnitySerializationHolder.AssemblyUnity, 
                                                               this.FullName,
                                                               this); 
        } 

        public override Module ManifestModule 
        {
            get
            {
                // We don't need to return the "external" ModuleBuilder because 
                // it is meant to be read-only
                return RuntimeAssembly.GetManifestModule(GetNativeHandle()); 
            } 
        }
 
        public override Object[] GetCustomAttributes(bool inherit)
        {
            return CustomAttribute.GetCustomAttributes(this, typeof(object) as RuntimeType);
        } 

        public override Object[] GetCustomAttributes(Type attributeType, bool inherit) 
        { 
            if (attributeType == null)
                throw new ArgumentNullException("attributeType"); 
            Contract.EndContractBlock();

            RuntimeType attributeRuntimeType = attributeType.UnderlyingSystemType as RuntimeType;
 
            if (attributeRuntimeType == null)
                throw new ArgumentException(Environment.GetResourceString("Arg_MustBeType"),"attributeType"); 
 
            return CustomAttribute.GetCustomAttributes(this, attributeRuntimeType);
        } 

        public override bool IsDefined(Type attributeType, bool inherit)
        {
            if (attributeType == null) 
                throw new ArgumentNullException("attributeType");
            Contract.EndContractBlock(); 
 
            RuntimeType attributeRuntimeType = attributeType.UnderlyingSystemType as RuntimeType;
 
            if (attributeRuntimeType == null)
                throw new ArgumentException(Environment.GetResourceString("Arg_MustBeType"),"caType");

            return CustomAttribute.IsDefined(this, attributeRuntimeType); 
        }
 
        public override IList<CustomAttributeData> GetCustomAttributesData() 
        {
            return CustomAttributeData.GetCustomAttributesInternal(this); 
        }

        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.Machine)] 
        [ResourceConsumption(ResourceScope.Machine)]
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable 
        internal static RuntimeAssembly InternalLoadFrom(String assemblyFile, 
                                                         Evidence securityEvidence,
                                                         byte[] hashValue, 
                                                         AssemblyHashAlgorithm hashAlgorithm,
                                                         bool forIntrospection,
                                                         bool suppressSecurityChecks,
                                                         ref StackCrawlMark stackMark) 
        {
            if (assemblyFile == null) 
                throw new ArgumentNullException("assemblyFile"); 
            Contract.EndContractBlock();
 
#if FEATURE_CAS_POLICY
            if (securityEvidence != null && !AppDomain.CurrentDomain.IsLegacyCasPolicyEnabled)
            {
                throw new NotSupportedException(Environment.GetResourceString("NotSupported_RequiresCasPolicyImplicit")); 
            }
#endif // FEATURE_CAS_POLICY 
            AssemblyName an = new AssemblyName(); 
            an.CodeBase = assemblyFile;
            an.SetHashControl(hashValue, hashAlgorithm); 
            // The stack mark is used for MDA filtering
            return InternalLoadAssemblyName(an, securityEvidence, ref stackMark, forIntrospection, suppressSecurityChecks);
        }
 
        [System.Security.SecurityCritical]  // auto-generated
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable 
        internal static RuntimeAssembly InternalLoad(String assemblyString, 
                                                     Evidence assemblySecurity,
                                                     ref StackCrawlMark stackMark, 
                                                     bool forIntrospection)
        {
            if (assemblyString == null)
                throw new ArgumentNullException("assemblyString"); 
            Contract.EndContractBlock();
            if ((assemblyString.Length == 0) || 
                (assemblyString[0] == '\0')) 
                throw new ArgumentException(Environment.GetResourceString("Format_StringZeroLength"));
 
            AssemblyName an = new AssemblyName();
            RuntimeAssembly assembly = null;

            an.Name = assemblyString; 
            int hr = an.nInit(out assembly, forIntrospection, true);
 
            if (hr == System.__HResults.FUSION_E_INVALID_NAME) { 
                return assembly;
            } 

            return InternalLoadAssemblyName(an, assemblySecurity, ref stackMark, forIntrospection, false);
        }
 
        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)] 
        [ResourceConsumption(ResourceScope.Machine, ResourceScope.Machine)] 
        internal static RuntimeAssembly InternalLoadAssemblyName(
            AssemblyName assemblyRef, 
            Evidence assemblySecurity,
            ref StackCrawlMark stackMark,
            bool forIntrospection,
            bool suppressSecurityChecks) 
        {
 
            if (assemblyRef == null) 
                throw new ArgumentNullException("assemblyRef");
            Contract.EndContractBlock(); 

            assemblyRef = (AssemblyName)assemblyRef.Clone();
#if FEATURE_VERSIONING
            if (!forIntrospection && 
                (assemblyRef.ProcessorArchitecture != ProcessorArchitecture.None)) {
                // PA does not have a semantics for by-name binds for execution 
                assemblyRef.ProcessorArchitecture = ProcessorArchitecture.None; 
            }
#endif 

            if (assemblySecurity != null)
            {
#if FEATURE_CAS_POLICY 
                if (!AppDomain.CurrentDomain.IsLegacyCasPolicyEnabled)
                { 
                    throw new NotSupportedException(Environment.GetResourceString("NotSupported_RequiresCasPolicyImplicit")); 
                }
#endif // FEATURE_CAS_POLICY 

                if (!suppressSecurityChecks)
                {
                    new SecurityPermission(SecurityPermissionFlag.ControlEvidence).Demand(); 
                }
            } 
 

            String codeBase = VerifyCodeBase(assemblyRef.CodeBase); 
            if (codeBase != null && !suppressSecurityChecks) {

                if (String.Compare( codeBase, 0, s_localFilePrefix, 0, 5, StringComparison.OrdinalIgnoreCase) != 0) {
#if FEATURE_FUSION   // Of all the binders, Fusion is the only one that understands Web locations 
                    IPermission perm = CreateWebPermission( assemblyRef.EscapedCodeBase );
                    perm.Demand(); 
#else 
                     throw new ArgumentException(Environment.GetResourceString("Arg_InvalidFileName"), "assemblyRef.CodeBase");
#endif 
                }
                else {
                    System.Security.Util.URLString urlString = new System.Security.Util.URLString( codeBase, true );
                    new FileIOPermission( FileIOPermissionAccess.PathDiscovery | FileIOPermissionAccess.Read , urlString.GetFileName() ).Demand(); 
                }
            } 
 
            return nLoad(assemblyRef, codeBase, assemblySecurity, null, ref stackMark, true, forIntrospection, suppressSecurityChecks);
        } 

        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        private static extern RuntimeAssembly _nLoad(AssemblyName fileName,
                                                     String codeBase, 
                                                     Evidence assemblySecurity, 
                                                     RuntimeAssembly locationHint,
                                                     ref StackCrawlMark stackMark, 
                                                     bool throwOnFileNotFound,
                                                     bool forIntrospection,
                                                     bool suppressSecurityChecks);
 

 
        [System.Security.SecurityCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.None)]
        private static RuntimeAssembly nLoad(AssemblyName fileName, 
                                             String codeBase,
                                             Evidence assemblySecurity,
                                             RuntimeAssembly locationHint,
                                             ref StackCrawlMark stackMark, 
                                             bool throwOnFileNotFound,
                                             bool forIntrospection, 
                                             bool suppressSecurityChecks) 
        {
            return _nLoad(fileName, codeBase, assemblySecurity, locationHint, ref stackMark, throwOnFileNotFound, forIntrospection, suppressSecurityChecks); 
        }

#if FEATURE_FUSION
        // used by vm 
        [System.Security.SecurityCritical]  // auto-generated
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable 
        private static unsafe RuntimeAssembly LoadWithPartialNameHack(String partialName, bool cropPublicKey) 
        {
            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller; 
 	
            RuntimeAssembly result = null;
            AssemblyName an = new AssemblyName(partialName);
 
            if (!IsSimplyNamed(an))
            { 
                if (cropPublicKey) 
                {
                    an.SetPublicKey(null); 
                    an.SetPublicKeyToken(null);
                }
                AssemblyName GACAssembly = EnumerateCache(an);
                if(GACAssembly != null) 
                    result = InternalLoadAssemblyName(GACAssembly, null,
                                        ref stackMark, false, false); 
            } 

            return result; 
        }

        [System.Security.SecurityCritical]  // auto-generated
        internal static RuntimeAssembly LoadWithPartialNameInternal(String partialName, Evidence securityEvidence, ref StackCrawlMark stackMark) 
        {
            AssemblyName an = new AssemblyName(partialName); 
            return LoadWithPartialNameInternal(an, securityEvidence, ref stackMark); 
        }
 
        [System.Security.SecurityCritical]  // auto-generated
        internal static RuntimeAssembly LoadWithPartialNameInternal(AssemblyName an, Evidence securityEvidence, ref StackCrawlMark stackMark)
        {
            if (securityEvidence != null) 
            {
#if FEATURE_CAS_POLICY 
                if (!AppDomain.CurrentDomain.IsLegacyCasPolicyEnabled) 
                {
                    throw new NotSupportedException(Environment.GetResourceString("NotSupported_RequiresCasPolicyImplicit")); 
                }
#endif // FEATURE_CAS_POLICY
                new SecurityPermission(SecurityPermissionFlag.ControlEvidence).Demand();
            } 

            RuntimeAssembly result = null; 
            try { 
                result = nLoad(an, null, securityEvidence, null, ref stackMark, true, false, false);
            } 
            catch(Exception e) {
                if (e.IsTransient)
                    throw e;
 
                if (IsUserError(e))
                    throw; 
 
                if (IsSimplyNamed(an))
                    return null; 

                AssemblyName GACAssembly = EnumerateCache(an);
                if(GACAssembly != null)
                    return InternalLoadAssemblyName(GACAssembly, securityEvidence, ref stackMark, false, false); 
            }
 
 
            return result;
        } 

        [SecuritySafeCritical]
        private static bool IsUserError(Exception e)
        { 
            return (uint)Marshal.GetHRForException(e) == COR_E_LOADING_REFERENCE_ASSEMBLY;
        } 
 
        private static bool IsSimplyNamed(AssemblyName partialName)
        { 
            byte[] pk = partialName.GetPublicKeyToken();
            if ((pk != null) &&
                (pk.Length == 0))
                return true; 

            pk = partialName.GetPublicKey(); 
            if ((pk != null) && 
                (pk.Length == 0))
                return true; 

            return false;
        }
 
        [System.Security.SecurityCritical]  // auto-generated
        private static AssemblyName EnumerateCache(AssemblyName partialName) 
        { 
            new SecurityPermission(SecurityPermissionFlag.UnmanagedCode).Assert();
 
            partialName.Version = null;

            ArrayList a = new ArrayList();
            Fusion.ReadCache(a, partialName.FullName, ASM_CACHE.GAC); 

            IEnumerator myEnum = a.GetEnumerator(); 
            AssemblyName ainfoBest = null; 
            CultureInfo refCI = partialName.CultureInfo;
 
            while (myEnum.MoveNext()) {
                AssemblyName ainfo = new AssemblyName((String)myEnum.Current);

                if (CulturesEqual(refCI, ainfo.CultureInfo)) { 
                    if (ainfoBest == null)
                        ainfoBest = ainfo; 
                    else { 
                        // Choose highest version
                        if (ainfo.Version > ainfoBest.Version) 
                            ainfoBest = ainfo;
                    }
                }
            } 

            return ainfoBest; 
        } 

        private static bool CulturesEqual(CultureInfo refCI, CultureInfo defCI) 
        {
            bool defNoCulture = defCI.Equals(CultureInfo.InvariantCulture);

            // cultured asms aren't allowed to be bound to if 
            // the ref doesn't ask for them specifically
            if ((refCI == null) || refCI.Equals(CultureInfo.InvariantCulture)) 
                return defNoCulture; 

            if (defNoCulture || 
                ( !defCI.Equals(refCI) ))
                return false;

            return true; 
        }
#endif // FEATURE_FUSION 
 
        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        private static extern bool IsReflectionOnly(RuntimeAssembly assembly);

        // To not break compatibility with the V1 _Assembly interface we need to make this 
        // new member ComVisible(false).
        [ComVisible(false)] 
        public override bool ReflectionOnly 
        {
            [System.Security.SecuritySafeCritical]  // auto-generated 
            get
            {
                return IsReflectionOnly(GetNativeHandle());
            } 
        }
 
#if FEATURE_CORECLR 

        // Loads the assembly with a COFF based IMAGE containing 
        // an emitted assembly. The assembly is loaded into the domain
        // of the caller. Currently is implemented only for  UnmanagedMemoryStream
        // (no derived classes since we are not calling Read())
        internal static RuntimeAssembly InternalLoadFromStream(Stream assemblyStream, Stream pdbStream, ref StackCrawlMark stackMark) 
        {
            if (assemblyStream  == null) 
                throw new ArgumentNullException("assemblyStream"); 

            if (assemblyStream.GetType()!=typeof(UnmanagedMemoryStream)) 
                throw new NotSupportedException();

            if (pdbStream!= null && pdbStream.GetType()!=typeof(UnmanagedMemoryStream))
                throw new NotSupportedException(); 

            UnmanagedMemoryStream umAssemblyStream = (UnmanagedMemoryStream)assemblyStream; 
            UnmanagedMemoryStream umPdbStream = (UnmanagedMemoryStream)pdbStream; 

            unsafe 
            {
                byte* umAssemblyStreamBuffer=umAssemblyStream.PositionPointer;
                byte* umPdbStreamBuffer=(umPdbStream!=null)?umPdbStream.PositionPointer:null;
                long assemblyDataLength = umAssemblyStream.Length-umAssemblyStream.Position; 
                long pdbDataLength = (umPdbStream!=null)?(umPdbStream.Length-umPdbStream.Position):0;
 
                // use Seek() to benefit from boundary checking, the actual read is done using *StreamBuffer 
                umAssemblyStream.Seek(assemblyDataLength,SeekOrigin.Current);
 
                if(umPdbStream != null)
                {
                    umPdbStream.Seek(pdbDataLength,SeekOrigin.Current);
                } 

                BCLDebug.Assert(assemblyDataLength > 0L, "assemblyDataLength > 0L"); 
 
                RuntimeAssembly assembly = null;
 
                nLoadFromUnmanagedArray(false,
                                                                 umAssemblyStreamBuffer,
                                                                 (ulong)assemblyDataLength,
                                                                 umPdbStreamBuffer, 
                                                                 (ulong)pdbDataLength,
                                                                 JitHelpers.GetStackCrawlMarkHandle(ref stackMark), 
                                                                 JitHelpers.GetObjectHandleOnStack(ref assembly)); 

                return assembly; 
            }
        }
#endif //FEATURE_CORECLR
 
#if FEATURE_MULTIMODULE_ASSEMBLIES
        [System.Security.SecurityCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.Machine | ResourceScope.Assembly)] 
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)]
		[SuppressUnmanagedCodeSecurity] 
        private extern static void LoadModule(RuntimeAssembly assembly,
                                                      String moduleName,
                                                      byte[] rawModule, int cbModule,
                                                      byte[] rawSymbolStore, int cbSymbolStore, 
                                                      ObjectHandleOnStack retModule);
 
        [ResourceExposure(ResourceScope.Machine)] 
        [ResourceConsumption(ResourceScope.Machine)]
        [SecurityPermissionAttribute(SecurityAction.Demand, ControlEvidence = true)] 
        [System.Security.SecuritySafeCritical]  // auto-generated
        public override Module LoadModule(String moduleName, byte[] rawModule, byte[] rawSymbolStore)
        {
            RuntimeModule retModule = null; 
            LoadModule(
                GetNativeHandle(), 
                moduleName, 
                rawModule,
                (rawModule != null) ? rawModule.Length : 0, 
                rawSymbolStore,
                (rawSymbolStore != null) ? rawSymbolStore.Length : 0,
                JitHelpers.GetObjectHandleOnStack(ref retModule));
 
            return retModule;
        } 
#endif //FEATURE_MULTIMODULE_ASSEMBLIES 

        // Returns the module in this assembly with name 'name' 

        [System.Security.SecurityCritical]  // auto-generated
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)]
 		[SuppressUnmanagedCodeSecurity] 
        private static extern void GetModule(RuntimeAssembly assembly, String name, ObjectHandleOnStack retModule);
 
        [System.Security.SecuritySafeCritical]  // auto-generated 
        public override Module GetModule(String name)
        { 
            Module retModule = null;
            GetModule(GetNativeHandle(), name, JitHelpers.GetObjectHandleOnStack(ref retModule));
            return retModule;
        } 

        // Returns the file in the File table of the manifest that matches the 
        // given name.  (Name should not include path.) 
        [System.Security.SecuritySafeCritical]  // auto-generated
        [ResourceExposure(ResourceScope.Machine | ResourceScope.Assembly)] 
        [ResourceConsumption(ResourceScope.Machine | ResourceScope.Assembly)]
        public override FileStream GetFile(String name)
        {
            RuntimeModule m = (RuntimeModule)GetModule(name); 
            if (m == null)
                return null; 
 
            return new FileStream(m.GetFullyQualifiedName(),
                                  FileMode.Open, 
                                  FileAccess.Read, FileShare.Read);
        }

        [System.Security.SecuritySafeCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.Machine | ResourceScope.Assembly)]
        [ResourceConsumption(ResourceScope.Machine | ResourceScope.Assembly)] 
        public override FileStream[] GetFiles(bool getResourceModules) 
        {
            Module[] m = GetModules(getResourceModules); 
            int iLength = m.Length;
            FileStream[] fs = new FileStream[iLength];

            for(int i = 0; i < iLength; i++) 
                fs[i] = new FileStream(((RuntimeModule)m[i]).GetFullyQualifiedName(),
                                       FileMode.Open, 
                                       FileAccess.Read, FileShare.Read); 

            return fs; 
        }


        // Returns the names of all the resources 
        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        private static extern String[] GetManifestResourceNames(RuntimeAssembly assembly);
 
        // Returns the names of all the resources
        [System.Security.SecuritySafeCritical]  // auto-generated
        public override String[] GetManifestResourceNames()
        { 
            return GetManifestResourceNames(GetNativeHandle());
        } 
 

        [System.Security.SecurityCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.None)]
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)]
 		[SuppressUnmanagedCodeSecurity]
        private extern static void GetExecutingAssembly(StackCrawlMarkHandle stackMark, ObjectHandleOnStack retAssembly); 

        [System.Security.SecurityCritical]  // auto-generated 
        internal static Assembly GetExecutingAssembly(ref StackCrawlMark stackMark) 
        {
            RuntimeAssembly retAssembly = null; 
            GetExecutingAssembly(JitHelpers.GetStackCrawlMarkHandle(ref stackMark), JitHelpers.GetObjectHandleOnStack(ref retAssembly));
            return retAssembly;
        }
 
        // Returns the names of all the resources
        [System.Security.SecurityCritical]  // auto-generated 
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        [ResourceExposure(ResourceScope.None)]
        private static extern AssemblyName[] GetReferencedAssemblies(RuntimeAssembly assembly); 

        [System.Security.SecuritySafeCritical]  // auto-generated
        public override AssemblyName[] GetReferencedAssemblies()
        { 
            return GetReferencedAssemblies(GetNativeHandle());
        } 
 
        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.Machine)] 
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)]
		[SuppressUnmanagedCodeSecurity]
        private static extern int GetManifestResourceInfo(RuntimeAssembly assembly,
                                                          String resourceName, 
                                                          ObjectHandleOnStack assemblyRef,
                                                          StringHandleOnStack retFileName, 
                                                          StackCrawlMarkHandle stackMark); 

        [System.Security.SecuritySafeCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable
        public override ManifestResourceInfo GetManifestResourceInfo(String resourceName) 
        {
            RuntimeAssembly retAssembly = null; 
            String fileName = null; 
            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller;
            int location = GetManifestResourceInfo(GetNativeHandle(), resourceName, 
                                                   JitHelpers.GetObjectHandleOnStack(ref retAssembly),
                                                   JitHelpers.GetStringHandleOnStack(ref fileName),
                                                   JitHelpers.GetStackCrawlMarkHandle(ref stackMark));
 
            if (location == -1)
                return null; 
 
            return new ManifestResourceInfo(retAssembly, fileName,
                                                (ResourceLocation) location); 
        }

        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.Machine)] 
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)]
 		[SuppressUnmanagedCodeSecurity] 
        private static extern void GetLocation(RuntimeAssembly assembly, StringHandleOnStack retString); 

        public override String Location 
        {
            [System.Security.SecuritySafeCritical]  // auto-generated
            [ResourceExposure(ResourceScope.Machine)]
            [ResourceConsumption(ResourceScope.Machine)] 
            get {
                String location = null; 
 
                GetLocation(GetNativeHandle(), JitHelpers.GetStringHandleOnStack(ref location));
 
                if (location != null)
                    new FileIOPermission( FileIOPermissionAccess.PathDiscovery, location ).Demand();

                return location; 
            }
        } 
 

        [System.Security.SecurityCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.None)]
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)]
		[SuppressUnmanagedCodeSecurity]
        private extern static void GetImageRuntimeVersion(RuntimeAssembly assembly, StringHandleOnStack retString); 

        // To not break compatibility with the V1 _Assembly interface we need to make this 
        // new member ComVisible(false). 
        [ComVisible(false)]
        public override String ImageRuntimeVersion 
        {
            [System.Security.SecuritySafeCritical]  // auto-generated
            get{
                String s = null; 
                GetImageRuntimeVersion(GetNativeHandle(), JitHelpers.GetStringHandleOnStack(ref s));
                return s; 
            } 
        }
 

        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        private extern static bool IsGlobalAssemblyCache(RuntimeAssembly assembly);
 
        public override bool GlobalAssemblyCache 
        {
            [System.Security.SecuritySafeCritical]  // auto-generated 
            get
            {
                return IsGlobalAssemblyCache(GetNativeHandle());
            } 
        }
 
        [System.Security.SecurityCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.None)]
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)] 
		[SuppressUnmanagedCodeSecurity]
        private extern static Int64 GetHostContext(RuntimeAssembly assembly);

        public override Int64 HostContext 
        {
            [System.Security.SecuritySafeCritical]  // auto-generated 
            get 
            {
                return GetHostContext(GetNativeHandle()); 
            }
        }

        [ResourceExposure(ResourceScope.Machine)] 
        [ResourceConsumption(ResourceScope.Machine)]
        private static String VerifyCodeBase(String codebase) 
        { 
            if(codebase == null)
                return null; 

            int len = codebase.Length;
            if (len == 0)
                return null; 

 
            int j = codebase.IndexOf(':'); 
            // Check to see if the url has a prefix
            if( (j != -1) && 
                (j+2 < len) &&
                ((codebase[j+1] == '/') || (codebase[j+1] == '\\')) &&
                ((codebase[j+2] == '/') || (codebase[j+2] == '\\')) )
                return codebase; 
#if !PLATFORM_UNIX
            else if ((len > 2) && (codebase[0] == '\\') && (codebase[1] == '\\')) 
                return "file://" + codebase; 
            else
                return "file:///" + Path.GetFullPathInternal( codebase ); 
#else
            else
                return "file://" + Path.GetFullPathInternal( codebase );
#endif // !PLATFORM_UNIX 
        }
 
        [System.Security.SecurityCritical]  // auto-generated 
        internal Stream GetManifestResourceStream(
            Type type, 
            String name,
            bool skipSecurityCheck,
            ref StackCrawlMark stackMark)
        { 
            StringBuilder sb = new StringBuilder();
            if(type == null) { 
                if (name == null) 
                    throw new ArgumentNullException("type");
            } 
            else {
                String nameSpace = type.Namespace;
                if(nameSpace != null) {
                    sb.Append(nameSpace); 
                    if(name != null)
                        sb.Append(Type.Delimiter); 
                } 
            }
 
            if(name != null)
                sb.Append(name);

            return GetManifestResourceStream(sb.ToString(), ref stackMark, skipSecurityCheck); 
        }
 
#if FEATURE_CAS_POLICY 
        /// <summary>
        ///     Determine if this assembly was loaded from a location under the AppBase for security 
        ///     purposes.  For instance, strong name bypass is disabled for assemblies considered outside
        ///     the AppBase.
        ///
        ///     This method is called from the VM: AssemblySecurityDescriptor::WasAssemblyLoadedFromAppBase 
        /// </summary>
        [System.Security.SecurityCritical]  // auto-generated 
        [FileIOPermission(SecurityAction.Assert, Unrestricted = true)] 
        private bool IsAssemblyUnderAppBase() {
            string assemblyLocation = Location; 

            // Assemblies that are loaded without a location will be considered part of the app
            if (String.IsNullOrEmpty(assemblyLocation)) {
                return true; 
            }
 
            // Compare the paths using the FileIOAccess mechanism that FileIOPermission uses internally.  If 
            // assemblyAccess is a subset of appbaseAccess (in FileIOPermission terms, appBaseAccess is the
            // demand set, and assemblyAccess is the grant set) that means assembly must be under the AppBase. 
            FileIOAccess assemblyAccess = new FileIOAccess(Path.GetFullPathInternal(assemblyLocation));
            FileIOAccess appBaseAccess = new FileIOAccess(Path.GetFullPathInternal(AppDomain.CurrentDomain.BaseDirectory));

            return assemblyAccess.IsSubsetOf(appBaseAccess); 
        }
 
        internal bool IsStrongNameVerified 
        {
            [System.Security.SecurityCritical]  // auto-generated 
            get { return GetIsStrongNameVerified(GetNativeHandle()); }
        }

        [System.Security.SecurityCritical]  // auto-generated 
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)]
        [SuppressUnmanagedCodeSecurity] 
        private static extern bool GetIsStrongNameVerified(RuntimeAssembly assembly); 
#endif // FEATURE_CAS_POLICY
 
        // GetResource will return a pointer to the resources in memory.
        [System.Security.SecurityCritical]  // auto-generated
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)]
        [SuppressUnmanagedCodeSecurity] 
        [ResourceExposure(ResourceScope.None)]
        private static unsafe extern byte* GetResource(RuntimeAssembly assembly, 
                                                       String resourceName, 
                                                       out ulong length,
                                                       StackCrawlMarkHandle stackMark, 
                                                       bool skipSecurityCheck);

        [System.Security.SecurityCritical]  // auto-generated
        internal unsafe Stream GetManifestResourceStream(String name, ref StackCrawlMark stackMark, bool skipSecurityCheck) 
        {
            ulong length = 0; 
            byte* pbInMemoryResource = GetResource(GetNativeHandle(), name, out length, JitHelpers.GetStackCrawlMarkHandle(ref stackMark), skipSecurityCheck); 

            if (pbInMemoryResource != null) { 
                //Console.WriteLine("Creating an unmanaged memory stream of length "+length);
                if (length > Int64.MaxValue)
                    throw new NotImplementedException(Environment.GetResourceString("NotImplemented_ResourcesLongerThan2^63"));
 
                // <STRIP>For cases where we're loading an embedded resource from an assembly,
                // in V1 we do not have any serious lifetime issues with the 
                // UnmanagedMemoryStream.  If the Stream is only used 
                // in the AppDomain that contains the assembly, then if that AppDomain
                // is unloaded, we will collect all of the objects in the AppDomain first 
                // before unloading assemblies.  If the Stream is shared across AppDomains,
                // then the original AppDomain was unloaded, accesses to this Stream will
                // throw an exception saying the appdomain was unloaded.  This is
                // guaranteed be EE AppDomain goo.  And for shared assemblies like 
                // mscorlib, their lifetime is the lifetime of the process, so the
                // assembly will NOT be unloaded, so the resource will always be in memory.</STRIP> 
                return new UnmanagedMemoryStream(pbInMemoryResource, (long)length, (long)length, FileAccess.Read, true); 
            }
 
            //Console.WriteLine("GetManifestResourceStream: Blob "+name+" not found...");
            return null;
        }
 
        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)] 
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)] 
		[SuppressUnmanagedCodeSecurity]
        private static extern void GetVersion(RuntimeAssembly assembly, 
                                              out int majVer,
                                              out int minVer,
                                              out int buildNum,
                                              out int revNum); 

        [System.Security.SecurityCritical]  // auto-generated 
        internal Version GetVersion() 
        {
            int majorVer, minorVer, build, revision; 
            GetVersion(GetNativeHandle(), out majorVer, out minorVer, out build, out revision);
            return new Version (majorVer, minorVer, build, revision);
        }
 
        [System.Security.SecurityCritical]  // auto-generated
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)] 
 		[SuppressUnmanagedCodeSecurity] 
        private static extern void GetLocale(RuntimeAssembly assembly, StringHandleOnStack retString);
 
        [System.Security.SecurityCritical]  // auto-generated
        internal CultureInfo GetLocale()
        {
            String locale = null; 

            GetLocale(GetNativeHandle(), JitHelpers.GetStringHandleOnStack(ref locale)); 
 
            if (locale == null)
                return CultureInfo.InvariantCulture; 

            return new CultureInfo(locale);
        }
 
        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        private static extern bool FCallIsDynamic(RuntimeAssembly assembly);
 
        public override bool IsDynamic
        {
            [SecuritySafeCritical]
            get { 
                return FCallIsDynamic(GetNativeHandle());
            } 
        } 

        [System.Security.SecurityCritical]  // auto-generated 
        private void VerifyCodeBaseDiscovery(String codeBase)
        {
            if ((codeBase != null) &&
                (String.Compare( codeBase, 0, s_localFilePrefix, 0, 5, StringComparison.OrdinalIgnoreCase) == 0)) { 
                System.Security.Util.URLString urlString = new System.Security.Util.URLString( codeBase, true );
                new FileIOPermission( FileIOPermissionAccess.PathDiscovery, urlString.GetFileName() ).Demand(); 
            } 
        }
 
        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)]
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)]
		[SuppressUnmanagedCodeSecurity] 
        private static extern void GetSimpleName(RuntimeAssembly assembly, StringHandleOnStack retSimpleName);
 
        [SecuritySafeCritical] 
        internal String GetSimpleName()
        { 
            string name = null;
            GetSimpleName(GetNativeHandle(), JitHelpers.GetStringHandleOnStack(ref name));
            return name;
        } 

        [System.Security.SecurityCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.None)] 
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)]
 		[SuppressUnmanagedCodeSecurity] 
        private extern static AssemblyHashAlgorithm GetHashAlgorithm(RuntimeAssembly assembly);

        [System.Security.SecurityCritical]  // auto-generated
        private AssemblyHashAlgorithm GetHashAlgorithm() 
        {
            return GetHashAlgorithm(GetNativeHandle()); 
        } 

        [System.Security.SecurityCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.None)]
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)]
 		[SuppressUnmanagedCodeSecurity]
        private extern static AssemblyNameFlags GetFlags(RuntimeAssembly assembly); 

        [System.Security.SecurityCritical]  // auto-generated 
        private AssemblyNameFlags GetFlags() 
        {
            return GetFlags(GetNativeHandle()); 
        }

        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)]
        [ResourceExposure(ResourceScope.None)] 
        [SecurityCritical]
        [SuppressUnmanagedCodeSecurity] 
        private static extern void GetRawBytes(RuntimeAssembly assembly, ObjectHandleOnStack retRawBytes); 

        // Get the raw bytes of the assembly 
        [SecuritySafeCritical]
        internal byte[] GetRawBytes()
        {
            byte[] rawBytes = null; 

            GetRawBytes(GetNativeHandle(), JitHelpers.GetObjectHandleOnStack(ref rawBytes)); 
            return rawBytes; 
        }
 
        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)]
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)]
		[SuppressUnmanagedCodeSecurity] 
        private static extern void GetPublicKey(RuntimeAssembly assembly, ObjectHandleOnStack retPublicKey);
 
        [System.Security.SecurityCritical]  // auto-generated 
        internal byte[] GetPublicKey()
        { 
            byte[] publicKey = null;
            GetPublicKey(GetNativeHandle(), JitHelpers.GetObjectHandleOnStack(ref publicKey));
            return publicKey;
        } 

        [SecurityCritical] 
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)] 
 		[SuppressUnmanagedCodeSecurity]
        [ResourceExposure(ResourceScope.None)] 
        private extern static void GetGrantSet(RuntimeAssembly assembly, ObjectHandleOnStack granted, ObjectHandleOnStack denied);

        [SecurityCritical]
        internal void GetGrantSet(out PermissionSet newGrant, out PermissionSet newDenied) 
        {
            PermissionSet granted = null, denied = null; 
            GetGrantSet(GetNativeHandle(), JitHelpers.GetObjectHandleOnStack(ref granted), JitHelpers.GetObjectHandleOnStack(ref denied)); 
            newGrant = granted; newDenied = denied;
        } 

        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)]
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)] 
        [SuppressUnmanagedCodeSecurity]
        [return: MarshalAs(UnmanagedType.Bool)] 
        private extern static bool IsAllSecurityCritical(RuntimeAssembly assembly); 

        // Is everything introduced by this assembly critical 
        [System.Security.SecuritySafeCritical]  // auto-generated
        internal bool IsAllSecurityCritical()
        {
            return IsAllSecurityCritical(GetNativeHandle()); 
        }
 
        [System.Security.SecurityCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.None)]
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)] 
        [SuppressUnmanagedCodeSecurity]
        [return: MarshalAs(UnmanagedType.Bool)]
        private extern static bool IsAllSecuritySafeCritical(RuntimeAssembly assembly);
 
        // Is everything introduced by this assembly safe critical
        [System.Security.SecuritySafeCritical]  // auto-generated 
        internal bool IsAllSecuritySafeCritical() 
        {
            return IsAllSecuritySafeCritical(GetNativeHandle()); 
        }

        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)] 
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)]
        [SuppressUnmanagedCodeSecurity] 
        [return: MarshalAs(UnmanagedType.Bool)] 
        private extern static bool IsAllSecurityTransparent(RuntimeAssembly assembly);
 
        // Is everything introduced by this assembly transparent
        [System.Security.SecuritySafeCritical]  // auto-generated
        internal bool IsAllSecurityTransparent()
        { 
            return IsAllSecurityTransparent(GetNativeHandle());
        } 
 
#if FEATURE_FUSION
        // demandFlag: 
        // 0 demand PathDiscovery permission only
        // 1 demand Read permission only
        // 2 demand both Read and PathDiscovery
        // 3 demand Web permission only 
        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)] 
        [ResourceConsumption(ResourceScope.Machine, ResourceScope.Machine)] 
        private static void DemandPermission(String codeBase, bool havePath,
                                             int demandFlag) 
        {
            FileIOPermissionAccess access = FileIOPermissionAccess.PathDiscovery;
            switch(demandFlag) {
 
            case 0: // default
                break; 
            case 1: 
                access = FileIOPermissionAccess.Read;
                break; 
            case 2:
                access = FileIOPermissionAccess.PathDiscovery | FileIOPermissionAccess.Read;
                break;
 
            case 3:
                IPermission perm = CreateWebPermission(AssemblyName.EscapeCodeBase(codeBase)); 
                perm.Demand(); 
                return;
            } 

            if (!havePath) {
                System.Security.Util.URLString urlString = new System.Security.Util.URLString( codeBase, true );
                codeBase = urlString.GetFileName(); 
            }
 
            codeBase = Path.GetFullPathInternal(codeBase);  // canonicalize 

            new FileIOPermission(access, codeBase).Demand(); 
        }
#endif

#if FEATURE_FUSION 
        private static IPermission CreateWebPermission( String codeBase )
        { 
            Contract.Assert( codeBase != null, "Must pass in a valid CodeBase" ); 
            Assembly sys = Assembly.Load("System, Version=" + ThisAssembly.Version + ", Culture=neutral, PublicKeyToken=" + AssemblyRef.EcmaPublicKeyToken);
 
            Type type = sys.GetType("System.Net.NetworkAccess", true);

            IPermission retval = null;
            if (!type.IsEnum || !type.IsVisible) 
                goto Exit;
 
            Object[] webArgs = new Object[2]; 
            webArgs[0] = (Enum) Enum.Parse(type, "Connect", true);
            if (webArgs[0] == null) 
                goto Exit;

            webArgs[1] = codeBase;
 
            type = sys.GetType("System.Net.WebPermission", true);
 
            if (!type.IsVisible) 
                goto Exit;
 
            retval = (IPermission) Activator.CreateInstance(type, webArgs);

        Exit:
            if (retval == null) { 
                Contract.Assert( false, "Unable to create WebPermission" );
                throw new InvalidOperationException(); 
            } 

            return retval; 
        }
#endif

        private RuntimeModule OnModuleResolveEvent(String moduleName) 
        {
            ModuleResolveEventHandler moduleResolve = _ModuleResolve; 
            if (moduleResolve == null) 
                return null;
 
            Delegate[] ds = moduleResolve.GetInvocationList();
            int len = ds.Length;
            for (int i = 0; i < len; i++) {
                RuntimeModule ret = (RuntimeModule)((ModuleResolveEventHandler) ds[i])(this, new ResolveEventArgs(moduleName,this)); 
                if (ret != null)
                    return ret; 
            } 

            return null; 
        }

        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable
        public override Assembly GetSatelliteAssembly(CultureInfo culture) 
        {
            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller; 
            return InternalGetSatelliteAssembly(culture, null, ref stackMark); 
        }
 
        // Useful for binding to a very specific version of a satellite assembly
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable
        public override Assembly GetSatelliteAssembly(CultureInfo culture, Version version)
        { 
            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller;
            return InternalGetSatelliteAssembly(culture, version, ref stackMark); 
        } 

        [System.Security.SecuritySafeCritical]  // auto-generated 
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable
        internal Assembly InternalGetSatelliteAssembly(CultureInfo culture,
                                                       Version version,
                                                       ref StackCrawlMark stackMark) 
        {
            if (culture == null) 
                throw new ArgumentNullException("culture"); 
            Contract.EndContractBlock();
 

            String name = GetSimpleName() + ".resources";
            return InternalGetSatelliteAssembly(name, culture, version, true, ref stackMark);
        } 

        [System.Security.SecurityCritical]  // auto-generated 
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable 
        internal RuntimeAssembly InternalGetSatelliteAssembly(String name,
                                                              CultureInfo culture, 
                                                              Version version,
                                                              bool throwOnFileNotFound,
                                                              ref StackCrawlMark stackMark)
        { 

            AssemblyName an = new AssemblyName(); 
 
            an.SetPublicKey(GetPublicKey());
            an.Flags = GetFlags() | AssemblyNameFlags.PublicKey; 

            if (version == null)
                an.Version = GetVersion();
            else 
                an.Version = version;
 
            an.CultureInfo = culture; 
            an.Name = name;
 
            RuntimeAssembly a = nLoad(an, null, null, this, ref stackMark, throwOnFileNotFound, false, false);
            if (a == this) {
                throw new FileNotFoundException(String.Format(culture, Environment.GetResourceString("IO.FileNotFound_FileName"), an.Name));
            } 

            return a; 
        } 

        internal InternalCache Cache { 
            get {
                // This grabs an internal copy of m_cachedData and uses
                // that instead of looking at m_cachedData directly because
                // the cache may get cleared asynchronously.  This prevents 
                // us from having to take a lock.
                InternalCache cache = m_cachedData; 
                if (cache == null) { 
                    cache = new InternalCache("Assembly");
                    m_cachedData = cache; 

                    // If assembly is not collectible assembly, then arrange to clear the
                    // cache at the next finalization.
                    if (SyncRoot.GetType() != typeof(LoaderAllocator)) 
                        GC.ClearCache += new ClearCacheHandler(OnCacheClear);
                } 
                return cache; 
            }
        } 

        internal void OnCacheClear(Object sender, ClearCacheEventArgs cacheEventArgs)
        {
            m_cachedData = null; 
        }
 
        [System.Security.SecurityCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.Machine)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        static internal extern RuntimeAssembly nLoadFile(String path, Evidence evidence);

        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        static internal extern RuntimeAssembly nLoadImage(byte[] rawAssembly, 
                                                          byte[] rawSymbolStore, 
                                                          Evidence evidence,
                                                          ref StackCrawlMark stackMark, 
                                                          bool fIntrospection,
                                                          SecurityContextSource securityContextSource);
#if FEATURE_CORECLR
        [System.Security.SecurityCritical]  // auto-generated 
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)]
        [SuppressUnmanagedCodeSecurity] 
        static internal extern unsafe void nLoadFromUnmanagedArray(bool fIntrospection, 
                                                                            byte* assemblyContent,
                                                                            ulong assemblySize, 
                                                                            byte* pdbContent,
                                                                            ulong pdbSize,
                                                                            StackCrawlMarkHandle stackMark,
                                                                            ObjectHandleOnStack retAssembly); 
#endif
 
        [System.Security.SecurityCritical]  // auto-generated 
        [DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)]
        [SuppressUnmanagedCodeSecurity] 
        private extern static void GetModules(RuntimeAssembly assembly,
                                              bool loadIfNotFound,
                                              bool getResourceModules,
                                              ObjectHandleOnStack retModuleHandles); 

        [System.Security.SecuritySafeCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.Machine | ResourceScope.Assembly)] 
        internal Module[] GetModulesInternal(bool loadIfNotFound,
                                     bool getResourceModules) 
        {
            Module[] modules = null;
            GetModules(GetNativeHandle(), loadIfNotFound, getResourceModules, JitHelpers.GetObjectHandleOnStack(ref modules));
            return modules; 
        }
 
        [ResourceExposure(ResourceScope.Machine | ResourceScope.Assembly)] 
        [ResourceConsumption(ResourceScope.Machine | ResourceScope.Assembly)]
        public override Module[] GetModules(bool getResourceModules) 
        {
            return GetModulesInternal(true, getResourceModules);
        }
 
        [ResourceExposure(ResourceScope.Machine | ResourceScope.Assembly)]
        [ResourceConsumption(ResourceScope.Machine | ResourceScope.Assembly)] 
        public override Module[] GetLoadedModules(bool getResourceModules) 
        {
            return GetModulesInternal(false, getResourceModules); 
        }

        [System.Security.SecuritySafeCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal static extern RuntimeModule GetManifestModule(RuntimeAssembly assembly); 
 
#if FEATURE_APTCA
        [System.Security.SecuritySafeCritical] 
        [ResourceExposure(ResourceScope.None)]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal static extern bool AptcaCheck(RuntimeAssembly targetAssembly, RuntimeAssembly sourceAssembly);
#endif // FEATURE_APTCA 

        [System.Security.SecurityCritical]  // auto-generated 
        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal static extern int GetToken(RuntimeAssembly assembly); 
    }
}

