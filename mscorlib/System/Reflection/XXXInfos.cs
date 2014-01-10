// ==++== 
//
//   Copyright(c) Microsoft Corporation.  All rights reserved.
//
// ==--== 
// <OWNER>[....]</OWNER>
// 
 
namespace System.Reflection
{ 
    using System;
    using System.Reflection;
    using System.Reflection.Emit;
    using System.Runtime; 
    using System.Runtime.ConstrainedExecution;
    using System.Globalization; 
    using System.Threading; 
    using System.Diagnostics;
    using System.Security.Permissions; 
    using System.Collections;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Security; 
    using System.Text;
    using System.Runtime.InteropServices; 
    using System.Runtime.Serialization; 
    using System.Runtime.Versioning;
    using System.Reflection.Cache; 
    using System.Diagnostics.Contracts;

    //
    // Invocation cached flags. Those are used in unmanaged code as well 
    // so be careful if you change them
    // 
    [Flags] 
    internal enum INVOCATION_FLAGS : uint
    { 
        INVOCATION_FLAGS_UNKNOWN = 0x00000000,
        INVOCATION_FLAGS_INITIALIZED = 0x00000001,
        // it's used for both method and field to signify that no access is allowed
        INVOCATION_FLAGS_NO_INVOKE = 0x00000002, 
        INVOCATION_FLAGS_NEED_SECURITY = 0x00000004,
        // Set for static ctors and ctors on abstract types, which 
        // can be invoked only if the "this" object is provided (even if it's null). 
        INVOCATION_FLAGS_NO_CTOR_INVOKE = 0x00000008,
        // because field and method are different we can reuse the same bits 
        // method
        INVOCATION_FLAGS_IS_CTOR = 0x00000010,
        INVOCATION_FLAGS_RISKY_METHOD = 0x00000020,
        INVOCATION_FLAGS_SECURITY_IMPOSED = 0x00000040, 
        INVOCATION_FLAGS_IS_DELEGATE_CTOR = 0x00000080,
        INVOCATION_FLAGS_CONTAINS_STACK_POINTERS = 0x00000100, 
        // field 
        INVOCATION_FLAGS_SPECIAL_FIELD = 0x00000010,
        INVOCATION_FLAGS_FIELD_SPECIAL_CAST = 0x00000020, 

        // temporary flag used for flagging invocation of method vs ctor
        // this flag never appears on the instance m_invocationFlag and is simply
        // passed down from within ConstructorInfo.Invoke() 
        INVOCATION_FLAGS_CONSTRUCTOR_INVOKE = 0x10000000,
    } 
 
    [Serializable]
    [ClassInterface(ClassInterfaceType.None)] 
    [ComDefaultInterface(typeof(_MemberInfo))]
    [PermissionSetAttribute(SecurityAction.InheritanceDemand, Name = "FullTrust")]
    [System.Runtime.InteropServices.ComVisible(true)]
    [ContractClass(typeof(MemberInfoContracts))] 
    public abstract class MemberInfo : ICustomAttributeProvider, _MemberInfo
    { 
        #region Constructor 
        protected MemberInfo() { }
        #endregion 

        #region Internal Methods
        internal virtual bool CacheEquals(object o) { throw new NotImplementedException(); }
        #endregion 

        #region Public Abstract\Virtual Members 
        public abstract MemberTypes MemberType { get; } 

        public abstract String Name { get; } 

        public abstract Type DeclaringType { get; }

        public abstract Type ReflectedType { get; } 

        public abstract Object[] GetCustomAttributes(bool inherit); 
 
        public abstract Object[] GetCustomAttributes(Type attributeType, bool inherit);
 
        public abstract bool IsDefined(Type attributeType, bool inherit);

        public virtual IList<CustomAttributeData> GetCustomAttributesData()
        { 
            throw new NotImplementedException();
        } 
 
        public virtual int MetadataToken { get { throw new InvalidOperationException(); } }
 
        public virtual Module Module
        {
            get
            { 
                if (this is Type)
                    return ((Type)this).Module; 
 
                throw new NotImplementedException();
            } 
        }


        // this method is required so Object.GetType is not made final virtual by the compiler 
        Type _MemberInfo.GetType() { return base.GetType(); }
        #endregion 
 
#if !FEATURE_CORECLR
        public static bool operator ==(MemberInfo left, MemberInfo right) 
        {
            if (ReferenceEquals(left, right))
                return true;
 
            if ((object)left == null || (object)right == null)
                return false; 
 
            Type type1, type2;
            MethodBase method1, method2; 
            FieldInfo field1, field2;
            EventInfo event1, event2;
            PropertyInfo property1, property2;
 
            if ((type1 = left as Type) != null && (type2 = right as Type) != null)
                return type1 == type2; 
            else if ((method1 = left as MethodBase) != null && (method2 = right as MethodBase) != null) 
                return method1 == method2;
            else if ((field1 = left as FieldInfo) != null && (field2 = right as FieldInfo) != null) 
                return field1 == field2;
            else if ((event1 = left as EventInfo) != null && (event2 = right as EventInfo) != null)
                return event1 == event2;
            else if ((property1 = left as PropertyInfo) != null && (property2 = right as PropertyInfo) != null) 
                return property1 == property2;
 
            return false; 
        }
 
        [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
        public static bool operator !=(MemberInfo left, MemberInfo right)
        {
            return !(left == right); 
        }
 
        public override bool Equals(object obj) 
        {
            return base.Equals(obj); 
        }

        public override int GetHashCode()
        { 
            return base.GetHashCode();
        } 
#endif // !FEATURE_CORECLR 

        void _MemberInfo.GetTypeInfoCount(out uint pcTInfo) 
        {
            throw new NotImplementedException();
        }
 
        void _MemberInfo.GetTypeInfo(uint iTInfo, uint lcid, IntPtr ppTInfo)
        { 
            throw new NotImplementedException(); 
        }
 
        void _MemberInfo.GetIDsOfNames([In] ref Guid riid, IntPtr rgszNames, uint cNames, uint lcid, IntPtr rgDispId)
        {
            throw new NotImplementedException();
        } 

        void _MemberInfo.Invoke(uint dispIdMember, [In] ref Guid riid, uint lcid, short wFlags, IntPtr pDispParams, IntPtr pVarResult, IntPtr pExcepInfo, IntPtr puArgErr) 
        { 
            throw new NotImplementedException();
        } 
    }

    [ContractClassFor(typeof(MemberInfo))]
    internal abstract class MemberInfoContracts : MemberInfo 
    {
        public override String Name { 
            get { 
                Contract.Ensures(Contract.Result<String>() != null);
                return Contract.Result<String>(); 
            }
        }
    }
 
    [Serializable]
    [ClassInterface(ClassInterfaceType.None)] 
    [ComDefaultInterface(typeof(_MethodBase))] 
    [PermissionSetAttribute(SecurityAction.InheritanceDemand, Name = "FullTrust")]
    [System.Runtime.InteropServices.ComVisible(true)] 
    public abstract class MethodBase : MemberInfo, _MethodBase
    {
        #region Static Members
        [System.Security.SecuritySafeCritical]  // auto-generated 
        public static MethodBase GetMethodFromHandle(RuntimeMethodHandle handle)
        { 
            if (handle.IsNullHandle()) 
                throw new ArgumentException(Environment.GetResourceString("Argument_InvalidHandle"));
 
            MethodBase m = RuntimeType.GetMethodBase(handle.GetMethodInfo());

            Type declaringType = m.DeclaringType;
            if (declaringType != null && declaringType.IsGenericType) 
                throw new ArgumentException(String.Format(
                    CultureInfo.CurrentCulture, Environment.GetResourceString("Argument_MethodDeclaringTypeGeneric"), 
                    m, declaringType.GetGenericTypeDefinition())); 

            return m; 
        }

        [System.Security.SecuritySafeCritical]  // auto-generated
        [System.Runtime.InteropServices.ComVisible(false)] 
        public static MethodBase GetMethodFromHandle(RuntimeMethodHandle handle, RuntimeTypeHandle declaringType)
        { 
            if (handle.IsNullHandle()) 
                throw new ArgumentException(Environment.GetResourceString("Argument_InvalidHandle"));
 
            return RuntimeType.GetMethodBase(declaringType.GetRuntimeType(), handle.GetMethodInfo());
        }

        [System.Security.SecuritySafeCritical]  // auto-generated 
        [DynamicSecurityMethod] // Specify DynamicSecurityMethod attribute to prevent inlining of the caller.
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable 
        public static MethodBase GetCurrentMethod() 
        {
            StackCrawlMark stackMark = StackCrawlMark.LookForMyCaller; 
            return RuntimeMethodInfo.InternalGetCurrentMethod(ref stackMark);
        }
        #endregion
 
        #region Constructor
        protected MethodBase() { } 
        #endregion 

#if !FEATURE_CORECLR 
        public static bool operator ==(MethodBase left, MethodBase right)
        {
            if (ReferenceEquals(left, right))
                return true; 

            if ((object)left == null || (object)right == null) 
                return false; 

            MethodInfo method1, method2; 
            ConstructorInfo constructor1, constructor2;

            if ((method1 = left as MethodInfo) != null && (method2 = right as MethodInfo) != null)
                return method1 == method2; 
            else if ((constructor1 = left as ConstructorInfo) != null && (constructor2 = right as ConstructorInfo) != null)
                return constructor1 == constructor2; 
 
            return false;
        } 

        public static bool operator !=(MethodBase left, MethodBase right)
        {
            return !(left == right); 
        }
 
        public override bool Equals(object obj) 
        {
            return base.Equals(obj); 
        }

        public override int GetHashCode()
        { 
            return base.GetHashCode();
        } 
#endif // !FEATURE_CORECLR 

        #region Internal Members 
        // used by EE
        [SecurityCritical]
        private IntPtr GetMethodDesc() { return MethodHandle.Value; }
        #endregion 

        #region Public Abstract\Virtual Members 
        [System.Security.SecuritySafeCritical]  // auto-generated 
        internal virtual ParameterInfo[] GetParametersNoCopy() { return GetParameters (); }
 
        [Pure]
        public abstract ParameterInfo[] GetParameters();

        public abstract MethodImplAttributes GetMethodImplementationFlags(); 

        public abstract RuntimeMethodHandle MethodHandle { get; } 
 
        public abstract MethodAttributes Attributes  { get; }
 
        public abstract Object Invoke(Object obj, BindingFlags invokeAttr, Binder binder, Object[] parameters, CultureInfo culture);

        public virtual CallingConventions CallingConvention { get { return CallingConventions.Standard; } }
 
        [System.Runtime.InteropServices.ComVisible(true)]
        public virtual Type[] GetGenericArguments() { throw new NotSupportedException(Environment.GetResourceString("NotSupported_SubclassOverride")); } 
 
        public virtual bool IsGenericMethodDefinition { get { return false; } }
 
        public virtual bool ContainsGenericParameters { get { return false; } }

        public virtual bool IsGenericMethod { get { return false; } }
 
        public virtual bool IsSecurityCritical { get { throw new NotImplementedException(); } }
 
        public virtual bool IsSecuritySafeCritical { get { throw new NotImplementedException(); } } 

        public virtual bool IsSecurityTransparent { get { throw new NotImplementedException(); } } 

        #endregion

        #region _MethodBase Implementation 
        Type _MethodBase.GetType() { return base.GetType(); }
        bool _MethodBase.IsPublic { get { return IsPublic; } } 
        bool _MethodBase.IsPrivate { get { return IsPrivate; } } 
        bool _MethodBase.IsFamily { get { return IsFamily; } }
        bool _MethodBase.IsAssembly { get { return IsAssembly; } } 
        bool _MethodBase.IsFamilyAndAssembly { get { return IsFamilyAndAssembly; } }
        bool _MethodBase.IsFamilyOrAssembly { get { return IsFamilyOrAssembly; } }
        bool _MethodBase.IsStatic { get { return IsStatic; } }
        bool _MethodBase.IsFinal { get { return IsFinal; } } 
        bool _MethodBase.IsVirtual { get { return IsVirtual; } }
        bool _MethodBase.IsHideBySig { get { return IsHideBySig; } } 
        bool _MethodBase.IsAbstract { get { return IsAbstract; } } 
        bool _MethodBase.IsSpecialName { get { return IsSpecialName; } }
        bool _MethodBase.IsConstructor { get { return IsConstructor; } } 
        #endregion

        #region Public Members
        [DebuggerStepThroughAttribute] 
        [Diagnostics.DebuggerHidden]
        public Object Invoke(Object obj, Object[] parameters) 
        { 
            return Invoke(obj,BindingFlags.Default,null,parameters,null);
        } 

        public bool IsPublic  { get { return(Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public; } }

        public bool IsPrivate { get { return(Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Private; } } 

        public bool IsFamily { get { return(Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Family; } } 
 
        public bool IsAssembly { get { return(Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Assembly; } }
 
        public bool IsFamilyAndAssembly { get { return(Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.FamANDAssem; } }

        public bool IsFamilyOrAssembly { get {return(Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.FamORAssem; } }
 
        public bool IsStatic { get { return(Attributes & MethodAttributes.Static) != 0; } }
 
        public bool IsFinal { get { return(Attributes & MethodAttributes.Final) != 0; } 
        }
        public bool IsVirtual { get { return(Attributes & MethodAttributes.Virtual) != 0; } 
        }
        public bool IsHideBySig { get { return(Attributes & MethodAttributes.HideBySig) != 0; } }

        public bool IsAbstract { get { return(Attributes & MethodAttributes.Abstract) != 0; } } 

        public bool IsSpecialName { get { return(Attributes & MethodAttributes.SpecialName) != 0; } } 
 
[System.Runtime.InteropServices.ComVisible(true)]
        public bool IsConstructor 
        {
            get
            {
                // To be backward compatible we only return true for instance RTSpecialName ctors. 
                return (this is ConstructorInfo &&
                        !IsStatic && 
                        ((Attributes & MethodAttributes.RTSpecialName) == MethodAttributes.RTSpecialName)); 
            }
        } 

        [System.Security.SecuritySafeCritical]  // auto-generated
        [ReflectionPermissionAttribute(SecurityAction.Demand, Flags=ReflectionPermissionFlag.MemberAccess)]
        public virtual MethodBody GetMethodBody() 
        {
            throw new InvalidOperationException(); 
        } 
        #endregion
 
        #region Internal Methods
        // helper method to construct the string representation of the parameter list
        internal static string ConstructParameters(ParameterInfo[] parameters, CallingConventions callingConvention)
        { 
            Type[] parameterTypes = new Type[parameters.Length];
 
            for (int i = 0; i < parameters.Length; i++) 
                parameterTypes[i] = parameters[i].ParameterType;
 
            return ConstructParameters(parameterTypes, callingConvention);
        }

        // helper method to construct the string representation of the parameter list 
        internal static string ConstructParameters(Type[] parameters, CallingConventions callingConvention)
        { 
            StringBuilder sbName = new StringBuilder(); 
            string comma = "";
 
            for (int i = 0; i < parameters.Length; i++)
            {
                Type t = parameters[i];
 
                sbName.Append(comma);
 
                string typeName = t.SigToString(); 
                if (t.IsByRef)
                { 
                    sbName.Append(typeName.TrimEnd(new char[] { '&' }));
                    sbName.Append(" ByRef");
                }
                else 
                {
                    sbName.Append(typeName); 
                } 

                comma = ", "; 
            }

            if ((callingConvention & CallingConventions.VarArgs) == CallingConventions.VarArgs)
            { 
                sbName.Append(comma);
                sbName.Append("..."); 
            } 

            return sbName.ToString(); 
        }

        internal virtual string ConstructName()
        { 
            // Serialization uses ToString to resolve MethodInfo overloads.
            StringBuilder sbName = new StringBuilder(Name); 
 
            sbName.Append("(");
            sbName.Append(ConstructParameters(GetParametersNoCopy(), CallingConvention)); 
            sbName.Append(")");

            return sbName.ToString();
        } 

        internal virtual Type[] GetParameterTypes() 
        { 
            ParameterInfo[] paramInfo = GetParametersNoCopy();
 
            Type[] parameterTypes = new Type[paramInfo.Length];
            for (int i = 0; i < paramInfo.Length; i++)
                parameterTypes[i] = paramInfo[i].ParameterType;
 
            return parameterTypes;
        } 
 
        [System.Security.SecurityCritical]  // auto-generated
        internal Object[] CheckArguments(Object[] parameters, Binder binder, 
            BindingFlags invokeAttr, CultureInfo culture, Signature sig)
        {
            int actualCount = (parameters != null) ? parameters.Length : 0;
            // copy the arguments in a different array so we detach from any user changes 
            Object[] copyOfParameters = new Object[actualCount];
 
            ParameterInfo[] p = null; 
            for (int i = 0; i < actualCount; i++)
            { 
                Object arg = parameters[i];
                RuntimeType argRT = sig.Arguments[i];

                if (arg == Type.Missing) 
                {
                    if (p == null) 
                        p = GetParametersNoCopy(); 
                    if (p[i].DefaultValue == System.DBNull.Value)
                        throw new ArgumentException(Environment.GetResourceString("Arg_VarMissNull"),"parameters"); 
                    arg = p[i].DefaultValue;
                }
                copyOfParameters[i] = argRT.CheckValue(arg, binder, culture, invokeAttr);
            } 

            return copyOfParameters; 
        } 
        #endregion
 
        void _MethodBase.GetTypeInfoCount(out uint pcTInfo)
        {
            throw new NotImplementedException();
        } 

        void _MethodBase.GetTypeInfo(uint iTInfo, uint lcid, IntPtr ppTInfo) 
        { 
            throw new NotImplementedException();
        } 

        void _MethodBase.GetIDsOfNames([In] ref Guid riid, IntPtr rgszNames, uint cNames, uint lcid, IntPtr rgDispId)
        {
            throw new NotImplementedException(); 
        }
 
        void _MethodBase.Invoke(uint dispIdMember, [In] ref Guid riid, uint lcid, short wFlags, IntPtr pDispParams, IntPtr pVarResult, IntPtr pExcepInfo, IntPtr puArgErr) 
        {
            throw new NotImplementedException(); 
        }
    }

 
    [Serializable]
    [ClassInterface(ClassInterfaceType.None)] 
    [ComDefaultInterface(typeof(_ConstructorInfo))] 
    [PermissionSetAttribute(SecurityAction.InheritanceDemand, Name = "FullTrust")]
    [System.Runtime.InteropServices.ComVisible(true)] 
    public abstract class ConstructorInfo : MethodBase, _ConstructorInfo
    {
        #region Static Members
        [System.Runtime.InteropServices.ComVisible(true)] 
        public readonly static String ConstructorName = ".ctor";
 
        [System.Runtime.InteropServices.ComVisible(true)] 
        public readonly static String TypeConstructorName = ".cctor";
        #endregion 

        #region Constructor
        protected ConstructorInfo() { }
        #endregion 

#if !FEATURE_CORECLR 
        public static bool operator ==(ConstructorInfo left, ConstructorInfo right) 
        {
            if (ReferenceEquals(left, right)) 
                return true;

            if ((object)left == null || (object)right == null ||
                left is RuntimeConstructorInfo || right is RuntimeConstructorInfo) 
            {
                return false; 
            } 
            return left.Equals(right);
        } 

        public static bool operator !=(ConstructorInfo left, ConstructorInfo right)
        {
            return !(left == right); 
        }
 
        public override bool Equals(object obj) 
        {
            return base.Equals(obj); 
        }

        public override int GetHashCode()
        { 
            return base.GetHashCode();
        } 
#endif // !FEATURE_CORECLR 

        #region Internal Members 
        internal virtual Type GetReturnType() { throw new NotImplementedException(); }
        #endregion

        #region MemberInfo Overrides 
        [System.Runtime.InteropServices.ComVisible(true)]
        public override MemberTypes MemberType { get { return System.Reflection.MemberTypes.Constructor; } } 
        #endregion 

        #region Public Abstract\Virtual Members 
        public abstract Object Invoke(BindingFlags invokeAttr, Binder binder, Object[] parameters, CultureInfo culture);
        #endregion

        #region Public Members 
        [DebuggerStepThroughAttribute]
        [Diagnostics.DebuggerHidden] 
        public Object Invoke(Object[] parameters) 
        {
            return Invoke(BindingFlags.Default, null, parameters, null); 
        }
        #endregion

        #region COM Interop Support 
        Type _ConstructorInfo.GetType()
        { 
            return base.GetType(); 
        }
 
        Object _ConstructorInfo.Invoke_2(Object obj, BindingFlags invokeAttr, Binder binder, Object[] parameters, CultureInfo culture)
        {
            return Invoke(obj, invokeAttr, binder, parameters, culture);
        } 

        Object _ConstructorInfo.Invoke_3(Object obj, Object[] parameters) 
        { 
            return Invoke(obj, parameters);
        } 

        Object _ConstructorInfo.Invoke_4(BindingFlags invokeAttr, Binder binder, Object[] parameters, CultureInfo culture)
        {
            return Invoke(invokeAttr, binder, parameters, culture); 
        }
 
        Object _ConstructorInfo.Invoke_5(Object[] parameters) 
        {
            return Invoke(parameters); 
        }

        void _ConstructorInfo.GetTypeInfoCount(out uint pcTInfo)
        { 
            throw new NotImplementedException();
        } 
 
        void _ConstructorInfo.GetTypeInfo(uint iTInfo, uint lcid, IntPtr ppTInfo)
        { 
            throw new NotImplementedException();
        }

        void _ConstructorInfo.GetIDsOfNames([In] ref Guid riid, IntPtr rgszNames, uint cNames, uint lcid, IntPtr rgDispId) 
        {
            throw new NotImplementedException(); 
        } 

        void _ConstructorInfo.Invoke(uint dispIdMember, [In] ref Guid riid, uint lcid, short wFlags, IntPtr pDispParams, IntPtr pVarResult, IntPtr pExcepInfo, IntPtr puArgErr) 
        {
            throw new NotImplementedException();
        }
        #endregion 
    }
 
 
    [Serializable]
    [ClassInterface(ClassInterfaceType.None)] 
    [ComDefaultInterface(typeof(_MethodInfo))]
    [PermissionSetAttribute(SecurityAction.InheritanceDemand, Name = "FullTrust")]
    [System.Runtime.InteropServices.ComVisible(true)]
    public abstract class MethodInfo : MethodBase, _MethodInfo 
    {
        #region Constructor 
        protected MethodInfo() { } 
        #endregion
 
#if !FEATURE_CORECLR
        public static bool operator ==(MethodInfo left, MethodInfo right)
        {
            if (ReferenceEquals(left, right)) 
                return true;
 
            if ((object)left == null || (object)right == null || 
                left is RuntimeMethodInfo || right is RuntimeMethodInfo)
            { 
                return false;
            }
            return left.Equals(right);
        } 

        [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")] 
        public static bool operator !=(MethodInfo left, MethodInfo right) 
        {
            return !(left == right); 
        }

        public override bool Equals(object obj)
        { 
            return base.Equals(obj);
        } 
 
        public override int GetHashCode()
        { 
            return base.GetHashCode();
        }
#endif // !FEATURE_CORECLR
 
        #region MemberInfo Overrides
        public override MemberTypes MemberType { get { return System.Reflection.MemberTypes.Method; } } 
        #endregion 

        #region Public Abstract\Virtual Members 
        public virtual Type ReturnType { get { throw new NotImplementedException(); } }

        public virtual ParameterInfo ReturnParameter { get { throw new NotImplementedException(); } }
 
        public abstract ICustomAttributeProvider ReturnTypeCustomAttributes { get;  }
 
        public abstract MethodInfo GetBaseDefinition(); 

        [System.Runtime.InteropServices.ComVisible(true)] 
        public override Type[] GetGenericArguments() { throw new NotSupportedException(Environment.GetResourceString("NotSupported_SubclassOverride")); }

        [System.Runtime.InteropServices.ComVisible(true)]
        public virtual MethodInfo GetGenericMethodDefinition() { throw new NotSupportedException(Environment.GetResourceString("NotSupported_SubclassOverride")); } 

        public virtual MethodInfo MakeGenericMethod(params Type[] typeArguments) { throw new NotSupportedException(Environment.GetResourceString("NotSupported_SubclassOverride")); } 
        #endregion 

        Type _MethodInfo.GetType() 
        {
            return base.GetType();
        }
 
        void _MethodInfo.GetTypeInfoCount(out uint pcTInfo)
        { 
            throw new NotImplementedException(); 
        }
 
        void _MethodInfo.GetTypeInfo(uint iTInfo, uint lcid, IntPtr ppTInfo)
        {
            throw new NotImplementedException();
        } 

        void _MethodInfo.GetIDsOfNames([In] ref Guid riid, IntPtr rgszNames, uint cNames, uint lcid, IntPtr rgDispId) 
        { 
            throw new NotImplementedException();
        } 

        void _MethodInfo.Invoke(uint dispIdMember, [In] ref Guid riid, uint lcid, short wFlags, IntPtr pDispParams, IntPtr pVarResult, IntPtr pExcepInfo, IntPtr puArgErr)
        {
            throw new NotImplementedException(); 
        }
    } 
 

    [Serializable] 
    [ClassInterface(ClassInterfaceType.None)]
    [ComDefaultInterface(typeof(_FieldInfo))]
    [PermissionSetAttribute(SecurityAction.InheritanceDemand, Name = "FullTrust")]
    [System.Runtime.InteropServices.ComVisible(true)] 
    public abstract class FieldInfo : MemberInfo, _FieldInfo
    { 
        #region Static Members 
        [System.Security.SecuritySafeCritical]  // auto-generated
        public static FieldInfo GetFieldFromHandle(RuntimeFieldHandle handle) 
        {
            if (handle.IsNullHandle())
                throw new ArgumentException(Environment.GetResourceString("Argument_InvalidHandle"));
 
            FieldInfo f = RuntimeType.GetFieldInfo(handle.GetRuntimeFieldInfo());
 
            Type declaringType = f.DeclaringType; 
            if (declaringType != null && declaringType.IsGenericType)
                throw new ArgumentException(String.Format( 
                    CultureInfo.CurrentCulture, Environment.GetResourceString("Argument_FieldDeclaringTypeGeneric"),
                    f.Name, declaringType.GetGenericTypeDefinition()));

            return f; 
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated 
        [System.Runtime.InteropServices.ComVisible(false)]
        public static FieldInfo GetFieldFromHandle(RuntimeFieldHandle handle, RuntimeTypeHandle declaringType) 
        {
            if (handle.IsNullHandle())
                throw new ArgumentException(Environment.GetResourceString("Argument_InvalidHandle"));
 
            return RuntimeType.GetFieldInfo(declaringType.GetRuntimeType(), handle.GetRuntimeFieldInfo());
        } 
        #endregion 

        #region Constructor 
        protected FieldInfo() { }
        #endregion

#if !FEATURE_CORECLR 
        public static bool operator ==(FieldInfo left, FieldInfo right)
        { 
            if (ReferenceEquals(left, right)) 
                return true;
 
            if ((object)left == null || (object)right == null ||
                left is RuntimeFieldInfo || right is RuntimeFieldInfo)
            {
                return false; 
            }
            return left.Equals(right); 
        } 

        [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")] 
        public static bool operator !=(FieldInfo left, FieldInfo right)
        {
            return !(left == right);
        } 

        public override bool Equals(object obj) 
        { 
            return base.Equals(obj);
        } 

        public override int GetHashCode()
        {
            return base.GetHashCode(); 
        }
#endif // !FEATURE_CORECLR 
 
        #region MemberInfo Overrides
        public override MemberTypes MemberType { get { return System.Reflection.MemberTypes.Field; } } 
        #endregion

        #region Public Abstract\Virtual Members
 
        public virtual Type[] GetRequiredCustomModifiers()
        { 
            throw new NotImplementedException(); 
        }
 
        public virtual Type[] GetOptionalCustomModifiers()
        {
            throw new NotImplementedException();
        } 

        [CLSCompliant(false)] 
        public virtual void SetValueDirect(TypedReference obj, Object value) 
        {
            throw new NotSupportedException(Environment.GetResourceString("NotSupported_AbstractNonCLS")); 
        }

        [CLSCompliant(false)]
        public virtual Object GetValueDirect(TypedReference obj) 
        {
            throw new NotSupportedException(Environment.GetResourceString("NotSupported_AbstractNonCLS")); 
        } 

        public abstract RuntimeFieldHandle FieldHandle { get; } 

        public abstract Type FieldType { get; }

        public abstract Object GetValue(Object obj); 
        public virtual Object GetRawConstantValue() { throw new NotSupportedException(Environment.GetResourceString("NotSupported_AbstractNonCLS")); }
 
        public abstract void SetValue(Object obj, Object value, BindingFlags invokeAttr, Binder binder, CultureInfo culture); 

        public abstract FieldAttributes Attributes { get; } 
        #endregion

        #region Public Members
        [DebuggerStepThroughAttribute] 
        [Diagnostics.DebuggerHidden]
        public void SetValue(Object obj, Object value) 
        { 
            SetValue(obj, value, BindingFlags.Default, Type.DefaultBinder, null);
        } 

        public bool IsPublic { get { return(Attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Public; } }

        public bool IsPrivate { get { return(Attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Private; } } 

        public bool IsFamily { get { return(Attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Family; } } 
 
        public bool IsAssembly { get { return(Attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Assembly; } }
 
        public bool IsFamilyAndAssembly { get { return(Attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.FamANDAssem; } }

        public bool IsFamilyOrAssembly { get { return(Attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.FamORAssem; } }
 
        public bool IsStatic { get { return(Attributes & FieldAttributes.Static) != 0; } }
 
        public bool IsInitOnly { get { return(Attributes & FieldAttributes.InitOnly) != 0; } } 

        public bool IsLiteral { get { return(Attributes & FieldAttributes.Literal) != 0; } } 

        public bool IsNotSerialized { get { return(Attributes & FieldAttributes.NotSerialized) != 0; } }

        public bool IsSpecialName  { get { return(Attributes & FieldAttributes.SpecialName) != 0; } } 

        public bool IsPinvokeImpl { get { return(Attributes & FieldAttributes.PinvokeImpl) != 0; } } 
 
        public virtual bool IsSecurityCritical
        { 
            get { return FieldHandle.IsSecurityCritical(); }
        }

        public virtual bool IsSecuritySafeCritical 
        {
            get { return FieldHandle.IsSecuritySafeCritical(); } 
        } 

        public virtual bool IsSecurityTransparent 
        {
            get { return FieldHandle.IsSecurityTransparent(); }
        }
 
        #endregion
 
        Type _FieldInfo.GetType() 
        {
            return base.GetType(); 
        }

        void _FieldInfo.GetTypeInfoCount(out uint pcTInfo)
        { 
            throw new NotImplementedException();
        } 
 
        void _FieldInfo.GetTypeInfo(uint iTInfo, uint lcid, IntPtr ppTInfo)
        { 
            throw new NotImplementedException();
        }

        void _FieldInfo.GetIDsOfNames([In] ref Guid riid, IntPtr rgszNames, uint cNames, uint lcid, IntPtr rgDispId) 
        {
            throw new NotImplementedException(); 
        } 

        void _FieldInfo.Invoke(uint dispIdMember, [In] ref Guid riid, uint lcid, short wFlags, IntPtr pDispParams, IntPtr pVarResult, IntPtr pExcepInfo, IntPtr puArgErr) 
        {
            throw new NotImplementedException();
        }
    } 

 
    [Serializable] 
    [ClassInterface(ClassInterfaceType.None)]
    [ComDefaultInterface(typeof(_EventInfo))] 
    [PermissionSetAttribute(SecurityAction.InheritanceDemand, Name = "FullTrust")]
    [System.Runtime.InteropServices.ComVisible(true)]
    public abstract class EventInfo : MemberInfo, _EventInfo
    { 
        #region Constructor
        protected EventInfo() { } 
        #endregion 

#if !FEATURE_CORECLR 
        public static bool operator ==(EventInfo left, EventInfo right)
        {
            if (ReferenceEquals(left, right))
                return true; 

            if ((object)left == null || (object)right == null || 
                left is RuntimeEventInfo || right is RuntimeEventInfo) 
            {
                return false; 
            }
            return left.Equals(right);
        }
 
        public static bool operator !=(EventInfo left, EventInfo right)
        { 
            return !(left == right); 
        }
 
        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        } 

        public override int GetHashCode() 
        { 
            return base.GetHashCode();
        } 
#endif // !FEATURE_CORECLR

        #region MemberInfo Overrides
        public override MemberTypes MemberType { get { return MemberTypes.Event; } } 
        #endregion
 
        #region Public Abstract\Virtual Members 
        public virtual MethodInfo[] GetOtherMethods(bool nonPublic)
        { 
            throw new NotImplementedException();
        }

        public abstract MethodInfo GetAddMethod(bool nonPublic); 

        public abstract MethodInfo GetRemoveMethod(bool nonPublic); 
 
        public abstract MethodInfo GetRaiseMethod(bool nonPublic);
 
        public abstract EventAttributes Attributes { get; }
        #endregion

        #region Public Members 
        public MethodInfo[] GetOtherMethods() { return GetOtherMethods(false); }
 
        public MethodInfo GetAddMethod() { return GetAddMethod(false); } 

        public MethodInfo GetRemoveMethod() { return GetRemoveMethod(false); } 

        public MethodInfo GetRaiseMethod() { return GetRaiseMethod(false); }

        [DebuggerStepThroughAttribute] 
        [Diagnostics.DebuggerHidden]
        public virtual void AddEventHandler(Object target, Delegate handler) 
        { 
            MethodInfo addMethod = GetAddMethod();
 
            if (addMethod == null)
                throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_NoPublicAddMethod"));

            addMethod.Invoke(target, new object[] { handler }); 
        }
 
        [DebuggerStepThroughAttribute] 
        [Diagnostics.DebuggerHidden]
        public virtual void RemoveEventHandler(Object target, Delegate handler) 
        {
            MethodInfo removeMethod = GetRemoveMethod();

            if (removeMethod == null) 
                throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_NoPublicRemoveMethod"));
 
            removeMethod.Invoke(target, new object[] { handler }); 
        }
 
        public virtual Type EventHandlerType
        {
            get
            { 
                MethodInfo m = GetAddMethod(true);
 
                ParameterInfo[] p = m.GetParametersNoCopy(); 

                Type del = typeof(Delegate); 

                for (int i = 0; i < p.Length; i++)
                {
                    Type c = p[i].ParameterType; 

                    if (c.IsSubclassOf(del)) 
                        return c; 
                }
                return null; 
            }
        }
        public bool IsSpecialName
        { 
            get
            { 
                return(Attributes & EventAttributes.SpecialName) != 0; 
            }
        } 

        public virtual bool IsMulticast
        {
            get 
            {
                Type cl = EventHandlerType; 
                Type mc = typeof(MulticastDelegate); 
                return mc.IsAssignableFrom(cl);
            } 
        }
        #endregion

        Type _EventInfo.GetType() 
        {
            return base.GetType(); 
        } 

        void _EventInfo.GetTypeInfoCount(out uint pcTInfo) 
        {
            throw new NotImplementedException();
        }
 
        void _EventInfo.GetTypeInfo(uint iTInfo, uint lcid, IntPtr ppTInfo)
        { 
            throw new NotImplementedException(); 
        }
 
        void _EventInfo.GetIDsOfNames([In] ref Guid riid, IntPtr rgszNames, uint cNames, uint lcid, IntPtr rgDispId)
        {
            throw new NotImplementedException();
        } 

        void _EventInfo.Invoke(uint dispIdMember, [In] ref Guid riid, uint lcid, short wFlags, IntPtr pDispParams, IntPtr pVarResult, IntPtr pExcepInfo, IntPtr puArgErr) 
        { 
            throw new NotImplementedException();
        } 
    }


    [Serializable] 
    [ClassInterface(ClassInterfaceType.None)]
    [ComDefaultInterface(typeof(_PropertyInfo))] 
    [PermissionSetAttribute(SecurityAction.InheritanceDemand, Name = "FullTrust")] 
    [System.Runtime.InteropServices.ComVisible(true)]
    public abstract class PropertyInfo : MemberInfo, _PropertyInfo 
    {
        #region Constructor
        protected PropertyInfo() { }
        #endregion 

#if !FEATURE_CORECLR 
        public static bool operator ==(PropertyInfo left, PropertyInfo right) 
        {
            if (ReferenceEquals(left, right)) 
                return true;

            if ((object)left == null || (object)right == null ||
                left is RuntimePropertyInfo || right is RuntimePropertyInfo) 
            {
                return false; 
            } 
            return left.Equals(right);
        } 

        [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
        public static bool operator !=(PropertyInfo left, PropertyInfo right)
        { 
            return !(left == right);
        } 
 
        public override bool Equals(object obj)
        { 
            return base.Equals(obj);
        }

        public override int GetHashCode() 
        {
            return base.GetHashCode(); 
        } 
#endif // !FEATURE_CORECLR
 
        #region MemberInfo Overrides
        public override MemberTypes MemberType { get { return System.Reflection.MemberTypes.Property; } }
        #endregion
 
        #region Public Abstract\Virtual Members
        public virtual object GetConstantValue() 
        { 
            throw new NotImplementedException();
        } 

        public virtual object GetRawConstantValue()
        {
            throw new NotImplementedException(); 
        }
 
        public abstract Type PropertyType { get; } 

        public abstract void SetValue(Object obj, Object value, BindingFlags invokeAttr, Binder binder, Object[] index, CultureInfo culture); 

        public abstract MethodInfo[] GetAccessors(bool nonPublic);

        public abstract MethodInfo GetGetMethod(bool nonPublic); 

        public abstract MethodInfo GetSetMethod(bool nonPublic); 
 
        public abstract ParameterInfo[] GetIndexParameters();
 
        public abstract PropertyAttributes Attributes { get; }

        public abstract bool CanRead { get; }
 
        public abstract bool CanWrite { get; }
 
        [DebuggerStepThroughAttribute] 
        [Diagnostics.DebuggerHidden]
        public virtual Object GetValue(Object obj,Object[] index) 
        {
            return GetValue(obj, BindingFlags.Default, null, index, null);
        }
 
        public abstract Object GetValue(Object obj, BindingFlags invokeAttr, Binder binder, Object[] index, CultureInfo culture);
 
        [DebuggerStepThroughAttribute] 
        [Diagnostics.DebuggerHidden]
#if !FEATURE_CORECLR 
        [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
#endif
        public virtual void SetValue(Object obj, Object value, Object[] index)
        { 
            SetValue(obj, value, BindingFlags.Default, null, index, null);
        } 
        #endregion 

        #region Public Members 
        public virtual Type[] GetRequiredCustomModifiers() { return new Type[0]; }

        public virtual Type[] GetOptionalCustomModifiers() { return new Type[0]; }
 
        public MethodInfo[] GetAccessors() { return GetAccessors(false); }
 
        public MethodInfo GetGetMethod() { return GetGetMethod(false); } 

        public MethodInfo GetSetMethod() { return GetSetMethod(false); } 

        public bool IsSpecialName { get { return(Attributes & PropertyAttributes.SpecialName) != 0; } }
        #endregion
 
        Type _PropertyInfo.GetType()
        { 
            return base.GetType(); 
        }
 
        void _PropertyInfo.GetTypeInfoCount(out uint pcTInfo)
        {
            throw new NotImplementedException();
        } 

        void _PropertyInfo.GetTypeInfo(uint iTInfo, uint lcid, IntPtr ppTInfo) 
        { 
            throw new NotImplementedException();
        } 

        void _PropertyInfo.GetIDsOfNames([In] ref Guid riid, IntPtr rgszNames, uint cNames, uint lcid, IntPtr rgDispId)
        {
            throw new NotImplementedException(); 
        }
 
        void _PropertyInfo.Invoke(uint dispIdMember, [In] ref Guid riid, uint lcid, short wFlags, IntPtr pDispParams, IntPtr pVarResult, IntPtr pExcepInfo, IntPtr puArgErr) 
        {
            throw new NotImplementedException(); 
        }
    }

} 

namespace System.Reflection 
{ 
    using System;
    using System.Reflection; 
    using System.Runtime;
    using System.Runtime.ConstrainedExecution;
    using System.Globalization;
    using System.Threading; 
    using System.Diagnostics;
    using System.Security.Permissions; 
    using System.Collections; 
    using System.Collections.Generic;
    using System.Runtime.CompilerServices; 
    using System.Security;
    using System.Text;
    using System.Runtime.InteropServices;
    using System.Runtime.Serialization; 
    using System.Runtime.Versioning;
    using System.Reflection.Cache; 
    using System.Diagnostics.Contracts; 
    using RuntimeTypeCache = System.RuntimeType.RuntimeTypeCache;
    using MdToken = System.Reflection.MetadataToken; 

    internal enum MemberListType
    {
        All, 
        CaseSensitive,
        CaseInsensitive, 
        HandleToInfo 
    }
 
    [Serializable]
    internal sealed class RuntimeMethodInfo : MethodInfo, ISerializable, IRuntimeMethodInfo
    {
        #region Private Data Members 
        private IntPtr m_handle;
        private RuntimeTypeCache m_reflectedTypeCache; 
        private string m_name; 
        private string m_toString;
        private ParameterInfo[] m_parameters; 
        private ParameterInfo m_returnParameter;
        private BindingFlags m_bindingFlags;
        private MethodAttributes m_methodAttributes;
        private Signature m_signature; 
        private RuntimeType m_declaringType;
        private object m_keepalive; 
        private INVOCATION_FLAGS m_invocationFlags; 

        private INVOCATION_FLAGS InvocationFlags 
        {
            [System.Security.SecurityCritical]  // auto-generated
            get
            { 
                if ((m_invocationFlags & INVOCATION_FLAGS.INVOCATION_FLAGS_INITIALIZED) == 0)
                { 
                    INVOCATION_FLAGS invocationFlags = INVOCATION_FLAGS.INVOCATION_FLAGS_UNKNOWN; 

                    Type declaringType = DeclaringType; 

                    //
                    // first take care of all the NO_INVOKE cases.
                    if (ContainsGenericParameters || 
                         ReturnType.IsByRef ||
                         (declaringType != null && declaringType.ContainsGenericParameters) || 
                         ((CallingConvention & CallingConventions.VarArgs) == CallingConventions.VarArgs) || 
                         ((Attributes & MethodAttributes.RequireSecObject) == MethodAttributes.RequireSecObject))
                    { 
                        // We don't need other flags if this method cannot be invoked
                        invocationFlags = INVOCATION_FLAGS.INVOCATION_FLAGS_NO_INVOKE;
                    }
                    else 
                    {
                        // this should be an invocable method, determine the other flags that participate in invocation 
                        invocationFlags = RuntimeMethodHandle.GetSecurityFlags(this); 

                        if ((invocationFlags & INVOCATION_FLAGS.INVOCATION_FLAGS_NEED_SECURITY) == 0) 
                        {
                            if ( (Attributes & MethodAttributes.MemberAccessMask) != MethodAttributes.Public ||
                                 (declaringType != null && declaringType.NeedsReflectionSecurityCheck) )
                            { 
                                // If method is non-public, or declaring type is not visible
                                invocationFlags |= INVOCATION_FLAGS.INVOCATION_FLAGS_NEED_SECURITY; 
                            } 
                            else if (IsGenericMethod)
                            { 
                                Type[] genericArguments = GetGenericArguments();

                                for (int i = 0; i < genericArguments.Length; i++)
                                { 
                                    if (genericArguments[i].NeedsReflectionSecurityCheck)
                                    { 
                                        invocationFlags |= INVOCATION_FLAGS.INVOCATION_FLAGS_NEED_SECURITY; 
                                        break;
                                    } 
                                }
                            }
                        }
                    } 

                    m_invocationFlags = invocationFlags | INVOCATION_FLAGS.INVOCATION_FLAGS_INITIALIZED; 
                } 

                return m_invocationFlags; 
            }
        }
        #endregion
 
        #region Constructor
        [System.Security.SecurityCritical]  // auto-generated 
        internal RuntimeMethodInfo( 
            RuntimeMethodHandleInternal handle, RuntimeType declaringType,
            RuntimeTypeCache reflectedTypeCache, MethodAttributes methodAttributes, BindingFlags bindingFlags, object keepalive) 
        {
            Contract.Ensures(!m_handle.IsNull());

            Contract.Assert(!handle.IsNullHandle()); 
            Contract.Assert(methodAttributes == RuntimeMethodHandle.GetAttributes(handle));
 
            m_bindingFlags = bindingFlags; 
            m_declaringType = declaringType;
            m_keepalive = keepalive; 
            m_handle = handle.Value;
            m_reflectedTypeCache = reflectedTypeCache;
            m_methodAttributes = methodAttributes;
        } 
        #endregion
 
        #region Legacy Remoting Cache 
        // The size of CachedData is accounted for by BaseObjectWithCachedData in object.h.
        // This member is currently being used by Remoting for caching remoting data. If you 
        // need to cache data here, talk to the Remoting team to work out a mechanism, so that
        // both caching systems can happily work together.
        private InternalCache m_cachedData;
 
        internal InternalCache RemotingCache
        { 
            get 
            {
                // This grabs an internal copy of m_cachedData and uses 
                // that instead of looking at m_cachedData directly because
                // the cache may get cleared asynchronously.  This prevents
                // us from having to take a lock.
                InternalCache cache = m_cachedData; 
                if (cache == null)
                { 
                    cache = new InternalCache("MemberInfo"); 
                    InternalCache ret = Interlocked.CompareExchange(ref m_cachedData, cache, null);
                    if (ret != null) 
                        cache = ret;
                    GC.ClearCache += new ClearCacheHandler(OnCacheClear);
                }
                return cache; 
            }
        } 
 

        internal void OnCacheClear(Object sender, ClearCacheEventArgs cacheEventArgs) 
        {
            m_cachedData = null;
        }
        #endregion 

        #region Private Methods 
        RuntimeMethodHandleInternal IRuntimeMethodInfo.Value 
        {
            [System.Security.SecuritySafeCritical] 
#if !FEATURE_CORECLR
            [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
#endif
            get 
            {
                return new RuntimeMethodHandleInternal(m_handle); 
            } 
        }
 
        private RuntimeType ReflectedTypeInternal
        {
            get
            { 
                return m_reflectedTypeCache.GetRuntimeType();
            } 
        } 

        [System.Security.SecurityCritical]  // auto-generated 
        private ParameterInfo[] FetchNonReturnParameters()
        {
            if (m_parameters == null)
                m_parameters = RuntimeParameterInfo.GetParameters(this, this, Signature); 

            return m_parameters; 
        } 

        [System.Security.SecurityCritical]  // auto-generated 
        private ParameterInfo FetchReturnParameter()
        {
            if (m_returnParameter == null)
                m_returnParameter = RuntimeParameterInfo.GetReturnParameter(this, this, Signature); 

            return m_returnParameter; 
        } 
        #endregion
 
        #region Internal Members
        internal override string ConstructName()
        {
            // Serialization uses ToString to resolve MethodInfo overloads. 
            StringBuilder sbName = new StringBuilder(Name);
 
            if (IsGenericMethod) 
                sbName.Append(RuntimeMethodHandle.ConstructInstantiation(this));
 
            sbName.Append("(");
            sbName.Append(ConstructParameters(GetParametersNoCopy(), CallingConvention));
            sbName.Append(")");
 
            return sbName.ToString();
        } 
 
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
#if !FEATURE_CORECLR 
        [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
#endif
        internal override bool CacheEquals(object o)
        { 
            RuntimeMethodInfo m = o as RuntimeMethodInfo;
 
            if ((object)m == null) 
                return false;
 
            return m.m_handle == m_handle;
        }

        internal Signature Signature 
        {
            [System.Security.SecuritySafeCritical]  // auto-generated 
            get 
            {
                if (m_signature == null) 
                    m_signature = new Signature(this, m_declaringType);

                return m_signature;
            } 
        }
 
        internal BindingFlags BindingFlags { get { return m_bindingFlags; } } 

        // Differs from MethodHandle in that it will return a valid handle even for reflection only loaded types 
        internal RuntimeMethodHandle GetMethodHandle()
        {
            return new RuntimeMethodHandle(this);
        } 

        [System.Security.SecuritySafeCritical]  // auto-generated 
        internal RuntimeMethodInfo GetParentDefinition() 
        {
            if (!IsVirtual || m_declaringType.IsInterface) 
                return null;

            RuntimeType parent = (RuntimeType)m_declaringType.BaseType;
 
            if (parent == null)
                return null; 
 
            int slot = RuntimeMethodHandle.GetSlot(this);
 
            if (RuntimeTypeHandle.GetNumVirtuals(parent) <= slot)
                return null;

            return (RuntimeMethodInfo)RuntimeType.GetMethodBase(parent, RuntimeTypeHandle.GetMethodAt(parent, slot)); 
        }
 
        // Unlike DeclaringType, this will return a valid type even for global methods 
        internal RuntimeType GetDeclaringTypeInternal()
        { 
            return m_declaringType;
        }

        #endregion 

        #region Object Overrides 
        [System.Security.SecuritySafeCritical]  // auto-generated 
        public override String ToString()
        { 
            if (m_toString == null)
                m_toString = ReturnType.SigToString() + " " + ConstructName();

            return m_toString; 
        }
 
        public override int GetHashCode() 
        {
            // See RuntimeMethodInfo.Equals() below. 
            if (IsGenericMethod)
                return ValueType.GetHashCodeOfPtr(m_handle);
            else
                return base.GetHashCode(); 
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated 
        public override bool Equals(object obj)
        { 
            if (!IsGenericMethod)
                return obj == (object)this;

            // We cannot do simple object identity comparisons for generic methods. 
            // Equals will be called in CerHashTable when RuntimeType+RuntimeTypeCache.GetGenericMethodInfo()
            // retrive items from and insert items into s_methodInstantiations which is a CerHashtable. 
            // 

            RuntimeMethodInfo mi = obj as RuntimeMethodInfo; 

            if (mi == null || !mi.IsGenericMethod)
                return false;
 
            // now we know that both operands are generic methods
 
            IRuntimeMethodInfo handle1 = RuntimeMethodHandle.StripMethodInstantiation(this); 
            IRuntimeMethodInfo handle2 = RuntimeMethodHandle.StripMethodInstantiation(mi);
            if (handle1.Value.Value != handle2.Value.Value) 
                return false;

            Type[] lhs = GetGenericArguments();
            Type[] rhs = mi.GetGenericArguments(); 

            if (lhs.Length != rhs.Length) 
                return false; 

            for (int i = 0; i < lhs.Length; i++) 
            {
                if (lhs[i] != rhs[i])
                    return false;
            } 

            if (DeclaringType != mi.DeclaringType) 
                return false; 

            if (ReflectedType != mi.ReflectedType) 
                return false;

            return true;
        } 
        #endregion
 
        #region ICustomAttributeProvider 
        [System.Security.SecuritySafeCritical]  // auto-generated
        public override Object[] GetCustomAttributes(bool inherit) 
        {
            return CustomAttribute.GetCustomAttributes(this, typeof(object) as RuntimeType as RuntimeType, inherit);
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated
        public override Object[] GetCustomAttributes(Type attributeType, bool inherit) 
        { 
            if (attributeType == null)
                throw new ArgumentNullException("attributeType"); 
            Contract.EndContractBlock();

            RuntimeType attributeRuntimeType = attributeType.UnderlyingSystemType as RuntimeType;
 
            if (attributeRuntimeType == null)
                throw new ArgumentException(Environment.GetResourceString("Arg_MustBeType"),"attributeType"); 
 
            return CustomAttribute.GetCustomAttributes(this, attributeRuntimeType, inherit);
        } 

        public override bool IsDefined(Type attributeType, bool inherit)
        {
            if (attributeType == null) 
                throw new ArgumentNullException("attributeType");
            Contract.EndContractBlock(); 
 
            RuntimeType attributeRuntimeType = attributeType.UnderlyingSystemType as RuntimeType;
 
            if (attributeRuntimeType == null)
                throw new ArgumentException(Environment.GetResourceString("Arg_MustBeType"),"attributeType");

            return CustomAttribute.IsDefined(this, attributeRuntimeType, inherit); 
        }
 
        public override IList<CustomAttributeData> GetCustomAttributesData() 
        {
            return CustomAttributeData.GetCustomAttributesInternal(this); 
        }
        #endregion

        #region MemberInfo Overrides 
        public override String Name
        { 
            [System.Security.SecuritySafeCritical]  // auto-generated 
#if !FEATURE_CORECLR
            [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")] 
#endif
            get
            {
                if (m_name == null) 
                    m_name = RuntimeMethodHandle.GetName(this);
 
                return m_name; 
            }
        } 

        public override Type DeclaringType
        {
#if !FEATURE_CORECLR 
            [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
#endif 
            get 
            {
                if (m_reflectedTypeCache.IsGlobal) 
                    return null;

                return m_declaringType;
            } 
        }
 
        public override Type ReflectedType 
        {
            get 
            {
                if (m_reflectedTypeCache.IsGlobal)
                    return null;
 
                return m_reflectedTypeCache.RuntimeType;
            } 
        } 

        public override MemberTypes MemberType { get { return MemberTypes.Method; } } 
        public override int MetadataToken
        {
            [System.Security.SecuritySafeCritical]  // auto-generated
#if !FEATURE_CORECLR 
            [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
#endif 
            get { return RuntimeMethodHandle.GetMethodDef(this); } 
        }
        public override Module Module { get { return GetRuntimeModule(); } } 
        internal RuntimeModule GetRuntimeModule() { return m_declaringType.GetRuntimeModule(); }

        public override bool IsSecurityCritical
        { 
            get { return RuntimeMethodHandle.IsSecurityCritical(this); }
        } 
        public override bool IsSecuritySafeCritical 
        {
            get { return RuntimeMethodHandle.IsSecuritySafeCritical(this); } 
        }
        public override bool IsSecurityTransparent
        {
            get { return RuntimeMethodHandle.IsSecurityTransparent(this); } 
        }
        #endregion 
 
        #region MethodBase Overrides
        [System.Security.SecuritySafeCritical]  // auto-generated 
#if !FEATURE_CORECLR
        [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
#endif
        internal override ParameterInfo[] GetParametersNoCopy() 
        {
            FetchNonReturnParameters(); 
 
            return m_parameters;
        } 

        [System.Security.SecuritySafeCritical]  // auto-generated
        [Pure]
        public override ParameterInfo[] GetParameters() 
        {
            FetchNonReturnParameters(); 
 
            if (m_parameters.Length == 0)
                return m_parameters; 

            ParameterInfo[] ret = new ParameterInfo[m_parameters.Length];

            Array.Copy(m_parameters, ret, m_parameters.Length); 

            return ret; 
        } 

        public override MethodImplAttributes GetMethodImplementationFlags() 
        {
            return RuntimeMethodHandle.GetImplAttributes(this);
        }
 
        internal bool IsOverloaded
        { 
            get 
            {
                return m_reflectedTypeCache.GetMethodList(MemberListType.CaseSensitive, Name).Count > 1; 
            }
        }

        public override RuntimeMethodHandle MethodHandle 
        {
            [System.Security.SecuritySafeCritical]  // auto-generated 
            get 
            {
                Type declaringType = DeclaringType; 
                if ((declaringType == null && Module.Assembly.ReflectionOnly) || declaringType is ReflectionOnlyType)
                    throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_NotAllowedInReflectionOnly"));
                return new RuntimeMethodHandle(this);
            } 
        }
 
        public override MethodAttributes Attributes { get { return m_methodAttributes; } } 

        public override CallingConventions CallingConvention 
        {
#if !FEATURE_CORECLR
            [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
#endif 
            get
            { 
                return Signature.CallingConvention; 
            }
        } 

        [System.Security.SecuritySafeCritical] // overrides SafeCritical member
        [ReflectionPermissionAttribute(SecurityAction.Demand, Flags = ReflectionPermissionFlag.MemberAccess)]
        public override MethodBody GetMethodBody() 
        {
            MethodBody mb = RuntimeMethodHandle.GetMethodBody(this, m_reflectedTypeCache.RuntimeType); 
            if (mb != null) 
                mb.m_methodBase = this;
            return mb; 
        }
        #endregion

        #region Invocation Logic(On MemberBase) 
        private void CheckConsistency(Object target)
        { 
            // only test instance methods 
            if ((m_methodAttributes & MethodAttributes.Static) != MethodAttributes.Static)
            { 
                if (!m_declaringType.IsInstanceOfType(target))
                {
                    if (target == null)
                        throw new TargetException(Environment.GetResourceString("RFLCT.Targ_StatMethReqTarg")); 
                    else
                        throw new TargetException(Environment.GetResourceString("RFLCT.Targ_ITargMismatch")); 
                } 
            }
        } 

        [System.Security.SecuritySafeCritical]
        private void ThrowNoInvokeException()
        { 
            // method is ReflectionOnly
            Type declaringType = DeclaringType; 
            if ((declaringType == null && Module.Assembly.ReflectionOnly) || declaringType is ReflectionOnlyType) 
            {
                throw new InvalidOperationException(Environment.GetResourceString("Arg_ReflectionOnlyInvoke")); 
            }
            // method is on a class that contains stack pointers
            else if ((InvocationFlags & INVOCATION_FLAGS.INVOCATION_FLAGS_CONTAINS_STACK_POINTERS) != 0)
            { 
                throw new NotSupportedException();
            } 
            // method is vararg 
            else if ((CallingConvention & CallingConventions.VarArgs) == CallingConventions.VarArgs)
            { 
                throw new NotSupportedException();
            }
            // method is generic or on a generic class
            else if (DeclaringType.ContainsGenericParameters || ContainsGenericParameters) 
            {
                throw new InvalidOperationException(Environment.GetResourceString("Arg_UnboundGenParam")); 
            } 
            // method is abstract class
            else if (IsAbstract) 
            {
                throw new MemberAccessException();
            }
            // ByRef return are not allowed in reflection 
            else if (ReturnType.IsByRef)
            { 
                throw new NotSupportedException(Environment.GetResourceString("NotSupported_ByRefReturn")); 
            }
 
            throw new TargetException();
        }

        [System.Security.SecuritySafeCritical]  // auto-generated 
        [DebuggerStepThroughAttribute]
        [Diagnostics.DebuggerHidden] 
        public override Object Invoke(Object obj, BindingFlags invokeAttr, Binder binder, Object[] parameters, CultureInfo culture) 
        {
            return Invoke(obj, invokeAttr, binder, parameters, culture, false); 
        }

        [System.Security.SecurityCritical]  // auto-generated
        [DebuggerStepThroughAttribute] 
        [Diagnostics.DebuggerHidden]
        internal object Invoke(Object obj, BindingFlags invokeAttr, Binder binder, Object[] parameters, CultureInfo culture, bool skipVisibilityChecks) 
        { 
            // get the signature
            int formalCount = Signature.Arguments.Length; 
            int actualCount =(parameters != null) ? parameters.Length : 0;

            INVOCATION_FLAGS invocationFlags = InvocationFlags;
 
            // INVOCATION_FLAGS_CONTAINS_STACK_POINTERS means that the struct (either the declaring type or the return type)
            // contains pointers that point to the stack. This is either a ByRef or a TypedReference. These structs cannot 
            // be boxed and thus cannot be invoked through reflection which only deals with boxed value type objects. 
            if ((invocationFlags & (INVOCATION_FLAGS.INVOCATION_FLAGS_NO_INVOKE | INVOCATION_FLAGS.INVOCATION_FLAGS_CONTAINS_STACK_POINTERS)) != 0)
                ThrowNoInvokeException(); 

            // check basic method consistency. This call will throw if there are problems in the target/method relationship
            CheckConsistency(obj);
 
            if (formalCount != actualCount)
                throw new TargetParameterCountException(Environment.GetResourceString("Arg_ParmCnt")); 
 
            // Don't allow more than 65535 parameters.
            if (actualCount > UInt16.MaxValue) 
                throw new TargetParameterCountException(Environment.GetResourceString("NotSupported_TooManyArgs"));

            if (!skipVisibilityChecks && (invocationFlags &(INVOCATION_FLAGS.INVOCATION_FLAGS_RISKY_METHOD | INVOCATION_FLAGS.INVOCATION_FLAGS_NEED_SECURITY)) != 0)
            { 
#if !FEATURE_CORECLR
                if ((invocationFlags & INVOCATION_FLAGS.INVOCATION_FLAGS_RISKY_METHOD) != 0) 
                    CodeAccessPermission.Demand(PermissionType.ReflectionMemberAccess); 

                if ((invocationFlags & INVOCATION_FLAGS.INVOCATION_FLAGS_NEED_SECURITY)  != 0) 
#endif // !FEATURE_CORECLR
                    RuntimeMethodHandle.PerformSecurityCheck(obj, this, m_declaringType, (uint)m_invocationFlags);
            }
 
            // if we are here we passed all the previous checks. Time to look at the arguments
            RuntimeType declaringType = null; 
            if (!m_reflectedTypeCache.IsGlobal) 
                declaringType = m_declaringType;
 
            if (actualCount == 0)
                return RuntimeMethodHandle.InvokeMethodFast(this, obj, null, Signature, m_methodAttributes, declaringType);

            Object[] arguments = CheckArguments(parameters, binder, invokeAttr, culture, Signature); 

            Object retValue = RuntimeMethodHandle.InvokeMethodFast(this, obj, arguments, Signature, m_methodAttributes, declaringType); 
 
            // copy out. This should be made only if ByRef are present.
            for(int index = 0; index < actualCount; index++) 
                parameters[index] = arguments[index];

            return retValue;
        } 
        #endregion
 
        #region MethodInfo Overrides 
        public override Type ReturnType
        { 
#if !FEATURE_CORECLR
            [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
#endif
            get { return Signature.ReturnType; } 
        }
 
        public override ICustomAttributeProvider ReturnTypeCustomAttributes 
        {
            get { return ReturnParameter; } 
        }

        public override ParameterInfo ReturnParameter
        { 
            [System.Security.SecuritySafeCritical]  // auto-generated
            get 
            { 
                Contract.Ensures(m_returnParameter != null);
 
                FetchReturnParameter();
                return m_returnParameter as ParameterInfo;
            }
        } 

        [System.Security.SecuritySafeCritical]  // auto-generated 
        public override MethodInfo GetBaseDefinition() 
        {
            if (!IsVirtual || IsStatic || m_declaringType == null || m_declaringType.IsInterface) 
                return this;

            int slot = RuntimeMethodHandle.GetSlot(this);
            RuntimeType declaringType = (RuntimeType)DeclaringType; 
            RuntimeType baseDeclaringType = declaringType;
            RuntimeMethodHandleInternal baseMethodHandle = new RuntimeMethodHandleInternal(); 
 
            do {
                int cVtblSlots = RuntimeTypeHandle.GetNumVirtuals(declaringType); 

                if (cVtblSlots <= slot)
                    break;
 
                baseMethodHandle = RuntimeTypeHandle.GetMethodAt(declaringType, slot);
                baseDeclaringType = declaringType; 
 
                declaringType = (RuntimeType)declaringType.BaseType;
            } while (declaringType != null); 

            return(MethodInfo)RuntimeType.GetMethodBase(baseDeclaringType, baseMethodHandle);
        }
        #endregion 

        #region Generics 
        [System.Security.SecuritySafeCritical]  // auto-generated 
        public override MethodInfo MakeGenericMethod(params Type[] methodInstantiation)
        { 
          if (methodInstantiation == null)
                throw new ArgumentNullException("methodInstantiation");
          Contract.EndContractBlock();
 
            RuntimeType[] methodInstantionRuntimeType = new RuntimeType[methodInstantiation.Length];
 
            if (!IsGenericMethodDefinition) 
                throw new InvalidOperationException(
                    Environment.GetResourceString("Arg_NotGenericMethodDefinition", this)); 

            for (int i = 0; i < methodInstantiation.Length; i++)
            {
                Type methodInstantiationElem = methodInstantiation[i]; 

                if (methodInstantiationElem == null) 
                    throw new ArgumentNullException(); 

                RuntimeType rtMethodInstantiationElem = methodInstantiationElem as RuntimeType; 

                if (rtMethodInstantiationElem == null)
                {
#if FEATURE_REFLECTION_EMIT_REFACTORING 
                    throw new ArgumentException(Environment.GetResourceString("Arg_MustAllBeRuntimeType"));
#else //FEATURE_REFLECTION_EMIT_REFACTORING 
                    Type[] methodInstantiationCopy = new Type[methodInstantiation.Length]; 
                    for (int iCopy = 0; iCopy < methodInstantiation.Length; iCopy++)
                        methodInstantiationCopy[iCopy] = methodInstantiation[iCopy]; 
                    methodInstantiation = methodInstantiationCopy;
                    return System.Reflection.Emit.MethodBuilderInstantiation.MakeGenericMethod(this, methodInstantiation);
#endif //FEATURE_REFLECTION_EMIT_REFACTORING
                } 

                methodInstantionRuntimeType[i] = rtMethodInstantiationElem; 
            } 

            RuntimeType[] genericParameters = GetGenericArgumentsInternal(); 

            RuntimeType.SanityCheckGenericArguments(methodInstantionRuntimeType, genericParameters);

            MethodInfo ret = null; 

            try 
            { 
                ret = RuntimeType.GetMethodBase(m_reflectedTypeCache.RuntimeType,
                    RuntimeMethodHandle.GetStubIfNeeded(new RuntimeMethodHandleInternal(this.m_handle), m_declaringType, methodInstantionRuntimeType)) as MethodInfo; 
            }
            catch (VerificationException e)
            {
                RuntimeType.ValidateGenericArguments(this, methodInstantionRuntimeType, e); 
                throw;
            } 
 
            return ret;
        } 

        [System.Security.SecuritySafeCritical]  // auto-generated
        internal RuntimeType[] GetGenericArgumentsInternal()
        { 
            return RuntimeMethodHandle.GetMethodInstantiationInternal(this);
        } 
 
        [System.Security.SecuritySafeCritical]  // auto-generated
        public override Type[] GetGenericArguments() 
        {
            Type[] types = RuntimeMethodHandle.GetMethodInstantiationPublic(this);

            if (types == null) 
            {
                types = new Type[0]; 
            } 
            return types;
        } 

        [System.Security.SecuritySafeCritical]  // auto-generated
        public override MethodInfo GetGenericMethodDefinition()
        { 
            if (!IsGenericMethod)
                throw new InvalidOperationException(); 
            Contract.EndContractBlock(); 

            return RuntimeType.GetMethodBase(m_declaringType, RuntimeMethodHandle.StripMethodInstantiation(this)) as MethodInfo; 
        }

        public override bool IsGenericMethod
        { 
            [System.Security.SecuritySafeCritical]  // auto-generated
#if !FEATURE_CORECLR 
            [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")] 
#endif
            get { return RuntimeMethodHandle.HasMethodInstantiation(this); } 
        }

        public override bool IsGenericMethodDefinition
        { 
            [System.Security.SecuritySafeCritical]  // auto-generated
            get { return RuntimeMethodHandle.IsGenericMethodDefinition(this); } 
        } 

        public override bool ContainsGenericParameters 
        {
            get
            {
                if (DeclaringType != null && DeclaringType.ContainsGenericParameters) 
                    return true;
 
                if (!IsGenericMethod) 
                    return false;
 
                Type[] pis = GetGenericArguments();
                for (int i = 0; i < pis.Length; i++)
                {
                    if (pis[i].ContainsGenericParameters) 
                        return true;
                } 
 
                return false;
            } 
        }
        #endregion

        #region ISerializable Implementation 
        [System.Security.SecurityCritical]  // auto-generated
        public void GetObjectData(SerializationInfo info, StreamingContext context) 
        { 
            if (info == null)
                throw new ArgumentNullException("info"); 
            Contract.EndContractBlock();

            if (m_reflectedTypeCache.IsGlobal)
                throw new NotSupportedException(Environment.GetResourceString("NotSupported_GlobalMethodSerialization")); 

            MemberInfoSerializationHolder.GetSerializationInfo( 
            info, Name,  ReflectedTypeInternal, ToString(), MemberTypes.Method, 
                IsGenericMethod & !IsGenericMethodDefinition ? GetGenericArguments() : null);
        } 
        #endregion

        #region Legacy Internal
        internal static MethodBase InternalGetCurrentMethod(ref StackCrawlMark stackMark) 
        {
            IRuntimeMethodInfo method = RuntimeMethodHandle.GetCurrentMethod(ref stackMark); 
 
            if (method == null)
                return null; 

            return RuntimeType.GetMethodBase(method);
        }
        #endregion 
    }
 
 
    [Serializable]
    internal sealed class RuntimeConstructorInfo : ConstructorInfo, ISerializable, IRuntimeMethodInfo 
    {
        #region Private Data Members
        private RuntimeType m_declaringType;
        private RuntimeTypeCache m_reflectedTypeCache; 
        private string m_toString;
        private ParameterInfo[] m_parameters = null; // Created lazily when GetParameters() is called. 
#pragma warning disable 169 
        private object _empty1; // These empties are used to ensure that RuntimeConstructorInfo and RuntimeMethodInfo are have a layout which is sufficiently similar
        private object _empty2; 
        private object _empty3;
#pragma warning restore 169
        private IntPtr m_handle;
        private MethodAttributes m_methodAttributes; 
        private BindingFlags m_bindingFlags;
        private Signature m_signature; 
        private INVOCATION_FLAGS m_invocationFlags; 
        private INVOCATION_FLAGS InvocationFlags
        { 
            [System.Security.SecurityCritical]  // auto-generated
            get
            {
                if ((m_invocationFlags & INVOCATION_FLAGS.INVOCATION_FLAGS_INITIALIZED) == 0) 
                {
                    INVOCATION_FLAGS invocationFlags = INVOCATION_FLAGS.INVOCATION_FLAGS_IS_CTOR; // this is a given 
 
                    Type declaringType = DeclaringType;
 
                    //
                    // first take care of all the NO_INVOKE cases.
                    if ( declaringType == typeof(void) ||
                         (declaringType != null && declaringType.ContainsGenericParameters) || 
                         ((CallingConvention & CallingConventions.VarArgs) == CallingConventions.VarArgs) ||
                         ((Attributes & MethodAttributes.RequireSecObject) == MethodAttributes.RequireSecObject)) 
                    { 
                        // We don't need other flags if this method cannot be invoked
                        invocationFlags |= INVOCATION_FLAGS.INVOCATION_FLAGS_NO_INVOKE; 
                    }
                    else if (IsStatic || declaringType != null && declaringType.IsAbstract)
                    {
                        invocationFlags |= INVOCATION_FLAGS.INVOCATION_FLAGS_NO_CTOR_INVOKE; 
                    }
                    else 
                    { 
                        // this should be an invocable method, determine the other flags that participate in invocation
                        invocationFlags |= RuntimeMethodHandle.GetSecurityFlags(this); 

                        if ( (invocationFlags & INVOCATION_FLAGS.INVOCATION_FLAGS_NEED_SECURITY) == 0 &&
                             ((Attributes & MethodAttributes.MemberAccessMask) != MethodAttributes.Public ||
                              (declaringType != null && declaringType.NeedsReflectionSecurityCheck)) ) 
                        {
                            // If method is non-public, or declaring type is not visible 
                            invocationFlags |= INVOCATION_FLAGS.INVOCATION_FLAGS_NEED_SECURITY; 
                        }
 
                        // Check for attempt to create a delegate class, we demand unmanaged
                        // code permission for this since it's hard to validate the target address.
                        if (typeof(Delegate).IsAssignableFrom(DeclaringType))
                            invocationFlags |= INVOCATION_FLAGS.INVOCATION_FLAGS_IS_DELEGATE_CTOR; 
                    }
 
                    m_invocationFlags = invocationFlags | INVOCATION_FLAGS.INVOCATION_FLAGS_INITIALIZED; 
                }
 
                return m_invocationFlags;
            }
        }
        #endregion 

        #region Constructor 
        [System.Security.SecurityCritical]  // auto-generated 
        internal RuntimeConstructorInfo(
            RuntimeMethodHandleInternal handle, RuntimeType declaringType, RuntimeTypeCache reflectedTypeCache, 
            MethodAttributes methodAttributes, BindingFlags bindingFlags)
        {
            Contract.Ensures(methodAttributes == RuntimeMethodHandle.GetAttributes(handle));
 
            m_bindingFlags = bindingFlags;
            m_reflectedTypeCache = reflectedTypeCache; 
            m_declaringType = declaringType; 
            m_handle = handle.Value;
            m_methodAttributes = methodAttributes; 
        }
        #endregion

        #region Legacy Remoting Cache 
        // The size of CachedData is accounted for by BaseObjectWithCachedData in object.h.
        // This member is currently being used by Remoting for caching remoting data. If you 
        // need to cache data here, talk to the Remoting team to work out a mechanism, so that 
        // both caching systems can happily work together.
        private InternalCache m_cachedData; 

        internal InternalCache RemotingCache
        {
            get 
            {
                // This grabs an internal copy of m_cachedData and uses 
                // that instead of looking at m_cachedData directly because 
                // the cache may get cleared asynchronously.  This prevents
                // us from having to take a lock. 
                InternalCache cache = m_cachedData;
                if (cache == null)
                {
                    cache = new InternalCache("MemberInfo"); 
                    InternalCache ret = Interlocked.CompareExchange(ref m_cachedData, cache, null);
                    if (ret != null) 
                        cache = ret; 
                    GC.ClearCache += new ClearCacheHandler(OnCacheClear);
                } 
                return cache;
            }
        }
 

        internal void OnCacheClear(Object sender, ClearCacheEventArgs cacheEventArgs) 
        { 
            m_cachedData = null;
        } 
        #endregion

        #region NonPublic Methods
        RuntimeMethodHandleInternal IRuntimeMethodInfo.Value 
        {
            [System.Security.SecuritySafeCritical] 
#if !FEATURE_CORECLR 
            [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
#endif 
            get
            {
                return new RuntimeMethodHandleInternal(m_handle);
            } 
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated 
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
#if !FEATURE_CORECLR 
        [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
#endif
        internal override bool CacheEquals(object o)
        { 
            RuntimeConstructorInfo m = o as RuntimeConstructorInfo;
 
            if ((object)m == null) 
                return false;
 
            return m.m_handle == m_handle;
        }

        private Signature Signature 
        {
            [System.Security.SecuritySafeCritical]  // auto-generated 
            get 
            {
                if (m_signature == null) 
                    m_signature = new Signature(this, m_declaringType);

                return m_signature;
            } 
        }
 
        private RuntimeType ReflectedTypeInternal 
        {
            get 
            {
                return m_reflectedTypeCache.GetRuntimeType();
            }
        } 

        private void CheckConsistency(Object target) 
        { 
            if (target == null && IsStatic)
                return; 

            if (!m_declaringType.IsInstanceOfType(target))
            {
                if (target == null) 
                    throw new TargetException(Environment.GetResourceString("RFLCT.Targ_StatMethReqTarg"));
 
                throw new TargetException(Environment.GetResourceString("RFLCT.Targ_ITargMismatch")); 
            }
        } 

        internal BindingFlags BindingFlags { get { return m_bindingFlags; } }

        // Differs from MethodHandle in that it will return a valid handle even for reflection only loaded types 
        internal RuntimeMethodHandle GetMethodHandle()
        { 
            return new RuntimeMethodHandle(this); 
        }
 
        internal bool IsOverloaded
        {
            get
            { 
                return m_reflectedTypeCache.GetConstructorList(MemberListType.CaseSensitive, Name).Count > 1;
            } 
        } 
        #endregion
 
        #region Object Overrides
        [System.Security.SecuritySafeCritical]  // auto-generated
        public override String ToString()
        { 
            if (m_toString == null)
                m_toString = "Void " + ConstructName(); 
 
            return m_toString;
        } 
        #endregion

        #region ICustomAttributeProvider
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
 
        [System.Security.SecuritySafeCritical]  // auto-generated
        public override bool IsDefined(Type attributeType, bool inherit) 
        { 
            if (attributeType == null)
                throw new ArgumentNullException("attributeType"); 
            Contract.EndContractBlock();

            RuntimeType attributeRuntimeType = attributeType.UnderlyingSystemType as RuntimeType;
 
            if (attributeRuntimeType == null)
                throw new ArgumentException(Environment.GetResourceString("Arg_MustBeType"),"attributeType"); 
 
            return CustomAttribute.IsDefined(this, attributeRuntimeType);
        } 

        public override IList<CustomAttributeData> GetCustomAttributesData()
        {
            return CustomAttributeData.GetCustomAttributesInternal(this); 
        }
        #endregion 
 

        #region MemberInfo Overrides 
        public override String Name
        {
            [System.Security.SecuritySafeCritical]  // auto-generated
            get { return RuntimeMethodHandle.GetName(this); } 
        }
[System.Runtime.InteropServices.ComVisible(true)] 
        public override MemberTypes MemberType { get { return MemberTypes.Constructor; } } 

        public override Type DeclaringType 
        {
#if !FEATURE_CORECLR
            [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
#endif 
            get
            { 
                return m_reflectedTypeCache.IsGlobal ? null : m_declaringType; 
            }
        } 

        public override Type ReflectedType { get { return m_reflectedTypeCache.IsGlobal ? null : m_reflectedTypeCache.RuntimeType; } }
        public override int MetadataToken
        { 
            [System.Security.SecuritySafeCritical]  // auto-generated
            get { return RuntimeMethodHandle.GetMethodDef(this); } 
        } 
        public override Module Module
        { 
            get { return GetRuntimeModule(); }
        }

        [System.Security.SecuritySafeCritical]  // auto-generated 
        internal RuntimeModule GetRuntimeModule() { return RuntimeTypeHandle.GetModule(m_declaringType); }
        #endregion 
 
        #region MethodBase Overrides
 
        // This seems to always returns System.Void.
        internal override Type GetReturnType() { return Signature.ReturnType; }

        [System.Security.SecuritySafeCritical]  // auto-generated 
        internal override ParameterInfo[] GetParametersNoCopy()
        { 
            if (m_parameters == null) 
                m_parameters = RuntimeParameterInfo.GetParameters(this, this, Signature);
 
            return m_parameters;
        }

        [Pure] 
        public override ParameterInfo[] GetParameters()
        { 
            ParameterInfo[] parameters = GetParametersNoCopy(); 

            if (parameters.Length == 0) 
                return parameters;

            ParameterInfo[] ret = new ParameterInfo[parameters.Length];
            Array.Copy(parameters, ret, parameters.Length); 
            return ret;
        } 
 
        public override MethodImplAttributes GetMethodImplementationFlags()
        { 
            return RuntimeMethodHandle.GetImplAttributes(this);
        }

        public override RuntimeMethodHandle MethodHandle 
        {
            [System.Security.SecuritySafeCritical]  // auto-generated 
#if !FEATURE_CORECLR 
            [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
#endif 
            get
            {
                Type declaringType = DeclaringType;
                if ((declaringType == null && Module.Assembly.ReflectionOnly) || declaringType is ReflectionOnlyType) 
                    throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_NotAllowedInReflectionOnly"));
                return new RuntimeMethodHandle(this); 
            } 
        }
 
        public override MethodAttributes Attributes
        {
            get
            { 
                return m_methodAttributes;
            } 
        } 

        public override CallingConventions CallingConvention 
        {
#if !FEATURE_CORECLR
            [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
#endif 
            get
            { 
                return Signature.CallingConvention; 
            }
        } 

        internal static void CheckCanCreateInstance(Type declaringType, bool isVarArg)
        {
            if (declaringType == null) 
                throw new ArgumentNullException("declaringType");
            Contract.EndContractBlock(); 
 
            // ctor is ReflectOnly
            if (declaringType is ReflectionOnlyType) 
                throw new InvalidOperationException(Environment.GetResourceString("Arg_ReflectionOnlyInvoke"));

            // ctor is declared on interface class
            else if (declaringType.IsInterface) 
                throw new MemberAccessException(
                    String.Format(CultureInfo.CurrentUICulture, Environment.GetResourceString("Acc_CreateInterfaceEx"), declaringType)); 
 
            // ctor is on an abstract class
            else if (declaringType.IsAbstract) 
                throw new MemberAccessException(
                    String.Format(CultureInfo.CurrentUICulture, Environment.GetResourceString("Acc_CreateAbstEx"), declaringType));

            // ctor is on a class that contains stack pointers 
            else if (declaringType.GetRootElementType() == typeof(ArgIterator))
                throw new NotSupportedException(); 
 
            // ctor is vararg
            else if (isVarArg) 
                throw new NotSupportedException();

            // ctor is generic or on a generic class
            else if (declaringType.ContainsGenericParameters) 
                throw new MemberAccessException(
                    String.Format(CultureInfo.CurrentUICulture, Environment.GetResourceString("Acc_CreateGenericEx"), declaringType)); 
 
            // ctor is declared on System.Void
            else if (declaringType == typeof(void)) 
                throw new MemberAccessException(Environment.GetResourceString("Access_Void"));
        }

        internal void ThrowNoInvokeException() 
        {
            CheckCanCreateInstance(DeclaringType, (CallingConvention & CallingConventions.VarArgs) == CallingConventions.VarArgs); 
 
            // ctor is .cctor
            if ((Attributes & MethodAttributes.Static) == MethodAttributes.Static) 
                throw new MemberAccessException(Environment.GetResourceString("Acc_NotClassInit"));

            throw new TargetException();
        } 

        [System.Security.SecuritySafeCritical]  // auto-generated 
        [DebuggerStepThroughAttribute] 
        [Diagnostics.DebuggerHidden]
        public override Object Invoke( 
            Object obj, BindingFlags invokeAttr, Binder binder, Object[] parameters, CultureInfo culture)
        {
            INVOCATION_FLAGS invocationFlags = InvocationFlags;
 
            if ((invocationFlags & INVOCATION_FLAGS.INVOCATION_FLAGS_NO_INVOKE) != 0)
                ThrowNoInvokeException(); 
 
            // check basic method consistency. This call will throw if there are problems in the target/method relationship
            CheckConsistency(obj); 

            if (obj != null)
            {
 
#if FEATURE_CORECLR
                // For unverifiable code, we require the caller to be critical. 
                // Adding the INVOCATION_FLAGS_NEED_SECURITY flag makes that check happen 
                invocationFlags |= INVOCATION_FLAGS.INVOCATION_FLAGS_NEED_SECURITY;
#else // FEATURE_CORECLR 
                new SecurityPermission(SecurityPermissionFlag.SkipVerification).Demand();
#endif // FEATURE_CORECLR

            } 

            if ((invocationFlags &(INVOCATION_FLAGS.INVOCATION_FLAGS_RISKY_METHOD | INVOCATION_FLAGS.INVOCATION_FLAGS_NEED_SECURITY)) != 0) 
            { 
#if !FEATURE_CORECLR
                if ((invocationFlags & INVOCATION_FLAGS.INVOCATION_FLAGS_RISKY_METHOD) != 0) 
                    CodeAccessPermission.Demand(PermissionType.ReflectionMemberAccess);
                if ((invocationFlags & INVOCATION_FLAGS.INVOCATION_FLAGS_NEED_SECURITY)  != 0)
#endif //#if !FEATURE_CORECLR
                    RuntimeMethodHandle.PerformSecurityCheck(obj, this, m_declaringType, (uint)m_invocationFlags); 
            }
 
            // get the signature 
            int formalCount = Signature.Arguments.Length;
            int actualCount =(parameters != null) ? parameters.Length : 0; 
            if (formalCount != actualCount)
                throw new TargetParameterCountException(Environment.GetResourceString("Arg_ParmCnt"));

            // if we are here we passed all the previous checks. Time to look at the arguments 
            if (actualCount > 0)
            { 
                Object[] arguments = CheckArguments(parameters, binder, invokeAttr, culture, Signature); 
                Object retValue = RuntimeMethodHandle.InvokeMethodFast(this, obj, arguments, Signature, m_methodAttributes, (RuntimeType)ReflectedType);
                // copy out. This should be made only if ByRef are present. 
                for(int index = 0; index < actualCount; index++)
                    parameters[index] = arguments[index];
                return retValue;
            } 
            return RuntimeMethodHandle.InvokeMethodFast(this, obj, null, Signature, m_methodAttributes, (RuntimeType)DeclaringType);
        } 
 

        [System.Security.SecuritySafeCritical] // overrides SC member 
        [ReflectionPermissionAttribute(SecurityAction.Demand, Flags = ReflectionPermissionFlag.MemberAccess)]
        public override MethodBody GetMethodBody()
        {
            MethodBody mb = RuntimeMethodHandle.GetMethodBody(this, m_reflectedTypeCache.RuntimeType); 
            if (mb != null)
                mb.m_methodBase = this; 
            return mb; 
        }
 
        public override bool IsSecurityCritical
        {
            get { return RuntimeMethodHandle.IsSecurityCritical(this); }
        } 

        public override bool IsSecuritySafeCritical 
        { 
            get { return RuntimeMethodHandle.IsSecuritySafeCritical(this); }
        } 

        public override bool IsSecurityTransparent
        {
            get { return RuntimeMethodHandle.IsSecurityTransparent(this); } 
        }
 
        public override bool ContainsGenericParameters 
        {
            get 
            {
                return (DeclaringType != null && DeclaringType.ContainsGenericParameters);
            }
        } 
        #endregion
 
        #region ConstructorInfo Overrides 
        [System.Security.SecuritySafeCritical]  // auto-generated
        [DebuggerStepThroughAttribute] 
        [Diagnostics.DebuggerHidden]
        public override Object Invoke(BindingFlags invokeAttr, Binder binder, Object[] parameters, CultureInfo culture)
        {
            INVOCATION_FLAGS invocationFlags = InvocationFlags; 

            // get the declaring TypeHandle early for consistent exceptions in IntrospectionOnly context 
            RuntimeTypeHandle declaringTypeHandle = m_declaringType.TypeHandle; 

            if ((invocationFlags & (INVOCATION_FLAGS.INVOCATION_FLAGS_NO_INVOKE | INVOCATION_FLAGS.INVOCATION_FLAGS_CONTAINS_STACK_POINTERS | INVOCATION_FLAGS.INVOCATION_FLAGS_NO_CTOR_INVOKE)) != 0) 
                ThrowNoInvokeException();

            if ((invocationFlags & (INVOCATION_FLAGS.INVOCATION_FLAGS_RISKY_METHOD | INVOCATION_FLAGS.INVOCATION_FLAGS_NEED_SECURITY | INVOCATION_FLAGS.INVOCATION_FLAGS_IS_DELEGATE_CTOR)) != 0)
            { 
#if !FEATURE_CORECLR
                if ((invocationFlags & INVOCATION_FLAGS.INVOCATION_FLAGS_RISKY_METHOD) != 0) 
                    CodeAccessPermission.Demand(PermissionType.ReflectionMemberAccess); 
                if ((invocationFlags & INVOCATION_FLAGS.INVOCATION_FLAGS_NEED_SECURITY)  != 0)
#endif //#if !FEATURE_CORECLR 
                    RuntimeMethodHandle.PerformSecurityCheck(null, this, m_declaringType, (uint)(m_invocationFlags | INVOCATION_FLAGS.INVOCATION_FLAGS_CONSTRUCTOR_INVOKE));
#if !FEATURE_CORECLR
                if ((invocationFlags & INVOCATION_FLAGS.INVOCATION_FLAGS_IS_DELEGATE_CTOR) != 0)
                    new SecurityPermission(SecurityPermissionFlag.UnmanagedCode).Demand(); 
#endif //#if !FEATURE_CORECLR
            } 
 
            // get the signature
            int formalCount = Signature.Arguments.Length; 
            int actualCount =(parameters != null) ? parameters.Length : 0;
            if (formalCount != actualCount)
                throw new TargetParameterCountException(Environment.GetResourceString("Arg_ParmCnt"));
 
            // We don't need to explicitly invoke the class constructor here,
            // JIT/NGen will insert the call to .cctor in the instance ctor. 
 
            // if we are here we passed all the previous checks. Time to look at the arguments
            if (actualCount > 0) 
            {
                Object[] arguments = CheckArguments(parameters, binder, invokeAttr, culture, Signature);
                Object retValue = RuntimeMethodHandle.InvokeConstructor(this, arguments, Signature, m_declaringType);
                // copy out. This should be made only if ByRef are present. 
                for(int index = 0; index < actualCount; index++)
                    parameters[index] = arguments[index]; 
                return retValue; 
            }
            return RuntimeMethodHandle.InvokeConstructor(this, null, Signature, m_declaringType); 
        }
        #endregion

        #region ISerializable Implementation 
        [System.Security.SecurityCritical]  // auto-generated
        public void GetObjectData(SerializationInfo info, StreamingContext context) 
        { 
            if (info==null)
                throw new ArgumentNullException("info"); 
            Contract.EndContractBlock();
            MemberInfoSerializationHolder.GetSerializationInfo(
                info, Name, ReflectedTypeInternal, ToString(), MemberTypes.Constructor);
        } 

        internal void SerializationInvoke(Object target, SerializationInfo info, StreamingContext context) 
        { 
            RuntimeMethodHandle.SerializationInvoke(this, target, Signature, info, context);
        } 
       #endregion
    }

 
    [Serializable]
    internal abstract class RuntimeFieldInfo : FieldInfo, ISerializable 
    { 
        #region Private Data Members
        private BindingFlags m_bindingFlags; 
        protected RuntimeTypeCache m_reflectedTypeCache;
        protected RuntimeType m_declaringType;
        #endregion
 
        #region Constructor
        protected RuntimeFieldInfo() 
        { 
            // Used for dummy head node during population
        } 
        protected RuntimeFieldInfo(RuntimeTypeCache reflectedTypeCache, RuntimeType declaringType, BindingFlags bindingFlags)
        {
            m_bindingFlags = bindingFlags;
            m_declaringType = declaringType; 
            m_reflectedTypeCache = reflectedTypeCache;
        } 
        #endregion 

        #region Legacy Remoting Cache 
        // The size of CachedData is accounted for by BaseObjectWithCachedData in object.h.
        // This member is currently being used by Remoting for caching remoting data. If you
        // need to cache data here, talk to the Remoting team to work out a mechanism, so that
        // both caching systems can happily work together. 
        private InternalCache m_cachedData;
 
        internal InternalCache RemotingCache 
        {
            get 
            {
                // This grabs an internal copy of m_cachedData and uses
                // that instead of looking at m_cachedData directly because
                // the cache may get cleared asynchronously.  This prevents 
                // us from having to take a lock.
                InternalCache cache = m_cachedData; 
                if (cache == null) 
                {
                    cache = new InternalCache("MemberInfo"); 
                    InternalCache ret = Interlocked.CompareExchange(ref m_cachedData, cache, null);
                    if (ret != null)
                        cache = ret;
                    GC.ClearCache += new ClearCacheHandler(OnCacheClear); 
                }
                return cache; 
            } 
        }
 

        internal void OnCacheClear(Object sender, ClearCacheEventArgs cacheEventArgs)
        {
            m_cachedData = null; 
        }
        #endregion 
 
        #region NonPublic Members
        internal BindingFlags BindingFlags { get { return m_bindingFlags; } } 
        private RuntimeType ReflectedTypeInternal
        {
            get
            { 
                return m_reflectedTypeCache.GetRuntimeType();
            } 
        } 
        internal RuntimeTypeHandle DeclaringTypeHandle
        { 
            get
            {
                Type declaringType = DeclaringType;
 
                if (declaringType == null)
                    return new RuntimeTypeHandle(GetRuntimeModule().RuntimeType); 
 
                return declaringType.GetTypeHandleInternal();
            } 
        }


        internal abstract RuntimeModule GetRuntimeModule(); 
        #endregion
 
        #region MemberInfo Overrides 
        public override MemberTypes MemberType { get { return MemberTypes.Field; } }
        public override Type ReflectedType { get { return m_reflectedTypeCache.IsGlobal ? null : m_reflectedTypeCache.RuntimeType; } } 

        public override Type DeclaringType
        {
#if !FEATURE_CORECLR 
            [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
#endif 
            get 
            {
                return m_reflectedTypeCache.IsGlobal ? null : m_declaringType; 
            }
        }

        public override Module Module { get { return GetRuntimeModule(); } } 
        #endregion
 
        #region Object Overrides 
        public unsafe override String ToString()
        { 
            return FieldType.SigToString() + " " + Name;
        }
        #endregion
 
        #region ICustomAttributeProvider
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
 
        [System.Security.SecuritySafeCritical]  // auto-generated 
        public override bool IsDefined(Type attributeType, bool inherit)
        { 
            if (attributeType == null)
                throw new ArgumentNullException("attributeType");
            Contract.EndContractBlock();
 
            RuntimeType attributeRuntimeType = attributeType.UnderlyingSystemType as RuntimeType;
 
            if (attributeRuntimeType == null) 
                throw new ArgumentException(Environment.GetResourceString("Arg_MustBeType"),"attributeType");
 
            return CustomAttribute.IsDefined(this, attributeRuntimeType);
        }

        public override IList<CustomAttributeData> GetCustomAttributesData() 
        {
            return CustomAttributeData.GetCustomAttributesInternal(this); 
        } 
        #endregion
 
        #region FieldInfo Overrides
        // All implemented on derived classes
        #endregion
 
        #region ISerializable Implementation
        [System.Security.SecurityCritical]  // auto-generated 
        public void GetObjectData(SerializationInfo info, StreamingContext context) 
        {
            if (info==null) 
                throw new ArgumentNullException("info");
            Contract.EndContractBlock();
            MemberInfoSerializationHolder.GetSerializationInfo(
                info, this.Name, this.ReflectedType, this.ToString(), MemberTypes.Field); 
        }
        #endregion 
    } 

 
    [Serializable]
    internal unsafe sealed class RtFieldInfo : RuntimeFieldInfo, IRuntimeFieldInfo
    {
        #region FCalls 
        [System.Security.SecurityCritical]  // auto-generated
        [ResourceExposure(ResourceScope.None)] 
        [MethodImplAttribute(MethodImplOptions.InternalCall)] 
        static private extern void PerformVisibilityCheckOnField(IntPtr field, Object target, RuntimeType declaringType, FieldAttributes attr, uint invocationFlags);
        #endregion 

        #region Private Data Members
        // agressive caching
        #if !FEATURE_CORECLR 
        [System.Runtime.ForceTokenStabilization]
        #endif //!FEATURE_CORECLR 
        private IntPtr m_fieldHandle; 
        private FieldAttributes m_fieldAttributes;
        // lazy caching 
        private string m_name;
        private RuntimeType m_fieldType;
        private INVOCATION_FLAGS m_invocationFlags;
        private INVOCATION_FLAGS InvocationFlags 
        {
            get 
            { 
                if ((m_invocationFlags & INVOCATION_FLAGS.INVOCATION_FLAGS_INITIALIZED) == 0)
                { 
                    Type declaringType = DeclaringType;
                    bool fIsReflectionOnlyType = (declaringType is ReflectionOnlyType);

                    INVOCATION_FLAGS invocationFlags = 0; 

                    // first take care of all the NO_INVOKE cases 
                    if ( 
                        (declaringType != null && declaringType.ContainsGenericParameters) ||
                        (declaringType == null && Module.Assembly.ReflectionOnly) || 
                        (fIsReflectionOnlyType)
                       )
                    {
                        invocationFlags |= INVOCATION_FLAGS.INVOCATION_FLAGS_NO_INVOKE; 
                    }
 
                    // If the invocationFlags are still 0, then 
                    // this should be an usable field, determine the other flags
                    if (invocationFlags == 0) 
                    {
                        if ((m_fieldAttributes & FieldAttributes.InitOnly) != (FieldAttributes)0)
                            invocationFlags |= INVOCATION_FLAGS.INVOCATION_FLAGS_SPECIAL_FIELD;
 
                        if ((m_fieldAttributes & FieldAttributes.HasFieldRVA) != (FieldAttributes)0)
                            invocationFlags |= INVOCATION_FLAGS.INVOCATION_FLAGS_SPECIAL_FIELD; 
 
                        // A public field is inaccesible to Transparent code if the method is Critical.
                        bool needsTransparencySecurityCheck = IsSecurityCritical && !IsSecuritySafeCritical; 
                        bool needsVisibilitySecurityCheck = ((m_fieldAttributes & FieldAttributes.FieldAccessMask) != FieldAttributes.Public) ||
                                                            (declaringType != null && declaringType.NeedsReflectionSecurityCheck);
                        if (needsTransparencySecurityCheck || needsVisibilitySecurityCheck)
                            invocationFlags |= INVOCATION_FLAGS.INVOCATION_FLAGS_NEED_SECURITY; 

                        // find out if the field type is one of the following: Primitive, Enum or Pointer 
                        Type fieldType = FieldType; 
                        if (fieldType.IsPointer || fieldType.IsEnum || fieldType.IsPrimitive)
                            invocationFlags |= INVOCATION_FLAGS.INVOCATION_FLAGS_FIELD_SPECIAL_CAST; 
                    }

                    // must be last to avoid threading problems
                    invocationFlags |= INVOCATION_FLAGS.INVOCATION_FLAGS_INITIALIZED; 

                    m_invocationFlags = invocationFlags; 
                } 

                return m_invocationFlags; 
            }
        }
        #endregion
 
        #region Constructor
        [System.Security.SecurityCritical]  // auto-generated 
        internal RtFieldInfo( 
            RuntimeFieldHandleInternal handle, RuntimeType declaringType, RuntimeTypeCache reflectedTypeCache, BindingFlags bindingFlags)
            : base(reflectedTypeCache, declaringType, bindingFlags) 
        {
            m_fieldHandle = handle.Value;
            m_fieldAttributes = RuntimeFieldHandle.GetAttributes(handle);
        } 
        #endregion
 
        #region Private Members 
        RuntimeFieldHandleInternal IRuntimeFieldInfo.Value
        { 
            [System.Security.SecuritySafeCritical]
#if !FEATURE_CORECLR
            [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
#endif 
            get
            { 
                return new RuntimeFieldHandleInternal(m_fieldHandle); 
            }
        } 

        private void CheckConsistency(Object target)
        {
            // only test instance fields 
            if ((m_fieldAttributes & FieldAttributes.Static) != FieldAttributes.Static)
            { 
                if (!m_declaringType.IsInstanceOfType(target)) 
                {
                    if (target == null) 
                        throw new TargetException(Environment.GetResourceString("RFLCT.Targ_StatFldReqTarg"));
                    else
                    {
                        throw new ArgumentException( 
                            String.Format(CultureInfo.CurrentUICulture, Environment.GetResourceString("Arg_FieldDeclTarget"),
                                Name, m_declaringType, target.GetType())); 
                    } 
                }
            } 
        }

        #endregion
 
        #region Internal Members
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)] 
#if !FEATURE_CORECLR 
        [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
#endif 
        internal override bool CacheEquals(object o)
        {
            RtFieldInfo m = o as RtFieldInfo;
 
            if ((object)m == null)
                return false; 
 
            return m.m_fieldHandle == m_fieldHandle;
        } 

        [System.Security.SecurityCritical]  // auto-generated
        [DebuggerStepThroughAttribute]
        [Diagnostics.DebuggerHidden] 
        internal void InternalSetValue(Object obj, Object value, BindingFlags invokeAttr, Binder binder, CultureInfo culture, bool doVisibilityCheck)
        { 
            InternalSetValue(obj, value, invokeAttr, binder, culture, doVisibilityCheck, true); 
        }
 
        [System.Security.SecurityCritical]  // auto-generated
        [DebuggerStepThroughAttribute]
        [Diagnostics.DebuggerHidden]
        internal void InternalSetValue(Object obj, Object value, BindingFlags invokeAttr, 
            Binder binder, CultureInfo culture, bool doVisibilityCheck, bool doCheckConsistency)
        { 
            INVOCATION_FLAGS invocationFlags = InvocationFlags; 
            RuntimeType declaringType = DeclaringType as RuntimeType;
 
            if ((invocationFlags & INVOCATION_FLAGS.INVOCATION_FLAGS_NO_INVOKE) != 0)
            {
                if (declaringType != null && declaringType.ContainsGenericParameters)
                    throw new InvalidOperationException(Environment.GetResourceString ("Arg_UnboundGenField")); 

                if ((declaringType == null && Module.Assembly.ReflectionOnly) || declaringType is  ReflectionOnlyType) 
                    throw new InvalidOperationException(Environment.GetResourceString("Arg_ReflectionOnlyField")); 

                throw new FieldAccessException(); 
            }

            if (doCheckConsistency)
                CheckConsistency(obj); 

            RuntimeType fieldType = (RuntimeType)FieldType; 
 
            value = fieldType.CheckValue(value, binder, culture, invokeAttr);
 
            if (doVisibilityCheck &&(invocationFlags &(INVOCATION_FLAGS.INVOCATION_FLAGS_SPECIAL_FIELD | INVOCATION_FLAGS.INVOCATION_FLAGS_NEED_SECURITY)) != 0)
                PerformVisibilityCheckOnField(m_fieldHandle, obj, m_declaringType, m_fieldAttributes, (uint)m_invocationFlags);

            bool domainInitialized = false; 
            if (declaringType == null)
            { 
                RuntimeFieldHandle.SetValue(this, obj, value, fieldType, m_fieldAttributes, null, ref domainInitialized); 
            }
            else 
            {
                domainInitialized = declaringType.DomainInitialized;
                RuntimeFieldHandle.SetValue(this, obj, value, fieldType, m_fieldAttributes, declaringType, ref domainInitialized);
                declaringType.DomainInitialized = domainInitialized; 
            }
        } 
 
        [DebuggerStepThroughAttribute]
        [Diagnostics.DebuggerHidden] 
        internal Object InternalGetValue(Object obj, bool doVisibilityCheck)
        {
            return InternalGetValue(obj, doVisibilityCheck, true);
        } 

        [System.Security.SecuritySafeCritical]  // auto-generated 
        [DebuggerStepThroughAttribute] 
        [Diagnostics.DebuggerHidden]
        internal Object InternalGetValue(Object obj, bool doVisibilityCheck, bool doCheckConsistency) 
        {
            INVOCATION_FLAGS invocationFlags = InvocationFlags;
            RuntimeType declaringType = DeclaringType as RuntimeType;
 
            if ((invocationFlags & INVOCATION_FLAGS.INVOCATION_FLAGS_NO_INVOKE) != 0)
            { 
                if (declaringType != null && DeclaringType.ContainsGenericParameters) 
                    throw new InvalidOperationException(Environment.GetResourceString ("Arg_UnboundGenField"));
 
                if ((declaringType == null && Module.Assembly.ReflectionOnly) || declaringType is ReflectionOnlyType)
                    throw new InvalidOperationException(Environment.GetResourceString("Arg_ReflectionOnlyField"));

                throw new FieldAccessException(); 
            }
 
            if (doCheckConsistency) 
                CheckConsistency(obj);
 
            RuntimeType fieldType = (RuntimeType)FieldType;
            if (doVisibilityCheck &&(invocationFlags & INVOCATION_FLAGS.INVOCATION_FLAGS_NEED_SECURITY) != 0)
                PerformVisibilityCheckOnField(m_fieldHandle, obj, m_declaringType, m_fieldAttributes, (uint)(m_invocationFlags & ~INVOCATION_FLAGS.INVOCATION_FLAGS_SPECIAL_FIELD));
 
            bool domainInitialized = false;
            if (declaringType == null) 
            { 
                return RuntimeFieldHandle.GetValue(this, obj, fieldType, null, ref domainInitialized);
            } 
            else
            {
                domainInitialized = declaringType.DomainInitialized;
                object retVal = RuntimeFieldHandle.GetValue(this, obj, fieldType, declaringType, ref domainInitialized); 
                declaringType.DomainInitialized = domainInitialized;
                return retVal; 
            } 
        }
 
        #endregion

        #region MemberInfo Overrides
        public override String Name 
        {
            [System.Security.SecuritySafeCritical]  // auto-generated 
#if !FEATURE_CORECLR 
            [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
#endif 
            get
            {
                if (m_name == null)
                    m_name = RuntimeFieldHandle.GetName(this); 

                return m_name; 
            } 
        }
 
        public override int MetadataToken
        {
            [System.Security.SecuritySafeCritical]  // auto-generated
            get { return RuntimeFieldHandle.GetToken(this); } 
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated 
        internal override RuntimeModule GetRuntimeModule()
        { 
            return RuntimeTypeHandle.GetModule(RuntimeFieldHandle.GetApproxDeclaringType(this));
        }

        #endregion 

        #region FieldInfo Overrides 
        public override Object GetValue(Object obj) 
        {
            return InternalGetValue(obj, true); 
        }

        public override object GetRawConstantValue() { throw new InvalidOperationException(); }
 
        [System.Security.SecuritySafeCritical]  // auto-generated
        [DebuggerStepThroughAttribute] 
        [Diagnostics.DebuggerHidden] 
        public override Object GetValueDirect(TypedReference obj)
        { 
            if (obj.IsNull)
                throw new ArgumentException(Environment.GetResourceString("Arg_TypedReference_Null"));
            Contract.EndContractBlock();
 
            unsafe
            { 
                // Passing TypedReference by reference is easier to make correct in native code 
                return RuntimeFieldHandle.GetValueDirect(this, (RuntimeType)FieldType, &obj, (RuntimeType)DeclaringType);
            } 
        }

        [System.Security.SecuritySafeCritical]  // auto-generated
        [DebuggerStepThroughAttribute] 
        [Diagnostics.DebuggerHidden]
        public override void SetValue(Object obj, Object value, BindingFlags invokeAttr, Binder binder, CultureInfo culture) 
        { 
            InternalSetValue(obj, value, invokeAttr, binder, culture, true);
        } 

        [System.Security.SecuritySafeCritical]  // auto-generated
        [DebuggerStepThroughAttribute]
        [Diagnostics.DebuggerHidden] 
        public override void SetValueDirect(TypedReference obj, Object value)
        { 
            if (obj.IsNull) 
                throw new ArgumentException(Environment.GetResourceString("Arg_TypedReference_Null"));
            Contract.EndContractBlock(); 

            unsafe
            {
                // Passing TypedReference by reference is easier to make correct in native code 
                RuntimeFieldHandle.SetValueDirect(this, (RuntimeType)FieldType, &obj, value, (RuntimeType)DeclaringType);
            } 
        } 

        public override RuntimeFieldHandle FieldHandle 
        {
            [System.Security.SecuritySafeCritical]  // auto-generated
            get
            { 
                Type declaringType = DeclaringType;
                if ((declaringType == null && Module.Assembly.ReflectionOnly) || declaringType is ReflectionOnlyType) 
                    throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_NotAllowedInReflectionOnly")); 
                return new RuntimeFieldHandle(this);
            } 
        }

        internal IntPtr GetFieldHandle()
        { 
            return m_fieldHandle;
        } 
 
        public override FieldAttributes Attributes
        { 
            get
            {
                return m_fieldAttributes;
            } 
        }
 
        public override Type FieldType 
        {
            [System.Security.SecuritySafeCritical]  // auto-generated 
            get
            {
                if (m_fieldType == null)
                    m_fieldType = new Signature(this, m_declaringType).FieldType; 

                return m_fieldType; 
            } 
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated
        public override Type[] GetRequiredCustomModifiers()
        {
            return new Signature(this, m_declaringType).GetCustomModifiers(1, true); 
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated 
        public override Type[] GetOptionalCustomModifiers()
        { 
            return new Signature(this, m_declaringType).GetCustomModifiers(1, false);
        }

        #endregion 
    }
 
 
    [Serializable]
    internal sealed unsafe class MdFieldInfo : RuntimeFieldInfo, ISerializable 
    {
        #region Private Data Members
        private int m_tkField;
        private string m_name; 
        private Type m_fieldType;
        private FieldAttributes m_fieldAttributes; 
        #endregion 

        #region Constructor 
        internal MdFieldInfo(
        int tkField, FieldAttributes fieldAttributes, RuntimeTypeHandle declaringTypeHandle, RuntimeTypeCache reflectedTypeCache, BindingFlags bindingFlags)
            : base(reflectedTypeCache, declaringTypeHandle.GetRuntimeType(), bindingFlags)
        { 
            m_tkField = tkField;
            m_name = null; 
            m_fieldAttributes = fieldAttributes; 
        }
        #endregion 

        #region Internal Members
        [System.Security.SecuritySafeCritical]  // auto-generated
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)] 
        internal override bool CacheEquals(object o)
        { 
            MdFieldInfo m = o as MdFieldInfo; 

            if ((object)m == null) 
                return false;

            return m.m_tkField == m_tkField &&
                m_declaringType.GetTypeHandleInternal().GetModuleHandle().Equals( 
                    m.m_declaringType.GetTypeHandleInternal().GetModuleHandle());
        } 
        #endregion 

        #region MemberInfo Overrides 
        public override String Name
        {
            [System.Security.SecuritySafeCritical]  // auto-generated
            get 
            {
                if (m_name == null) 
                    m_name = GetRuntimeModule().MetadataImport.GetName(m_tkField).ToString(); 

                return m_name; 
            }
        }

        public override int MetadataToken { get { return m_tkField; } } 
        internal override RuntimeModule GetRuntimeModule() { return m_declaringType.GetRuntimeModule(); }
        #endregion 
 
        #region FieldInfo Overrides
        public override RuntimeFieldHandle FieldHandle { get { throw new NotSupportedException(); } } 
        public override FieldAttributes Attributes { get { return m_fieldAttributes; } }

        public override bool IsSecurityCritical { get { return DeclaringType.IsSecurityCritical; } }
        public override bool IsSecuritySafeCritical { get { return DeclaringType.IsSecuritySafeCritical; } } 
        public override bool IsSecurityTransparent { get { return DeclaringType.IsSecurityTransparent; } }
 
        [DebuggerStepThroughAttribute] 
        [Diagnostics.DebuggerHidden]
        public override Object GetValueDirect(TypedReference obj) 
        {
            return GetValue(null);
        }
 
        [DebuggerStepThroughAttribute]
        [Diagnostics.DebuggerHidden] 
        public override void SetValueDirect(TypedReference obj,Object value) 
        {
            throw new FieldAccessException(Environment.GetResourceString("Acc_ReadOnly")); 
        }

        [DebuggerStepThroughAttribute]
        [Diagnostics.DebuggerHidden] 
        public unsafe override Object GetValue(Object obj)
        { 
            return GetValue(false); 
        }
 
        public unsafe override Object GetRawConstantValue() { return GetValue(true); }

        [System.Security.SecuritySafeCritical]  // auto-generated
        internal unsafe Object GetValue(bool raw) 
        {
            // Cannot cache these because they could be user defined non-agile enumerations 
 
            Object value = MdConstant.GetValue(GetRuntimeModule().MetadataImport, m_tkField, FieldType.GetTypeHandleInternal(), raw);
 
            if (value == DBNull.Value)
                throw new NotSupportedException(Environment.GetResourceString("Arg_EnumLitValueNotFound"));

            return value; 
        }
 
        [DebuggerStepThroughAttribute] 
        [Diagnostics.DebuggerHidden]
        public override void SetValue(Object obj, Object value, BindingFlags invokeAttr, Binder binder, CultureInfo culture) 
        {
            throw new FieldAccessException(Environment.GetResourceString("Acc_ReadOnly"));
        }
 
        public override Type FieldType
        { 
            [System.Security.SecuritySafeCritical]  // auto-generated 
            get
            { 
                if (m_fieldType == null)
                {
                    ConstArray fieldMarshal = GetRuntimeModule().MetadataImport.GetSigOfFieldDef(m_tkField);
 
                    m_fieldType = new Signature(fieldMarshal.Signature.ToPointer(),
                        (int)fieldMarshal.Length, m_declaringType).FieldType; 
                } 

                return m_fieldType; 
            }
        }

        public override Type[] GetRequiredCustomModifiers() 
        {
            return new Type[0]; 
        } 

        public override Type[] GetOptionalCustomModifiers() 
        {
            return new Type[0];
        }
 
        #endregion
    } 
 

    internal static class MdConstant 
    {
        [System.Security.SecurityCritical]  // auto-generated
        public static unsafe Object GetValue(MetadataImport scope, int token, RuntimeTypeHandle fieldTypeHandle, bool raw)
        { 
            CorElementType corElementType = 0;
            long buffer = 0; 
            int length; 
        String stringVal;
 
            stringVal = scope.GetDefaultValue(token, out buffer, out length, out corElementType);

            RuntimeType fieldType = fieldTypeHandle.GetRuntimeType();
 
            if (fieldType.IsEnum && raw == false)
            { 
                long defaultValue = 0; 

                switch (corElementType) 
                {
                    #region Switch

                    case CorElementType.Void: 
                        return DBNull.Value;
 
                    case CorElementType.Char: 
                        defaultValue = *(char*)&buffer;
                        break; 

                    case CorElementType.I1:
                        defaultValue = *(sbyte*)&buffer;
                        break; 

                    case CorElementType.U1: 
                        defaultValue = *(byte*)&buffer; 
                        break;
 
                    case CorElementType.I2:
                        defaultValue = *(short*)&buffer;
                        break;
 
                    case CorElementType.U2:
                        defaultValue = *(ushort*)&buffer; 
                        break; 

                    case CorElementType.I4: 
                        defaultValue = *(int*)&buffer;
                        break;

                    case CorElementType.U4: 
                        defaultValue = *(uint*)&buffer;
                        break; 
 
                    case CorElementType.I8:
                        defaultValue = buffer; 
                        break;

                    case CorElementType.U8:
                        defaultValue = buffer; 
                        break;
 
                    default: 
                        throw new FormatException(Environment.GetResourceString("Arg_BadLiteralFormat"));
                    #endregion 
                }

                return RuntimeType.CreateEnum(fieldType, defaultValue);
            } 
            else if (fieldType == typeof(DateTime))
            { 
                long defaultValue = 0; 

                switch (corElementType) 
                {
                    #region Switch

                    case CorElementType.Void: 
                        return DBNull.Value;
 
                    case CorElementType.I8: 
                        defaultValue = buffer;
                        break; 

                    case CorElementType.U8:
                        defaultValue = buffer;
                        break; 

                    default: 
                        throw new FormatException(Environment.GetResourceString("Arg_BadLiteralFormat")); 
                    #endregion
                } 

                return new DateTime(defaultValue);
            }
            else 
            {
                switch (corElementType) 
                { 
                    #region Switch
 
                    case CorElementType.Void:
                        return DBNull.Value;

                    case CorElementType.Char: 
                        return *(char*)&buffer;
 
                    case CorElementType.I1: 
                        return *(sbyte*)&buffer;
 
                    case CorElementType.U1:
                        return *(byte*)&buffer;

                    case CorElementType.I2: 
                        return *(short*)&buffer;
 
                    case CorElementType.U2: 
                        return *(ushort*)&buffer;
 
                    case CorElementType.I4:
                        return *(int*)&buffer;

                    case CorElementType.U4: 
                        return *(uint*)&buffer;
 
                    case CorElementType.I8: 
                        return buffer;
 
                    case CorElementType.U8:
                        return (ulong)buffer;

                    case CorElementType.Boolean : 
                        // The boolean value returned from the metadata engine is stored as a
                        // BOOL, which actually maps to an int. We need to read it out as an int 
                        // to avoid problems on big-endian machines. 
                        return (*(int*)&buffer != 0);
 
                    case CorElementType.R4 :
                        return *(float*)&buffer;

                    case CorElementType.R8: 
                        return *(double*)&buffer;
 
                    case CorElementType.String: 
                        // A string constant can be empty but never null.
                        // A nullref constant can only be type CorElementType.Class. 
                        return stringVal == null ? String.Empty : stringVal;

                    case CorElementType.Class:
                        return null; 

                    default: 
                        throw new FormatException(Environment.GetResourceString("Arg_BadLiteralFormat")); 
                    #endregion
                } 
            }
        }
    }
 

    internal static class Associates 
    { 
        [Flags]
        internal enum Attributes 
        {
            ComposedOfAllVirtualMethods = 0x1,
            ComposedOfAllPrivateMethods = 0x2,
            ComposedOfNoPublicMembers   = 0x4, 
            ComposedOfNoStaticMembers   = 0x8,
        } 
 
        internal static bool IncludeAccessor(MethodInfo associate, bool nonPublic)
        { 
            if ((object)associate == null)
                return false;

            if (nonPublic) 
                return true;
 
            if (associate.IsPublic) 
                return true;
 
            return false;
        }

        [System.Security.SecurityCritical]  // auto-generated 
        private static unsafe RuntimeMethodInfo AssignAssociates(
            int tkMethod, 
            RuntimeType declaredType, 
            RuntimeType reflectedType)
        { 
            if (MetadataToken.IsNullToken(tkMethod))
                return null;

            Contract.Assert(declaredType != null); 
            Contract.Assert(reflectedType != null);
 
            bool isInherited = declaredType != reflectedType; 

            IntPtr[] genericArgumentHandles = null; 
            int genericArgumentCount = 0;
            RuntimeType [] genericArguments = declaredType.GetTypeHandleInternal().GetInstantiationInternal();
            if (genericArguments != null)
            { 
                genericArgumentCount = genericArguments.Length;
                genericArgumentHandles = new IntPtr[genericArguments.Length]; 
                for (int i = 0; i < genericArguments.Length; i++) 
                {
                    genericArgumentHandles[i] = genericArguments[i].GetTypeHandleInternal().Value; 
                }
            }

            RuntimeMethodHandleInternal associateMethodHandle = ModuleHandle.ResolveMethodHandleInternalCore(RuntimeTypeHandle.GetModule(declaredType), tkMethod, genericArgumentHandles, genericArgumentCount, null, 0); 
            Contract.Assert(!associateMethodHandle.IsNullHandle(), "Failed to resolve associateRecord methodDef token");
 
            if (isInherited) 
            {
                MethodAttributes methAttr = RuntimeMethodHandle.GetAttributes(associateMethodHandle); 

                // ECMA MethodSemantics: "All methods for a given Property or Event shall have the same accessibility
                //(ie the MemberAccessMask subfield of their Flags row) and cannot be CompilerControlled  [CLS]"
                // Consequently, a property may be composed of public and private methods. If the declared type != 
                // the reflected type, the private methods should not be exposed. Note that this implies that the
                // identity of a property includes it's reflected type. 
                if ((methAttr & MethodAttributes.MemberAccessMask) == MethodAttributes.Private) 
                    return null;
 
                // Note this is the first time the property was encountered walking from the most derived class
                // towards the base class. It would seem to follow that any associated methods would not
                // be overriden -- but this is not necessarily true. A more derived class may have overriden a
                // virtual method associated with a property in a base class without associating the override with 
                // the same or any property in the derived class.
                if ((methAttr & MethodAttributes.Virtual) != 0) 
                { 
                    bool declaringTypeIsClass =
                        (RuntimeTypeHandle.GetAttributes(declaredType) & TypeAttributes.ClassSemanticsMask) == TypeAttributes.Class; 

                    // It makes no sense to search for a virtual override of a method declared on an interface.
                    if (declaringTypeIsClass)
                    { 
                        int slot = RuntimeMethodHandle.GetSlot(associateMethodHandle);
 
                        // Find the override visible from the reflected type 
                        associateMethodHandle = RuntimeTypeHandle.GetMethodAt(reflectedType, slot);
                    } 
                }
            }

            RuntimeMethodInfo associateMethod = 
                RuntimeType.GetMethodBase(reflectedType, associateMethodHandle) as RuntimeMethodInfo;
 
            // suppose a property was mapped to a method not in the derivation hierarchy of the reflectedTypeHandle 
            if (associateMethod == null)
                associateMethod = reflectedType.Module.ResolveMethod(tkMethod, null, null) as RuntimeMethodInfo; 

            return associateMethod;
        }
 
        [System.Security.SecurityCritical]  // auto-generated
        internal static unsafe void AssignAssociates( 
            AssociateRecord* associates, 
            int cAssociates,
            RuntimeType declaringType, 
            RuntimeType reflectedType,
            out RuntimeMethodInfo addOn,
            out RuntimeMethodInfo removeOn,
            out RuntimeMethodInfo fireOn, 
            out RuntimeMethodInfo getter,
            out RuntimeMethodInfo setter, 
            out MethodInfo[] other, 
            out bool composedOfAllPrivateMethods,
            out BindingFlags bindingFlags) 
        {
            addOn = removeOn = fireOn = getter = setter = null;
            other = null;
 
            Attributes attributes =
                Attributes.ComposedOfAllPrivateMethods | 
                Attributes.ComposedOfAllVirtualMethods | 
                Attributes.ComposedOfNoPublicMembers |
                Attributes.ComposedOfNoStaticMembers; 

            while(RuntimeTypeHandle.IsGenericVariable(reflectedType))
                reflectedType = (RuntimeType)reflectedType.BaseType;
 
            bool isInherited = declaringType != reflectedType;
 
            List<MethodInfo> otherList = new List<MethodInfo>(cAssociates); 

            for (int i = 0; i < cAssociates; i++) 
            {
                #region Assign each associate
                RuntimeMethodInfo associateMethod =
                    AssignAssociates(associates[i].MethodDefToken, declaringType, reflectedType); 

                if (associateMethod == null) 
                    continue; 

                MethodAttributes methAttr = associateMethod.Attributes; 
                bool isPrivate =(methAttr & MethodAttributes.MemberAccessMask) == MethodAttributes.Private;
                bool isVirtual =(methAttr & MethodAttributes.Virtual) != 0;

                MethodAttributes visibility = methAttr & MethodAttributes.MemberAccessMask; 
                bool isPublic = visibility == MethodAttributes.Public;
                bool isStatic =(methAttr & MethodAttributes.Static) != 0; 
 
                if (isPublic)
                { 
                    attributes &= ~Attributes.ComposedOfNoPublicMembers;
                    attributes &= ~Attributes.ComposedOfAllPrivateMethods;
                }
                else if (!isPrivate) 
                {
                    attributes &= ~Attributes.ComposedOfAllPrivateMethods; 
                } 

                if (isStatic) 
                    attributes &= ~Attributes.ComposedOfNoStaticMembers;

                if (!isVirtual)
                    attributes &= ~Attributes.ComposedOfAllVirtualMethods; 
                #endregion
 
                if (associates[i].Semantics == MethodSemanticsAttributes.Setter) 
                    setter = associateMethod;
                else if (associates[i].Semantics == MethodSemanticsAttributes.Getter) 
                    getter = associateMethod;
                else if (associates[i].Semantics == MethodSemanticsAttributes.Fire)
                    fireOn = associateMethod;
                else if (associates[i].Semantics == MethodSemanticsAttributes.AddOn) 
                    addOn = associateMethod;
                else if (associates[i].Semantics == MethodSemanticsAttributes.RemoveOn) 
                    removeOn = associateMethod; 
                else
                    otherList.Add(associateMethod); 
            }

            bool isPseudoPublic = (attributes & Attributes.ComposedOfNoPublicMembers) == 0;
            bool isPseudoStatic = (attributes & Attributes.ComposedOfNoStaticMembers) == 0; 
            bindingFlags = RuntimeType.FilterPreCalculate(isPseudoPublic, isInherited, isPseudoStatic);
 
            composedOfAllPrivateMethods =(attributes & Attributes.ComposedOfAllPrivateMethods) != 0; 

            other = otherList.ToArray(); 
        }
    }

 
    [Serializable]
    internal unsafe sealed class RuntimePropertyInfo : PropertyInfo, ISerializable 
    { 
        #region Private Data Members
        private int m_token; 
        private string m_name;
        private void* m_utf8name;
        private PropertyAttributes m_flags;
        private RuntimeTypeCache m_reflectedTypeCache; 
        private RuntimeMethodInfo m_getterMethod;
        private RuntimeMethodInfo m_setterMethod; 
        private MethodInfo[] m_otherMethod; 
        private RuntimeType m_declaringType;
        private BindingFlags m_bindingFlags; 
        private Signature m_signature;
        private ParameterInfo[] m_parameters;
        #endregion
 
        #region Constructor
        [System.Security.SecurityCritical]  // auto-generated 
        internal RuntimePropertyInfo( 
            int tkProperty, RuntimeType declaredType, RuntimeTypeCache reflectedTypeCache, out bool isPrivate)
        { 
            Contract.Requires(declaredType != null);
            Contract.Requires(reflectedTypeCache != null);
            Contract.Assert(!reflectedTypeCache.IsGlobal);
 
            MetadataImport scope = declaredType.GetRuntimeModule().MetadataImport;
 
            m_token = tkProperty; 
            m_reflectedTypeCache = reflectedTypeCache;
            m_declaringType = declaredType; 

            RuntimeMethodInfo dummy;

            scope.GetPropertyProps(tkProperty, out m_utf8name, out m_flags, out MetadataArgs.Skip.ConstArray); 
            int cAssociateRecord = scope.GetAssociatesCount(tkProperty);
            AssociateRecord* associateRecord = stackalloc AssociateRecord[cAssociateRecord]; 
            scope.GetAssociates(tkProperty, associateRecord, cAssociateRecord); 
            Associates.AssignAssociates(associateRecord, cAssociateRecord, declaredType, reflectedTypeCache.RuntimeType,
                out dummy, out dummy, out dummy, 
                out m_getterMethod, out m_setterMethod, out m_otherMethod,
                out isPrivate, out m_bindingFlags);
        }
        #endregion 

        #region Legacy Remoting Cache 
        // The size of CachedData is accounted for by BaseObjectWithCachedData in object.h. 
        // This member is currently being used by Remoting for caching remoting data. If you
        // need to cache data here, talk to the Remoting team to work out a mechanism, so that 
        // both caching systems can happily work together.
        private InternalCache m_cachedData;

        internal InternalCache RemotingCache 
        {
            get 
            { 
                // This grabs an internal copy of m_cachedData and uses
                // that instead of looking at m_cachedData directly because 
                // the cache may get cleared asynchronously.  This prevents
                // us from having to take a lock.
                InternalCache cache = m_cachedData;
                if (cache == null) 
                {
                    cache = new InternalCache("MemberInfo"); 
                    InternalCache ret = Interlocked.CompareExchange(ref m_cachedData, cache, null); 
                    if (ret != null)
                        cache = ret; 
                    GC.ClearCache += new ClearCacheHandler(OnCacheClear);
                }
                return cache;
            } 
        }
 
 
        internal void OnCacheClear(Object sender, ClearCacheEventArgs cacheEventArgs)
        { 
            m_cachedData = null;
        }
        #endregion
 
        #region Internal Members
        [System.Security.SecuritySafeCritical]  // auto-generated 
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)] 
        internal override bool CacheEquals(object o)
        { 
            RuntimePropertyInfo m = o as RuntimePropertyInfo;

            if ((object)m == null)
                return false; 

            return m.m_token == m_token && 
                RuntimeTypeHandle.GetModule(m_declaringType).Equals( 
                    RuntimeTypeHandle.GetModule(m.m_declaringType));
        } 

        internal Signature Signature
        {
            [System.Security.SecuritySafeCritical]  // auto-generated 
            get
            { 
                if (m_signature == null) 
                {
                    ConstArray sig; 

                    void* name;
                    GetRuntimeModule().MetadataImport.GetPropertyProps(
                        m_token, out name, out MetadataArgs.Skip.PropertyAttributes, out sig); 

                    m_signature = new Signature(sig.Signature.ToPointer(), (int)sig.Length, m_declaringType); 
                } 

                return m_signature; 
            }
        }
        internal bool EqualsSig(RuntimePropertyInfo target)
        { 
            //@Asymmetry - Legacy policy is to remove duplicate properties, including hidden properties.
            //             The comparison is done by name and by sig. The EqualsSig comparison is expensive 
            //             but forutnetly it is only called when an inherited property is hidden by name or 
            //             when an interfaces declare properies with the same signature.
            //             Note that we intentionally don't resolve generic arguments so that we don't treat 
            //             signatures that only match in certain instantiations as duplicates. This has the
            //             down side of treating overriding and overriden properties as different properties
            //             in some cases. But PopulateProperties in rttype.cs should have taken care of that
            //             by comparing VTable slots. 
            //
            //             Class C1(Of T, Y) 
            //                 Property Prop1(ByVal t1 As T) As Integer 
            //                     Get
            //                         ... ... 
            //                     End Get
            //                 End Property
            //                 Property Prop1(ByVal y1 As Y) As Integer
            //                     Get 
            //                         ... ...
            //                     End Get 
            //                 End Property 
            //             End Class
            // 

            Contract.Requires(Name.Equals(target.Name));
            Contract.Requires(this != target);
            Contract.Requires(this.ReflectedType == target.ReflectedType); 

            return Signature.DiffSigs(this.Signature, target.Signature); 
        } 
        internal BindingFlags BindingFlags { get { return m_bindingFlags; } }
        #endregion 

        #region Object Overrides
        public override String ToString()
        { 
            StringBuilder sbName = new StringBuilder(PropertyType.SigToString());
            sbName.Append(" "); 
            sbName.Append(Name); 

            RuntimeType[] arguments = Signature.Arguments; 
            if (arguments.Length > 0)
            {

                sbName.Append(" ["); 
                sbName.Append(MethodBase.ConstructParameters(arguments, Signature.CallingConvention));
                sbName.Append("]"); 
            } 

            return sbName.ToString(); 
        }
        #endregion

        #region ICustomAttributeProvider 
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

        [System.Security.SecuritySafeCritical]  // auto-generated 
        public override bool IsDefined(Type attributeType, bool inherit) 
        {
            if (attributeType == null) 
                throw new ArgumentNullException("attributeType");
            Contract.EndContractBlock();

            RuntimeType attributeRuntimeType = attributeType.UnderlyingSystemType as RuntimeType; 

            if (attributeRuntimeType == null) 
                throw new ArgumentException(Environment.GetResourceString("Arg_MustBeType"),"attributeType"); 

            return CustomAttribute.IsDefined(this, attributeRuntimeType); 
        }

        public override IList<CustomAttributeData> GetCustomAttributesData()
        { 
            return CustomAttributeData.GetCustomAttributesInternal(this);
        } 
        #endregion 

        #region MemberInfo Overrides 
        public override MemberTypes MemberType { get { return MemberTypes.Property; } }
        public override String Name
        {
            [System.Security.SecuritySafeCritical]  // auto-generated 
            get
            { 
                if (m_name == null) 
                    m_name = new Utf8String(m_utf8name).ToString();
 
                return m_name;
            }
        }
        public override Type DeclaringType 
        {
#if !FEATURE_CORECLR 
            [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")] 
#endif
            get 
            {
                return m_declaringType;
            }
        } 

        public override Type ReflectedType { get { return m_reflectedTypeCache.RuntimeType; } } 
 
        public override int MetadataToken { get { return m_token; } }
 
        public override Module Module { get { return GetRuntimeModule(); } }
        internal RuntimeModule GetRuntimeModule() { return m_declaringType.GetRuntimeModule(); }
        #endregion
 
        #region PropertyInfo Overrides
 
        #region Non Dynamic 

        public override Type[] GetRequiredCustomModifiers() 
        {
            return Signature.GetCustomModifiers(0, true);
        }
 
        public override Type[] GetOptionalCustomModifiers()
        { 
            return Signature.GetCustomModifiers(0, false); 
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated
        internal object GetConstantValue(bool raw)
        {
            Object defaultValue = MdConstant.GetValue(GetRuntimeModule().MetadataImport, m_token, PropertyType.GetTypeHandleInternal(), raw); 

            if (defaultValue == DBNull.Value) 
                // Arg_EnumLitValueNotFound -> "Literal value was not found." 
                throw new InvalidOperationException(Environment.GetResourceString("Arg_EnumLitValueNotFound"));
 
            return defaultValue;
        }

        public override object GetConstantValue() { return GetConstantValue(false); } 

        public override object GetRawConstantValue() { return GetConstantValue(true); } 
 
        public override MethodInfo[] GetAccessors(bool nonPublic)
        { 
            List<MethodInfo> accessorList = new List<MethodInfo>();

            if (Associates.IncludeAccessor(m_getterMethod, nonPublic))
                accessorList.Add(m_getterMethod); 

            if (Associates.IncludeAccessor(m_setterMethod, nonPublic)) 
                accessorList.Add(m_setterMethod); 

            if ((object)m_otherMethod != null) 
            {
                for(int i = 0; i < m_otherMethod.Length; i ++)
                {
                    if (Associates.IncludeAccessor(m_otherMethod[i] as MethodInfo, nonPublic)) 
                        accessorList.Add(m_otherMethod[i]);
                } 
            } 
            return accessorList.ToArray();
        } 

        public override Type PropertyType
        {
#if !FEATURE_CORECLR 
            [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
#endif 
            get { return Signature.ReturnType; } 
        }
 
#if !FEATURE_CORECLR
        [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
#endif
        public override MethodInfo GetGetMethod(bool nonPublic) 
        {
            if (!Associates.IncludeAccessor(m_getterMethod, nonPublic)) 
                return null; 

            return m_getterMethod; 
        }

#if !FEATURE_CORECLR
        [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")] 
#endif
        public override MethodInfo GetSetMethod(bool nonPublic) 
        { 
            if (!Associates.IncludeAccessor(m_setterMethod, nonPublic))
                return null; 

            return m_setterMethod;
        }
 
        public override ParameterInfo[] GetIndexParameters()
        { 
            ParameterInfo[] indexParams = GetIndexParametersNoCopy(); 

            int numParams = indexParams.Length; 

            if (numParams == 0)
                return indexParams;
 
            ParameterInfo[] ret = new ParameterInfo[numParams];
 
            Array.Copy(indexParams, ret, numParams); 

            return ret; 
        }

        internal ParameterInfo[] GetIndexParametersNoCopy()
        { 
            // @History - Logic ported from RTM
 
            // No need to lock because we don't guarantee the uniqueness of ParameterInfo objects 
            if (m_parameters == null)
            { 
                int numParams = 0;
                ParameterInfo[] methParams = null;

                // First try to get the Get method. 
                MethodInfo m = GetGetMethod(true);
                if (m != null) 
                { 
                    // There is a Get method so use it.
                    methParams = m.GetParametersNoCopy(); 
                    numParams = methParams.Length;
                }
                else
                { 
                    // If there is no Get method then use the Set method.
                    m = GetSetMethod(true); 
 
                    if (m != null)
                    { 
                        methParams = m.GetParametersNoCopy();
                        numParams = methParams.Length - 1;
                    }
                } 

                // Now copy over the parameter info's and change their 
                // owning member info to the current property info. 

                ParameterInfo[] propParams = new ParameterInfo[numParams]; 

                for (int i = 0; i < numParams; i++)
                    propParams[i] = new RuntimeParameterInfo((RuntimeParameterInfo)methParams[i], this);
 
                m_parameters = propParams;
            } 
 
            return m_parameters;
        } 

        public override PropertyAttributes Attributes
        {
            get 
            {
                return m_flags; 
            } 
        }
 
        public override bool CanRead
        {
            get
            { 
                return m_getterMethod != null;
            } 
        } 

        public override bool CanWrite 
        {
#if !FEATURE_CORECLR
            [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
#endif 
            get
            { 
                return m_setterMethod != null; 
            }
        } 
        #endregion

        #region Dynamic
        [DebuggerStepThroughAttribute] 
        [Diagnostics.DebuggerHidden]
#if !FEATURE_CORECLR 
        [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")] 
#endif
        public override Object GetValue(Object obj,Object[] index) 
        {
            return GetValue(obj, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static,
                null, index, null);
        } 

        [DebuggerStepThroughAttribute] 
        [Diagnostics.DebuggerHidden] 
        public override Object GetValue(Object obj, BindingFlags invokeAttr, Binder binder, Object[] index, CultureInfo culture)
        { 

            MethodInfo m = GetGetMethod(true);
            if (m == null)
                throw new ArgumentException(System.Environment.GetResourceString("Arg_GetMethNotFnd")); 
            return m.Invoke(obj, invokeAttr, binder, index, null);
        } 
 
        [DebuggerStepThroughAttribute]
        [Diagnostics.DebuggerHidden] 
#if !FEATURE_CORECLR
        [TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
#endif
        public override void SetValue(Object obj, Object value, Object[] index) 
        {
            SetValue(obj, 
                    value, 
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static,
                    null, 
                    index,
                    null);
        }
 
        [DebuggerStepThroughAttribute]
        [Diagnostics.DebuggerHidden] 
        public override void SetValue(Object obj, Object value, BindingFlags invokeAttr, Binder binder, Object[] index, CultureInfo culture) 
        {
 
            MethodInfo m = GetSetMethod(true);

            if (m == null)
                throw new ArgumentException(System.Environment.GetResourceString("Arg_SetMethNotFnd")); 

            Object[] args = null; 
 
            if (index != null)
            { 
                args = new Object[index.Length + 1];

                for(int i=0;i<index.Length;i++)
                    args[i] = index[i]; 

                args[index.Length] = value; 
            } 
            else
            { 
                args = new Object[1];
                args[0] = value;
            }
 
            m.Invoke(obj, invokeAttr, binder, args, culture);
        } 
        #endregion 

        #endregion 

        #region ISerializable Implementation
        [System.Security.SecurityCritical]  // auto-generated
        public void GetObjectData(SerializationInfo info, StreamingContext context) 
        {
            if (info == null) 
                throw new ArgumentNullException("info"); 
            Contract.EndContractBlock();
 
            MemberInfoSerializationHolder.GetSerializationInfo(info, Name, ReflectedType, ToString(), MemberTypes.Property);
        }
        #endregion
    } 

 
    [Serializable] 
    internal unsafe sealed class RuntimeEventInfo : EventInfo, ISerializable
    { 
        #region Private Data Members
        private int m_token;
        private EventAttributes m_flags;
        private string m_name; 
        private void* m_utf8name;
        private RuntimeTypeCache m_reflectedTypeCache; 
        private RuntimeMethodInfo m_addMethod; 
        private RuntimeMethodInfo m_removeMethod;
        private RuntimeMethodInfo m_raiseMethod; 
        private MethodInfo[] m_otherMethod;
        private RuntimeType m_declaringType;
        private BindingFlags m_bindingFlags;
        #endregion 

        #region Constructor 
        internal RuntimeEventInfo() 
        {
            // Used for dummy head node during population 
        }
        [System.Security.SecurityCritical]  // auto-generated
        internal RuntimeEventInfo(int tkEvent, RuntimeType declaredType, RuntimeTypeCache reflectedTypeCache, out bool isPrivate)
        { 
            Contract.Requires(declaredType != null);
            Contract.Requires(reflectedTypeCache != null); 
            Contract.Assert(!reflectedTypeCache.IsGlobal); 

            MetadataImport scope = declaredType.GetRuntimeModule().MetadataImport; 

            m_token = tkEvent;
            m_reflectedTypeCache = reflectedTypeCache;
            m_declaringType = declaredType; 

 
            RuntimeType reflectedType = reflectedTypeCache.RuntimeType; 
            RuntimeMethodInfo dummy;
 
            scope.GetEventProps(tkEvent, out m_utf8name, out m_flags);
            int cAssociateRecord = scope.GetAssociatesCount(tkEvent);
            AssociateRecord* associateRecord = stackalloc AssociateRecord[cAssociateRecord];
            scope.GetAssociates(tkEvent, associateRecord, cAssociateRecord); 
            Associates.AssignAssociates(associateRecord, cAssociateRecord, declaredType, reflectedType,
                out m_addMethod, out m_removeMethod, out m_raiseMethod, 
                out dummy, out dummy, out m_otherMethod, out isPrivate, out m_bindingFlags); 
        }
        #endregion 

        #region Legacy Remoting Cache
        // The size of CachedData is accounted for by BaseObjectWithCachedData in object.h.
        // This member is currently being used by Remoting for caching remoting data. If you 
        // need to cache data here, talk to the Remoting team to work out a mechanism, so that
        // both caching systems can happily work together. 
        private InternalCache m_cachedData; 

        internal InternalCache RemotingCache 
        {
            get
            {
                // This grabs an internal copy of m_cachedData and uses 
                // that instead of looking at m_cachedData directly because
                // the cache may get cleared asynchronously.  This prevents 
                // us from having to take a lock. 
                InternalCache cache = m_cachedData;
                if (cache == null) 
                {
                    cache = new InternalCache("MemberInfo");
                    InternalCache ret = Interlocked.CompareExchange(ref m_cachedData, cache, null);
                    if (ret != null) 
                        cache = ret;
                    GC.ClearCache += new ClearCacheHandler(OnCacheClear); 
                } 
                return cache;
            } 
        }


        internal void OnCacheClear(Object sender, ClearCacheEventArgs cacheEventArgs) 
        {
            m_cachedData = null; 
        } 
        #endregion
 
        #region Internal Members
        [System.Security.SecuritySafeCritical]  // auto-generated
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        internal override bool CacheEquals(object o) 
        {
            RuntimeEventInfo m = o as RuntimeEventInfo; 
 
            if ((object)m == null)
                return false; 

            return m.m_token == m_token &&
                RuntimeTypeHandle.GetModule(m_declaringType).Equals(
                    RuntimeTypeHandle.GetModule(m.m_declaringType)); 
        }
 
        internal BindingFlags BindingFlags { get { return m_bindingFlags; } } 
        #endregion
 
        #region Object Overrides
        public override String ToString()
        {
            if (m_addMethod == null || m_addMethod.GetParametersNoCopy().Length == 0) 
                throw new InvalidOperationException(Environment.GetResourceString("InvalidOperation_NoPublicAddMethod"));
 
            return m_addMethod.GetParametersNoCopy()[0].ParameterType.SigToString() + " " + Name; 
        }
        #endregion 

        #region ICustomAttributeProvider
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

        [System.Security.SecuritySafeCritical]  // auto-generated 
        public override bool IsDefined(Type attributeType, bool inherit)
        { 
            if (attributeType == null) 
                throw new ArgumentNullException("attributeType");
            Contract.EndContractBlock(); 

            RuntimeType attributeRuntimeType = attributeType.UnderlyingSystemType as RuntimeType;

            if (attributeRuntimeType == null) 
                throw new ArgumentException(Environment.GetResourceString("Arg_MustBeType"),"attributeType");
 
            return CustomAttribute.IsDefined(this, attributeRuntimeType); 
        }
 
        public override IList<CustomAttributeData> GetCustomAttributesData()
        {
            return CustomAttributeData.GetCustomAttributesInternal(this);
        } 
        #endregion
 
        #region MemberInfo Overrides 
        public override MemberTypes MemberType { get { return MemberTypes.Event; } }
        public override String Name 
        {
            [System.Security.SecuritySafeCritical]  // auto-generated
            get
            { 
                if (m_name == null)
                    m_name = new Utf8String(m_utf8name).ToString(); 
 
                return m_name;
            } 
        }
        public override Type DeclaringType { get { return m_declaringType; } }
        public override Type ReflectedType { get { return m_reflectedTypeCache.RuntimeType; } }
        public override int MetadataToken { get { return m_token; } } 
        public override Module Module { get { return GetRuntimeModule(); } }
        internal RuntimeModule GetRuntimeModule() { return m_declaringType.GetRuntimeModule(); } 
        #endregion 

        #region ISerializable 
        [System.Security.SecurityCritical]  // auto-generated_required
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null) 
                throw new ArgumentNullException("info");
            Contract.EndContractBlock(); 
 
            MemberInfoSerializationHolder.GetSerializationInfo(info, Name, ReflectedType, null, MemberTypes.Event);
        } 
        #endregion

        #region EventInfo Overrides
        public override MethodInfo[] GetOtherMethods(bool nonPublic) 
        {
            List<MethodInfo> ret = new List<MethodInfo>(); 
 
            if ((object)m_otherMethod == null)
                return new MethodInfo[0]; 

            for(int i = 0; i < m_otherMethod.Length; i ++)
            {
                if (Associates.IncludeAccessor((MethodInfo)m_otherMethod[i], nonPublic)) 
                    ret.Add(m_otherMethod[i]);
            } 
 
            return ret.ToArray();
        } 

        public override MethodInfo GetAddMethod(bool nonPublic)
        {
            if (!Associates.IncludeAccessor(m_addMethod, nonPublic)) 
                return null;
 
            return m_addMethod; 
        }
 
        public override MethodInfo GetRemoveMethod(bool nonPublic)
        {
            if (!Associates.IncludeAccessor(m_removeMethod, nonPublic))
                return null; 

            return m_removeMethod; 
        } 

        public override MethodInfo GetRaiseMethod(bool nonPublic) 
        {
            if (!Associates.IncludeAccessor(m_raiseMethod, nonPublic))
                return null;
 
            return m_raiseMethod;
        } 
 
        public override EventAttributes Attributes
        { 
            get
            {
                return m_flags;
            } 
        }
        #endregion 
    } 

 
    [Serializable]
    [ClassInterface(ClassInterfaceType.None)]
    [ComDefaultInterface(typeof(_ParameterInfo))]
    [System.Runtime.InteropServices.ComVisible(true)] 
    public class ParameterInfo : _ParameterInfo, ICustomAttributeProvider, IObjectReference
    { 
        #region Legacy Protected Members 
        protected String NameImpl;
        protected Type ClassImpl; 
        protected int PositionImpl;
        protected ParameterAttributes AttrsImpl;
        protected Object DefaultValueImpl; // cannot cache this as it may be non agile user defined enum
        protected MemberInfo MemberImpl; 
        #endregion
 
        #region Legacy Private Members 
        // These are here only for backwards compatibility -- they are not set
        // until this instance is serialized, so don't rely on their values from 
        // arbitrary code.
#pragma warning disable 169
        [OptionalField]
        private IntPtr _importer; 
        [OptionalField]
        private int _token; 
        [OptionalField] 
        private bool bExtraConstChecked;
#pragma warning restore 169 
        #endregion

        #region Constructor
        protected ParameterInfo() 
        {
        } 
        #endregion 

        #region Internal Members 
        // this is an internal api for DynamicMethod. A better solution is to change the relationship
        // between ParameterInfo and ParameterBuilder so that a ParameterBuilder can be seen as a writer
        // api over a ParameterInfo. However that is a possible breaking change so it needs to go through some process first
        internal void SetName(String name) 
        {
            NameImpl = name; 
        } 

        internal void SetAttributes(ParameterAttributes attributes) 
        {
            AttrsImpl = attributes;
        }
        #endregion 

        #region Public Methods 
        public virtual Type ParameterType 
        {
            get 
            {
                return ClassImpl;
            }
        } 

        public virtual String Name 
        { 
            get
            { 
                return NameImpl;
            }
        }
        public virtual Object DefaultValue { get { throw new NotImplementedException(); } } 
        public virtual Object RawDefaultValue  { get { throw new NotImplementedException(); } }
 
        public virtual int Position { get { return PositionImpl; } } 
        public virtual ParameterAttributes Attributes { get { return AttrsImpl; } }
        public virtual MemberInfo Member { get { return MemberImpl; } } 
        public bool IsIn { get { return((Attributes & ParameterAttributes.In) != 0); } }
        public bool IsOut { get { return((Attributes & ParameterAttributes.Out) != 0); } }
#if FEATURE_USE_LCID
        public bool IsLcid { get { return((Attributes & ParameterAttributes.Lcid) != 0); } } 
#endif
        public bool IsRetval { get { return((Attributes & ParameterAttributes.Retval) != 0); } } 
        public bool IsOptional { get { return((Attributes & ParameterAttributes.Optional) != 0); } } 

        public virtual int MetadataToken 
        {
            get
            {
                // This API was made virtual in V4. Code compiled against V2 might use 
                // "call" rather than "callvirt" to call it.
                // This makes sure those code still works. 
                RuntimeParameterInfo rtParam = this as RuntimeParameterInfo; 
                if (rtParam != null)
                    return rtParam.MetadataToken; 

                // return a null token
                return (int)MetadataTokenType.ParamDef;
            } 
        }
 
        public virtual Type[] GetRequiredCustomModifiers() 
        {
            return new Type[0]; 
        }

        public virtual Type[] GetOptionalCustomModifiers()
        { 
            return new Type[0];
        } 
        #endregion 

        #region Object Overrides 
        public override String ToString()
        {
            return ParameterType.SigToString() + " " + Name;
        } 
        #endregion
 
        #region ICustomAttributeProvider 
        public virtual Object[] GetCustomAttributes(bool inherit)
        { 
            return new object[0];
        }

        public virtual Object[] GetCustomAttributes(Type attributeType, bool inherit) 
        {
            if (attributeType == null) 
                throw new ArgumentNullException("attributeType"); 
            Contract.EndContractBlock();
 
            return new object[0];
        }

        public virtual bool IsDefined(Type attributeType, bool inherit) 
        {
            if (attributeType == null) 
                throw new ArgumentNullException("attributeType"); 
            Contract.EndContractBlock();
 
            return false;
        }

        public virtual IList<CustomAttributeData> GetCustomAttributesData() 
        {
            throw new NotImplementedException(); 
        } 
        #endregion
 
        #region _ParameterInfo implementation
        void _ParameterInfo.GetTypeInfoCount(out uint pcTInfo)
        {
            throw new NotImplementedException(); 
        }
 
        void _ParameterInfo.GetTypeInfo(uint iTInfo, uint lcid, IntPtr ppTInfo) 
        {
            throw new NotImplementedException(); 
        }

        void _ParameterInfo.GetIDsOfNames([In] ref Guid riid, IntPtr rgszNames, uint cNames, uint lcid, IntPtr rgDispId)
        { 
            throw new NotImplementedException();
        } 
 
        void _ParameterInfo.Invoke(uint dispIdMember, [In] ref Guid riid, uint lcid, short wFlags, IntPtr pDispParams, IntPtr pVarResult, IntPtr pExcepInfo, IntPtr puArgErr)
        { 
            throw new NotImplementedException();
        }
        #endregion
 
        #region IObjectReference
        // In V4 RuntimeParameterInfo is introduced. 
        // To support deserializing ParameterInfo instances serialized in earlier versions 
        // we need to implement IObjectReference.
        [SecurityCritical] 
        public object GetRealObject(StreamingContext context)
        {
            Contract.Ensures(Contract.Result<Object>() != null);
 
            // Once all the serializable fields have come in we can set up the real
            // instance based on just two of them (MemberImpl and PositionImpl). 
 
            if (MemberImpl == null)
                throw new SerializationException(Environment.GetResourceString(ResId.Serialization_InsufficientState)); 

            ParameterInfo[] args = null;

            switch (MemberImpl.MemberType) 
            {
                case MemberTypes.Constructor: 
                case MemberTypes.Method: 
                    if (PositionImpl == -1)
                    { 
                        if (MemberImpl.MemberType == MemberTypes.Method)
                            return ((MethodInfo)MemberImpl).ReturnParameter;
                        else
                            throw new SerializationException(Environment.GetResourceString(ResId.Serialization_BadParameterInfo)); 
                    }
                    else 
                    { 
                        args = ((MethodBase)MemberImpl).GetParametersNoCopy();
 
                        if (args != null && PositionImpl < args.Length)
                            return args[PositionImpl];
                        else
                            throw new SerializationException(Environment.GetResourceString(ResId.Serialization_BadParameterInfo)); 
                    }
 
                case MemberTypes.Property: 
                    args = ((RuntimePropertyInfo)MemberImpl).GetIndexParametersNoCopy();
 
                    if (args != null && PositionImpl > -1 && PositionImpl < args.Length)
                        return args[PositionImpl];
                    else
                        throw new SerializationException(Environment.GetResourceString(ResId.Serialization_BadParameterInfo)); 

                default: 
                    throw new SerializationException(Environment.GetResourceString(ResId.Serialization_NoParameterInfo)); 
            }
        } 
        #endregion
    }

    [Serializable] 
    internal unsafe sealed class RuntimeParameterInfo : ParameterInfo, ISerializable
    { 
        #region Static Members 
        [System.Security.SecurityCritical]  // auto-generated
        internal unsafe static ParameterInfo[] GetParameters(IRuntimeMethodInfo method, MemberInfo member, Signature sig) 
        {
            Contract.Assert(method is RuntimeMethodInfo || method is RuntimeConstructorInfo);

            ParameterInfo dummy; 
            return GetParameters(method, member, sig, out dummy, false);
        } 
 
        [System.Security.SecurityCritical]  // auto-generated
        internal unsafe static ParameterInfo GetReturnParameter(IRuntimeMethodInfo method, MemberInfo member, Signature sig) 
        {
            Contract.Assert(method is RuntimeMethodInfo || method is RuntimeConstructorInfo);

            ParameterInfo returnParameter; 
            GetParameters(method, member, sig, out returnParameter, true);
            return returnParameter; 
        } 

        [System.Security.SecurityCritical]  // auto-generated 
        internal unsafe static ParameterInfo[] GetParameters(
            IRuntimeMethodInfo methodHandle, MemberInfo member, Signature sig, out ParameterInfo returnParameter, bool fetchReturnParameter)
        {
            returnParameter = null; 
            int sigArgCount = sig.Arguments.Length;
            ParameterInfo[] args = fetchReturnParameter ? null : new ParameterInfo[sigArgCount]; 
 
            int tkMethodDef = RuntimeMethodHandle.GetMethodDef(methodHandle);
            int cParamDefs = 0; 

            // Not all methods have tokens. Arrays, pointers and byRef types do not have tokens as they
            // are generated on the fly by the runtime.
            if (!MdToken.IsNullToken(tkMethodDef)) 
            {
                MetadataImport scope = RuntimeTypeHandle.GetMetadataImport(RuntimeMethodHandle.GetDeclaringType(methodHandle)); 
                cParamDefs = scope.EnumParamsCount(tkMethodDef); 
                int* tkParamDefs = stackalloc int[cParamDefs];
                scope.EnumParams(tkMethodDef, tkParamDefs, cParamDefs); 

                // Not all parameters have tokens. Parameters may have no token
                // if they have no name and no attributes.
                if (cParamDefs > sigArgCount + 1 /* return type */) 
                    throw new BadImageFormatException(Environment.GetResourceString("BadImageFormat_ParameterSignatureMismatch"));
 
                for (uint i = 0; i < cParamDefs; i++) 
                {
                    #region Populate ParameterInfos 
                    ParameterAttributes attr;
                    int position, tkParamDef = tkParamDefs[i];

                    scope.GetParamDefProps(tkParamDef, out position, out attr); 

                    position--; 
 
                    if (fetchReturnParameter == true && position == -1)
                    { 
                        // more than one return parameter?
                        if (returnParameter != null)
                            throw new BadImageFormatException(Environment.GetResourceString("BadImageFormat_ParameterSignatureMismatch"));
 
                        returnParameter = new RuntimeParameterInfo(sig, scope, tkParamDef, position, attr, member);
                    } 
                    else if (fetchReturnParameter == false && position >= 0) 
                    {
                        // position beyong sigArgCount? 
                        if (position >= sigArgCount)
                            throw new BadImageFormatException(Environment.GetResourceString("BadImageFormat_ParameterSignatureMismatch"));

                        args[position] = new RuntimeParameterInfo(sig, scope, tkParamDef, position, attr, member); 
                    }
                    #endregion 
                } 
            }
 
            // Fill in empty ParameterInfos for those without tokens
            if (fetchReturnParameter)
            {
                if (returnParameter == null) 
                {
                    returnParameter = new RuntimeParameterInfo(sig, MetadataImport.EmptyImport, 0, -1, (ParameterAttributes)0, member); 
                } 
            }
            else 
            {
                if (cParamDefs < args.Length + 1)
                {
                    for (int i = 0; i < args.Length; i++) 
                    {
                        if (args[i] != null) 
                            continue; 

                        args[i] = new RuntimeParameterInfo(sig, MetadataImport.EmptyImport, 0, i, (ParameterAttributes)0, member); 
                    }
                }
            }
 
            return args;
        } 
        #endregion 

        #region Private Statics 
        private static readonly Type s_DecimalConstantAttributeType = typeof(DecimalConstantAttribute);
        private static readonly Type s_CustomConstantAttributeType = typeof(CustomConstantAttribute);
        #endregion
 
        #region Private Data Members
        // These are new in Whidbey, so we cannot serialize them directly or we break backwards compatibility. 
        [NonSerialized] 
        private int m_tkParamDef;
        [NonSerialized] 
        private MetadataImport m_scope;
        [NonSerialized]
        private Signature m_signature;
        [NonSerialized] 
        private volatile bool m_nameIsCached = false;
        [NonSerialized] 
        private readonly bool m_noDefaultValue = false; 
        [NonSerialized]
        private MethodBase m_originalMember = null; 
        #endregion

        #region Internal Properties
        internal MethodBase DefiningMethod 
        {
            get 
            { 
                MethodBase result = m_originalMember != null ? m_originalMember : MemberImpl as MethodBase;
                Contract.Assert(result != null); 
                return result;
            }
        }
        #endregion 

        #region VTS magic to serialize/deserialized to/from pre-Whidbey endpoints. 
        [SecurityCritical] 
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        { 
            if (info == null)
                throw new ArgumentNullException("info");
            Contract.EndContractBlock();
 
            // We could be serializing for consumption by a pre-Whidbey
            // endpoint. Therefore we set up all the serialized fields to look 
            // just like a v1.0/v1.1 instance. 

            // Need to set the type to ParameterInfo so that pre-Whidbey and Whidbey code 
            // can deserialize this. This is also why we cannot simply use [OnSerializing].
            info.SetType(typeof(ParameterInfo));

            // Use the properties intead of the fields in case the fields haven't been et 
            // _importer, bExtraConstChecked, and m_cachedData don't need to be set
 
            // Now set the legacy fields that the current implementation doesn't 
            // use any more. Note that _importer is a raw pointer that should
            // never have been serialized in V1. We set it to zero here; if the 
            // deserializer uses it (by calling GetCustomAttributes() on this
            // instance) they'll AV, but at least it will be a well defined
            // exception and not a random AV.
 
            info.AddValue("AttrsImpl", Attributes);
            info.AddValue("ClassImpl", ParameterType); 
            info.AddValue("DefaultValueImpl", DefaultValue); 
            info.AddValue("MemberImpl", Member);
            info.AddValue("NameImpl", Name); 
            info.AddValue("PositionImpl", Position);
            info.AddValue("_token", m_tkParamDef);
        }
        #endregion 

        #region Constructor 
        private RuntimeParameterInfo() 
        {
            m_nameIsCached = true; 
            m_noDefaultValue = true;
        }

        // used by RuntimePropertyInfo 
        internal RuntimeParameterInfo(RuntimeParameterInfo accessor, RuntimePropertyInfo property)
            : this(accessor, (MemberInfo)property) 
        { 
            m_signature = property.Signature;
        } 

        private RuntimeParameterInfo(RuntimeParameterInfo accessor, MemberInfo member)
        {
            // Change ownership 
            MemberImpl = member;
 
            // The original owner should always be a method, because this method is only used to 
            // change the owner from a method to a property.
            m_originalMember = accessor.MemberImpl as MethodBase; 
            Contract.Assert(m_originalMember != null);

            // Populate all the caches -- we inherit this behavior from RTM
            NameImpl = accessor.Name; 
            m_nameIsCached = true;
            ClassImpl = accessor.ParameterType; 
            PositionImpl = accessor.Position; 
            AttrsImpl = accessor.Attributes;
 
            // Strictly speeking, property's don't contain paramter tokens
            // However we need this to make ca's work... oh well...
            m_tkParamDef = MdToken.IsNullToken(accessor.MetadataToken) ? (int)MetadataTokenType.ParamDef : accessor.MetadataToken;
            m_scope = accessor.m_scope; 
        }
 
        private RuntimeParameterInfo( 
            Signature signature, MetadataImport scope, int tkParamDef,
            int position, ParameterAttributes attributes, MemberInfo member) 
        {
            Contract.Requires(member != null);
            Contract.Assert(MdToken.IsNullToken(tkParamDef) == scope.Equals(MetadataImport.EmptyImport));
            Contract.Assert(MdToken.IsNullToken(tkParamDef) || MdToken.IsTokenOfType(tkParamDef, MetadataTokenType.ParamDef)); 

            PositionImpl = position; 
            MemberImpl = member; 
            m_signature = signature;
            m_tkParamDef = MdToken.IsNullToken(tkParamDef) ? (int)MetadataTokenType.ParamDef : tkParamDef; 
            m_scope = scope;
            AttrsImpl = attributes;

            ClassImpl = null; 
            NameImpl = null;
        } 
 
        // ctor for no metadata MethodInfo in the DynamicMethod and RuntimeMethodInfo cases
        internal RuntimeParameterInfo(MethodInfo owner, String name, Type parameterType, int position) 
        {
            MemberImpl = owner;
            NameImpl = name;
            m_nameIsCached = true; 
            m_noDefaultValue = true;
            ClassImpl = parameterType; 
            PositionImpl = position; 
            AttrsImpl = ParameterAttributes.None;
            m_tkParamDef = (int)MetadataTokenType.ParamDef; 
            m_scope = MetadataImport.EmptyImport;
        }
        #endregion
 
        #region Public Methods
        public override Type ParameterType 
        { 
            get
            { 
                // only instance of ParameterInfo has ClassImpl, all its subclasses don't
                if (ClassImpl == null)
                {
                    RuntimeType parameterType; 
                    if (PositionImpl == -1)
                        parameterType = m_signature.ReturnType; 
                    else 
                        parameterType = m_signature.Arguments[PositionImpl];
 
                    Contract.Assert(parameterType != null);
                    // different thread could only write ClassImpl to the same value, so ---- is not a problem here
                    ClassImpl = parameterType;
                } 

                return ClassImpl; 
            } 
        }
 
        public override String Name
        {
            [System.Security.SecuritySafeCritical]  // auto-generated
            get 
            {
                if (!m_nameIsCached) 
                { 
                    if (!MdToken.IsNullToken(m_tkParamDef))
                    { 
                        string name;
                        name = m_scope.GetName(m_tkParamDef).ToString();
                        NameImpl = name;
                    } 

                    // other threads could only write it to true, so ---- is OK 
                    // this field is volatile, so the write ordering is guaranteed 
                    m_nameIsCached = true;
                } 

                // name may be null
                return NameImpl;
            } 
        }
        public override Object DefaultValue { get { return GetDefaultValue(false); } } 
        public override Object RawDefaultValue { get { return GetDefaultValue(true); } } 
        [System.Security.SecuritySafeCritical]  // auto-generated
        internal Object GetDefaultValue(bool raw) 
        {
            // Cannot cache because default value could be non-agile user defined enumeration.
            object defaultValue = null;
 
            // for dynamic method we pretend to have cached the value so we do not go to metadata
            if (!m_noDefaultValue) 
            { 
                if (ParameterType == typeof(DateTime))
                { 
                    if (raw)
                    {
                        CustomAttributeTypedArgument value =
                            CustomAttributeData.Filter( 
                                CustomAttributeData.GetCustomAttributes(this), typeof(DateTimeConstantAttribute), 0);
 
                        if (value.ArgumentType != null) 
                            return new DateTime((long)value.Value);
                    } 
                    else
                    {
                        object[] dt = GetCustomAttributes(typeof(DateTimeConstantAttribute), false);
                        if (dt != null && dt.Length != 0) 
                            return ((DateTimeConstantAttribute)dt[0]).Value;
                    } 
                } 

                #region Look for a default value in metadata 
                if (!MdToken.IsNullToken(m_tkParamDef))
                {
                    defaultValue = MdConstant.GetValue(m_scope, m_tkParamDef, ParameterType.GetTypeHandleInternal(), raw);
                } 
                #endregion
 
                if (defaultValue == DBNull.Value) 
                {
                    #region Look for a default value in the custom attributes 
                    if (raw)
                    {
                        System.Collections.Generic.IList<CustomAttributeData> attrs = CustomAttributeData.GetCustomAttributes(this);
                        CustomAttributeTypedArgument value = CustomAttributeData.Filter( 
                            attrs, s_CustomConstantAttributeType, "Value");
 
                        if (value.ArgumentType == null) 
                        {
                            value = CustomAttributeData.Filter( 
                                attrs, s_DecimalConstantAttributeType, "Value");


                            if (value.ArgumentType == null) 
                            {
                                for (int i = 0; i < attrs.Count; i++) 
                                { 
                                    if (attrs[i].Constructor.DeclaringType == s_DecimalConstantAttributeType)
                                    { 
                                        ParameterInfo[] parameters = attrs[i].Constructor.GetParameters();

                                        if (parameters.Length != 0)
                                        { 
                                            if (parameters[2].ParameterType == typeof(uint))
                                            { 
                                                System.Collections.Generic.IList<CustomAttributeTypedArgument> args = attrs[i].ConstructorArguments; 
                                                int low = (int)(UInt32)args[4].Value;
                                                int mid = (int)(UInt32)args[3].Value; 
                                                int hi = (int)(UInt32)args[2].Value;
                                                byte sign = (byte)args[1].Value;
                                                byte scale = (byte)args[0].Value;
                                                value = new CustomAttributeTypedArgument( 
                                                    new System.Decimal(low, mid, hi, (sign != 0), scale));
                                            } 
                                            else 
                                            {
                                                System.Collections.Generic.IList<CustomAttributeTypedArgument> args = attrs[i].ConstructorArguments; 
                                                int low = (int)args[4].Value;
                                                int mid = (int)args[3].Value;
                                                int hi = (int)args[2].Value;
                                                byte sign = (byte)args[1].Value; 
                                                byte scale = (byte)args[0].Value;
                                                value = new CustomAttributeTypedArgument( 
                                                    new System.Decimal(low, mid, hi, (sign != 0), scale)); 
                                            }
                                        } 
                                    }
                                }
                            }
                        } 

                        if (value.ArgumentType != null) 
                            defaultValue = value.Value; 
                    }
                    else 
                    {
                        Object[] CustomAttrs = GetCustomAttributes(s_CustomConstantAttributeType, false);
                        if (CustomAttrs.Length != 0)
                        { 
                            defaultValue = ((CustomConstantAttribute)CustomAttrs[0]).Value;
                        } 
                        else 
                        {
                            CustomAttrs = GetCustomAttributes(s_DecimalConstantAttributeType, false); 
                            if (CustomAttrs.Length != 0)
                            {
                                defaultValue = ((DecimalConstantAttribute)CustomAttrs[0]).Value;
                            } 
                        }
                    } 
                    #endregion 
                }
 
                if (defaultValue == DBNull.Value)
                {
                    #region Handle case if no default value was found
                    if (IsOptional) 
                    {
                        // If the argument is marked as optional then the default value is Missing.Value. 
                        defaultValue = Type.Missing; 
                    }
                    #endregion 
                }

            }
 
            return defaultValue;
        } 
 
        internal RuntimeModule GetRuntimeModule()
        { 
            RuntimeMethodInfo method = Member as RuntimeMethodInfo;
            RuntimeConstructorInfo constructor = Member as RuntimeConstructorInfo;
            RuntimePropertyInfo property = Member as RuntimePropertyInfo;
 
            if (method != null)
                return method.GetRuntimeModule(); 
            else if (constructor != null) 
                return constructor.GetRuntimeModule();
            else if (property != null) 
                return property.GetRuntimeModule();
            else
                return null;
        } 

        public override int MetadataToken 
        { 
            get
            { 
                return m_tkParamDef;
            }
        }
 
        public override Type[] GetRequiredCustomModifiers()
        { 
            return m_signature.GetCustomModifiers(PositionImpl + 1, true); 
        }
 
        public override Type[] GetOptionalCustomModifiers()
        {
            return m_signature.GetCustomModifiers(PositionImpl + 1, false);
        } 

        #endregion 
 
        #region ICustomAttributeProvider
        public override Object[] GetCustomAttributes(bool inherit) 
        {
            if (MdToken.IsNullToken(m_tkParamDef))
                return new object[0];
 
            return CustomAttribute.GetCustomAttributes(this, typeof(object) as RuntimeType);
        } 
 
        public override Object[] GetCustomAttributes(Type attributeType, bool inherit)
        { 
            if (attributeType == null)
                throw new ArgumentNullException("attributeType");
            Contract.EndContractBlock();
 
            if (MdToken.IsNullToken(m_tkParamDef))
                return new object[0]; 
 
            RuntimeType attributeRuntimeType = attributeType.UnderlyingSystemType as RuntimeType;
 
            if (attributeRuntimeType == null)
                throw new ArgumentException(Environment.GetResourceString("Arg_MustBeType"), "attributeType");

            return CustomAttribute.GetCustomAttributes(this, attributeRuntimeType); 
        }
 
        [System.Security.SecuritySafeCritical]  // auto-generated 
        public override bool IsDefined(Type attributeType, bool inherit)
        { 
            if (attributeType == null)
                throw new ArgumentNullException("attributeType");
            Contract.EndContractBlock();
 
            if (MdToken.IsNullToken(m_tkParamDef))
                return false; 
 
            RuntimeType attributeRuntimeType = attributeType.UnderlyingSystemType as RuntimeType;
 
            if (attributeRuntimeType == null)
                throw new ArgumentException(Environment.GetResourceString("Arg_MustBeType"), "attributeType");

            return CustomAttribute.IsDefined(this, attributeRuntimeType); 
        }
 
        public override IList<CustomAttributeData> GetCustomAttributesData() 
        {
            return CustomAttributeData.GetCustomAttributesInternal(this); 
        }
        #endregion

        #region Remoting Cache 
        private InternalCache m_cachedData;
 
        internal InternalCache RemotingCache 
        {
            get 
            {
                // This grabs an internal copy of m_cachedData and uses
                // that instead of looking at m_cachedData directly because
                // the cache may get cleared asynchronously.  This prevents 
                // us from having to take a lock.
                InternalCache cache = m_cachedData; 
                if (cache == null) 
                {
                    cache = new InternalCache("ParameterInfo"); 
                    InternalCache ret = Interlocked.CompareExchange(ref m_cachedData, cache, null);
                    if (ret != null)
                        cache = ret;
 
                    GC.ClearCache += new ClearCacheHandler(OnCacheClear);
                } 
                return cache; 
            }
        } 

        internal void OnCacheClear(Object sender, ClearCacheEventArgs cacheEventArgs)
        {
            m_cachedData = null; 
        }
        #endregion 
    } 
}

