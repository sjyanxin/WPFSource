//---------------------------------------------------------------------------- 
//
// <copyright file="DynamicObjectAccessor.cs" company="Microsoft">
//    Copyright (C) Microsoft Corporation.  All rights reserved.
// </copyright> 
//
// Description: Accessors for dynamic objects 
// 
//---------------------------------------------------------------------------
 
using System;
using System.Diagnostics;
using System.Dynamic;                   // GetMemberBinder, etc.
using System.Reflection;                // PropertyInfo, etc. 
using System.Runtime.CompilerServices;  // CallSite
using System.Linq.Expressions;          // Expression 
using SW = System.Windows;              // SRID, SR 

namespace MS.Internal.Data 
{
    #region DynamicObjectAccessor

    internal class DynamicObjectAccessor 
    {
        public DynamicObjectAccessor(Type ownerType, string propertyName) 
        { 
            _ownerType = ownerType;
            _propertyName = propertyName; 
        }

        public Type OwnerType { get { return _ownerType; } }
        public string PropertyName { get { return _propertyName; } } 
        public bool IsReadOnly { get { return false; } }
        public Type PropertyType { get { return typeof(object); } } 
 
        Type _ownerType;
        string _propertyName; 
    }

    #endregion DynamicObjectAccessor
 
    #region DynamicPropertyAccessor
 
    internal class DynamicPropertyAccessor : DynamicObjectAccessor 
    {
        public DynamicPropertyAccessor(Type ownerType, string propertyName) 
            : base(ownerType, propertyName)
        {
        }
 
        public object GetValue(object component)
        { 
            if (_getter == null) 
            {
                var binder = new TrivialGetMemberBinder(PropertyName); 
                _getter = CallSite<Func<CallSite, object, object>>.Create(binder);
            }

            return _getter.Target(_getter, component); 
        }
 
        public void SetValue(object component, object value) 
        {
            if (_setter == null) 
            {
                var binder = new TrivialSetMemberBinder(PropertyName);
                _setter = CallSite<Action<CallSite, object, object>>.Create(binder);
            } 

            _setter.Target(_setter, component, value); 
        } 

        CallSite<Func<CallSite, object, object>> _getter; 
        CallSite<Action<CallSite, object, object>> _setter;
    }

    #endregion DynamicPropertyAccessor 

    #region DynamicIndexerAccessor 
 
    internal class DynamicIndexerAccessor : DynamicObjectAccessor
    { 
        private DynamicIndexerAccessor(int rank)
            : base(typeof(IDynamicMetaObjectProvider), "Items")
        {
            var getBinder = new TrivialGetIndexBinder(rank); 
            var setBinder = new TrivialSetIndexBinder(rank);
 
            Type delegateType, callsiteType; 
            MethodInfo createMethod;
            FieldInfo targetField; 
            Type[] typeArgs;
            int i;

            // getter delegate type:  Func<CallSite, object, ..., object> 
            typeArgs = new Type[rank+3];
            typeArgs[0] = typeof(CallSite); 
            for (i=1; i<=rank+2; ++i) 
            {
                typeArgs[i] = typeof(object); 
            }
            delegateType = Expression.GetDelegateType(typeArgs);

            // getter CallSite:  CallSite<Func<CallSite, object, ..., object>>.Create(getBinder) 
            callsiteType = typeof(CallSite<>).MakeGenericType(new Type[]{ delegateType });
            createMethod = callsiteType.GetMethod("Create", new Type[]{ typeof(CallSiteBinder) }); 
            _getterCallSite = (CallSite)createMethod.Invoke(null, new object[]{ getBinder }); 

            // getter delegate:  _getterCallSite.Target 
            targetField = callsiteType.GetField("Target");
            _getterDelegate = (MulticastDelegate)targetField.GetValue(_getterCallSite);

 
            // setter delegate type:  Action<CallSite, object, ..., object>
            typeArgs = new Type[rank+4]; 
            typeArgs[0] = typeof(CallSite); 
            typeArgs[rank+3] = typeof(void);
            for (i=1; i<=rank+2; ++i) 
            {
                typeArgs[i] = typeof(object);
            }
            delegateType = Expression.GetDelegateType(typeArgs); 

            // setter CallSite:  CallSite<Func<CallSite, object, ..., object>>.Create(setBinder) 
            callsiteType = typeof(CallSite<>).MakeGenericType(new Type[]{ delegateType }); 
            createMethod = callsiteType.GetMethod("Create", new Type[]{ typeof(CallSiteBinder) });
            _setterCallSite = (CallSite)createMethod.Invoke(null, new object[]{ setBinder }); 

            // setter delegate:  _setterCallSite.Target
            targetField = callsiteType.GetField("Target");
            _setterDelegate = (MulticastDelegate)targetField.GetValue(_setterCallSite); 
        }
 
        public object GetValue(object component, object[] args) 
        {
            int rank = args.Length; 
            object[] delegateArgs = new object[rank + 2];
            delegateArgs[0] = _getterCallSite;
            delegateArgs[1] = component;
            Array.Copy(args, 0, delegateArgs, 2, rank); 

            return _getterDelegate.DynamicInvoke(delegateArgs); 
        } 

        public void SetValue(object component, object[] args, object value) 
        {
            int rank = args.Length;
            object[] delegateArgs = new object[rank + 3];
            delegateArgs[0] = _setterCallSite; 
            delegateArgs[1] = component;
            Array.Copy(args, 0, delegateArgs, 2, rank); 
            delegateArgs[rank + 2] = value; 

            _setterDelegate.DynamicInvoke(delegateArgs); 
        }

        // ensure only one accessor for each rank
        public static DynamicIndexerAccessor GetIndexerAccessor(int rank) 
        {
            if (_accessors.Length < rank || _accessors[rank-1] == null) 
            { 
                lock(_lock)
                { 
                    if (_accessors.Length < rank)
                    {
                        DynamicIndexerAccessor[] newAccessors = new DynamicIndexerAccessor[rank];
                        Array.Copy(_accessors, 0, newAccessors, 0, _accessors.Length); 
                        _accessors = newAccessors;
                    } 
 
                    if (_accessors[rank-1] == null)
                    { 
                        _accessors[rank-1] = new DynamicIndexerAccessor(rank);
                    }
                }
            } 

            return _accessors[rank-1]; 
        } 

        CallSite            _getterCallSite, _setterCallSite; 
        MulticastDelegate   _getterDelegate, _setterDelegate;

        static DynamicIndexerAccessor[] _accessors = new DynamicIndexerAccessor[1];
        static object _lock = new object(); 
    }
 
    #endregion DynamicIndexerAccessor 

    #region Trivial binders 

    internal class TrivialGetMemberBinder : GetMemberBinder
    {
        public TrivialGetMemberBinder(string propertyName) 
            : base(propertyName, false /*ignoreCase*/)
        { 
        } 

        public override DynamicMetaObject FallbackGetMember(DynamicMetaObject target, 
                                                            DynamicMetaObject errorSuggestion)
        {
            return errorSuggestion ??
                TrivialBinderHelper.ThrowExpression(SW.SR.Get(SW.SRID.PropertyPathNoProperty, target, Name), ReturnType); 
        }
    } 
 
    internal class TrivialSetMemberBinder : SetMemberBinder
    { 
        public TrivialSetMemberBinder(string propertyName)
            : base(propertyName, false /*ignoreCase*/)
        {
        } 

        public override DynamicMetaObject FallbackSetMember(DynamicMetaObject target, 
                                                            DynamicMetaObject value, 
                                                            DynamicMetaObject errorSuggestion)
        { 
            return errorSuggestion ??
                TrivialBinderHelper.ThrowExpression(SW.SR.Get(SW.SRID.PropertyPathNoProperty, target, Name), ReturnType);
        }
    } 

    internal class TrivialGetIndexBinder : GetIndexBinder 
    { 
        public TrivialGetIndexBinder(int rank)
            : base(new CallInfo(rank)) 
        {
        }

        public override DynamicMetaObject FallbackGetIndex(DynamicMetaObject target, 
                                                            DynamicMetaObject[] indexes,
                                                            DynamicMetaObject errorSuggestion) 
        { 
            return errorSuggestion ??
                TrivialBinderHelper.ThrowExpression(SW.SR.Get(SW.SRID.PropertyPathNoProperty, target, "Items"), ReturnType); 
        }
    }

    internal class TrivialSetIndexBinder : SetIndexBinder 
    {
        public TrivialSetIndexBinder(int rank) 
            : base(new CallInfo(rank)) 
        {
        } 

        public override DynamicMetaObject FallbackSetIndex(DynamicMetaObject target,
                                                            DynamicMetaObject[] indexes,
                                                            DynamicMetaObject value, 
                                                            DynamicMetaObject errorSuggestion)
        { 
            return errorSuggestion ?? 
                TrivialBinderHelper.ThrowExpression(SW.SR.Get(SW.SRID.PropertyPathNoProperty, target, "Items"), ReturnType);
        } 
    }

    internal static class TrivialBinderHelper
    { 
        public static DynamicMetaObject ThrowExpression(string message, Type returnType)
        { 
            return new DynamicMetaObject( 
                        Expression.Throw(
                            Expression.New( 
                                typeof(InvalidOperationException).GetConstructor(new Type[] { typeof(string) }),
                                Expression.Constant(message)
                            ),
                            returnType 
                        ),
                        BindingRestrictions.Empty 
                    ); 
        }
    } 

    #endregion Trivial binders
}

