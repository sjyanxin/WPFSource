//---------------------------------------------------------------------------- 
//
// <copyright file="Binding.cs" company="Microsoft">
//    Copyright (C) Microsoft Corporation.  All rights reserved.
// </copyright> 
//
// Description: Defines Binding object, which describes an instance of data Binding. 
// 
// See spec at http://avalon/connecteddata/Specs/Data%20Binding.mht
// 
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic; 
using System.Collections.ObjectModel;
using System.Globalization; 
using System.Diagnostics; 
using System.ComponentModel;
using System.Xml; 

using System.Windows;
using System.Windows.Controls;  // Validation
using System.Windows.Markup; 
using MS.Utility;
using MS.Internal; // Invariant.Assert 
using MS.Internal.Controls; // Validation 
using MS.Internal.Data;
using MS.Internal.KnownBoxes; 

namespace System.Windows.Data
{
 
    /// <summary>
    /// Status of the Binding 
    /// </summary> 
    public enum BindingStatus
    { 
        /// <summary> Binding has not yet been attached to its target </summary>
        Unattached = 0,
        /// <summary> Binding has not yet been activated </summary>
        Inactive, 
        /// <summary> Binding has been successfully activated </summary>
        Active, 
        /// <summary> Binding has been detached from its target </summary> 
        Detached,
        /// <summary> Binding is waiting for an async operation to complete</summary> 
        AsyncRequestPending,
        /// <summary> error - source path could not be resolved </summary>
        PathError,
        /// <summary> error - a legal value could not be obtained from the source</summary> 
        UpdateTargetError,
        /// <summary> error - the value could not be sent to the source </summary> 
        UpdateSourceError, 
    }
 
    /// <summary>
    ///  Describes an instance of a Binding, binding a target
    ///  (DependencyObject, DependencyProperty) to a source (object, property)
    /// </summary> 
    public class Binding : BindingBase
    { 
        //----------------------------------------------------- 
        //
        //  Enums 
        //
        //-----------------------------------------------------

        // Which source property is in use 
        enum SourceProperties : byte { None, RelativeSource, ElementName, Source, InternalSource }
 
 
        //------------------------------------------------------
        // 
        //  Dynamic properties and events
        //
        //-----------------------------------------------------
 
        /// <summary>
        /// The SourceUpdated event is raised whenever a value is transferred from the target to the source, 
        /// but only for Bindings that have requested the event by setting BindFlags.NotifyOnSourceUpdated. 
        /// </summary>
        public static readonly RoutedEvent SourceUpdatedEvent = 
                EventManager.RegisterRoutedEvent("SourceUpdated",
                                        RoutingStrategy.Bubble,
                                        typeof(EventHandler<DataTransferEventArgs>),
                                        typeof(Binding)); 

        /// <summary> 
        ///     Adds a handler for the SourceUpdated attached event 
        /// </summary>
        /// <param name="element">UIElement or ContentElement that listens to this event</param> 
        /// <param name="handler">Event Handler to be added</param>
        public static void AddSourceUpdatedHandler(DependencyObject element, EventHandler<DataTransferEventArgs> handler)
        {
            FrameworkElement.AddHandler(element, SourceUpdatedEvent, handler); 
        }
 
        /// <summary> 
        ///     Removes a handler for the SourceUpdated attached event
        /// </summary> 
        /// <param name="element">UIElement or ContentElement that listens to this event</param>
        /// <param name="handler">Event Handler to be removed</param>
        public static void RemoveSourceUpdatedHandler(DependencyObject element, EventHandler<DataTransferEventArgs> handler)
        { 
            FrameworkElement.RemoveHandler(element, SourceUpdatedEvent, handler);
        } 
 
        /// <summary>
        /// The TargetUpdated event is raised whenever a value is transferred from the source to the target, 
        /// but only for Bindings that have requested the event by setting BindFlags.NotifyOnTargetUpdated.
        /// </summary>
        public static readonly RoutedEvent TargetUpdatedEvent =
                EventManager.RegisterRoutedEvent("TargetUpdated", 
                                        RoutingStrategy.Bubble,
                                        typeof(EventHandler<DataTransferEventArgs>), 
                                        typeof(Binding)); 

        /// <summary> 
        ///     Adds a handler for the TargetUpdated attached event
        /// </summary>
        /// <param name="element">UIElement or ContentElement that listens to this event</param>
        /// <param name="handler">Event Handler to be added</param> 
        public static void AddTargetUpdatedHandler(DependencyObject element, EventHandler<DataTransferEventArgs> handler)
        { 
            FrameworkElement.AddHandler(element, TargetUpdatedEvent, handler); 
        }
 
        /// <summary>
        ///     Removes a handler for the TargetUpdated attached event
        /// </summary>
        /// <param name="element">UIElement or ContentElement that listens to this event</param> 
        /// <param name="handler">Event Handler to be removed</param>
        public static void RemoveTargetUpdatedHandler(DependencyObject element, EventHandler<DataTransferEventArgs> handler) 
        { 
            FrameworkElement.RemoveHandler(element, TargetUpdatedEvent, handler);
        } 


        // PreSharp uses message numbers that the C# compiler doesn't know about.
        // Disable the C# complaints, per the PreSharp documentation. 
        #pragma warning disable 1634, 1691
 
        // PreSharp checks that the type of the DP agrees with the type of the static 
        // accessors.  But setting the type of the DP to XmlNamespaceManager would
        // load System.Xml during the static cctor, which is considered a perf bug. 
        // So instead we set the type of the DP to 'object' and use the
        // ValidateValueCallback to ensure that only values of the right type are allowed.
        // Meanwhile, disable the PreSharp warning
        #pragma warning disable 7008 

        /// <summary> 
        /// The XmlNamespaceManager to use to perform Namespace aware XPath queries in XmlData bindings 
        /// </summary>
        public static readonly DependencyProperty XmlNamespaceManagerProperty= 
                DependencyProperty.RegisterAttached("XmlNamespaceManager", typeof(object), typeof(Binding),
                                            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits),
                                            new ValidateValueCallback(IsValidXmlNamespaceManager));
 
        /// <summary> Static accessor for XmlNamespaceManager property </summary>
        /// <exception cref="ArgumentNullException"> DependencyObject target cannot be null </exception> 
        public static XmlNamespaceManager GetXmlNamespaceManager(DependencyObject target) 
        {
            if (target == null) 
                throw new ArgumentNullException("target");

            return (XmlNamespaceManager)target.GetValue(XmlNamespaceManagerProperty);
        } 

        /// <summary> Static modifier for XmlNamespaceManager property </summary> 
        /// <exception cref="ArgumentNullException"> DependencyObject target cannot be null </exception> 
        public static void SetXmlNamespaceManager(DependencyObject target, XmlNamespaceManager value)
        { 
            if (target == null)
                throw new ArgumentNullException("target");

            target.SetValue(XmlNamespaceManagerProperty, value); 
        }
 
        private static bool IsValidXmlNamespaceManager(object value) 
        {
            return (value == null) || AssemblyHelper.IsXmlNamespaceManager(value); 
        }

        #pragma warning restore 7008
        #pragma warning restore 1634, 1691 

 
        //------------------------------------------------------ 
        //
        //  Constructors 
        //
        //------------------------------------------------------

        /// <summary> 
        /// Default constructor.
        /// </summary> 
        public Binding() {} 

        /// <summary> 
        /// Convenience constructor.  Sets most fields to default values.
        /// </summary>
        /// <param name="path">source path </param>
        public Binding(string path) 
        {
            if (path != null) 
            { 
                if (System.Windows.Threading.Dispatcher.CurrentDispatcher == null)
                    throw new InvalidOperationException();  // This is actually never called since CurrentDispatcher will throw if null. 

                _ppath = new PropertyPath(path);
                _attachedPropertiesInPath = -1;
            } 
        }
 
        //----------------------------------------------------- 
        //
        //  Public Properties 
        //
        //------------------------------------------------------

        /// <summary> 
        ///     Collection&lt;ValidationRule&gt; is a collection of ValidationRule
        ///     implementations on either a Binding or a MultiBinding.  Each of the rules 
        ///     is run by the binding engine when validation on update to source 
        /// </summary>
        public Collection<ValidationRule> ValidationRules 
        {
            get
            {
                if (_validationRules == null) 
                    _validationRules = new ValidationRuleCollection();
 
                return _validationRules; 
            }
 
        }

        /// <summary>
        /// This method is used by TypeDescriptor to determine if this property should 
        /// be serialized.
        /// </summary> 
        [EditorBrowsable(EditorBrowsableState.Never)] 
        public bool ShouldSerializeValidationRules()
        { 
            return (_validationRules != null && _validationRules.Count > 0);
        }

        /// <summary> True if an exception during source updates should be considered a validation error.</summary> 
        [DefaultValue(false)]
        public bool ValidatesOnExceptions 
        { 
            get
            { 
                return TestFlag(BindingFlags.ValidatesOnExceptions);
            }
            set
            { 
                bool currentValue = TestFlag(BindingFlags.ValidatesOnExceptions);
                if (currentValue != value) 
                { 
                    CheckSealed();
                    ChangeFlag(BindingFlags.ValidatesOnExceptions, value); 
                }
            }
        }
 
        /// <summary> True if a data error in the source item should be considered a validation error.</summary>
        [DefaultValue(false)] 
        public bool ValidatesOnDataErrors 
        {
            get 
            {
                return TestFlag(BindingFlags.ValidatesOnDataErrors);
            }
            set 
            {
                bool currentValue = TestFlag(BindingFlags.ValidatesOnDataErrors); 
                if (currentValue != value) 
                {
                    CheckSealed(); 
                    ChangeFlag(BindingFlags.ValidatesOnDataErrors, value);
                }
            }
        } 

 
        /// <summary> The source path (for CLR bindings).</summary> 
        public PropertyPath Path
        { 
            get { return _ppath; }
            set
            {
                CheckSealed(); 

                _ppath = value; 
                _attachedPropertiesInPath = -1; 
                ClearFlag(BindingFlags.PathGeneratedInternally);
            } 
        }

        /// <summary>
        /// This method is used by TypeDescriptor to determine if this property should 
        /// be serialized.
        /// </summary> 
        [EditorBrowsable(EditorBrowsableState.Never)] 
        public bool ShouldSerializePath()
        { 
            return _ppath != null && !TestFlag(BindingFlags.PathGeneratedInternally);
        }

        /// <summary> The XPath path (for XML bindings).</summary> 
        [DefaultValue(null)]
        public string XPath 
        { 
            get { return _xpath; }
            set 
            {
                CheckSealed();

                _xpath = value; 
            }
        } 
 
        /// <summary> Binding mode </summary>
        [DefaultValue(BindingMode.Default)] 
        public BindingMode Mode
        {
            get
            { 
                switch (GetFlagsWithinMask(BindingFlags.PropagationMask))
                { 
                    case BindingFlags.OneWay:           return BindingMode.OneWay; 
                    case BindingFlags.TwoWay:           return BindingMode.TwoWay;
                    case BindingFlags.OneWayToSource:   return BindingMode.OneWayToSource; 
                    case BindingFlags.OneTime:          return BindingMode.OneTime;
                    case BindingFlags.PropDefault:      return BindingMode.Default;
                }
                Invariant.Assert(false, "Unexpected BindingMode value"); 
                return 0;
            } 
            set 
            {
                CheckSealed(); 

                BindingFlags flags = FlagsFrom(value);
                if (flags == BindingFlags.IllegalInput)
                    throw new InvalidEnumArgumentException("value", (int) value, typeof(BindingMode)); 

                ChangeFlagsWithinMask(BindingFlags.PropagationMask, flags); 
            } 
        }
 
        /// <summary> Update type </summary>
        [DefaultValue(UpdateSourceTrigger.Default)]
        public UpdateSourceTrigger UpdateSourceTrigger
        { 
            get
            { 
                switch (GetFlagsWithinMask(BindingFlags.UpdateMask)) 
                {
                    case BindingFlags.UpdateOnPropertyChanged: return UpdateSourceTrigger.PropertyChanged; 
                    case BindingFlags.UpdateOnLostFocus:    return UpdateSourceTrigger.LostFocus;
                    case BindingFlags.UpdateExplicitly:     return UpdateSourceTrigger.Explicit;
                    case BindingFlags.UpdateDefault:        return UpdateSourceTrigger.Default;
                } 
                Invariant.Assert(false, "Unexpected UpdateSourceTrigger value");
                return 0; 
            } 
            set
            { 
                CheckSealed();

                BindingFlags flags = FlagsFrom(value);
                if (flags == BindingFlags.IllegalInput) 
                    throw new InvalidEnumArgumentException("value", (int) value, typeof(UpdateSourceTrigger));
 
                ChangeFlagsWithinMask(BindingFlags.UpdateMask, flags); 
            }
        } 

        /// <summary> Raise SourceUpdated event whenever a value flows from target to source </summary>
        [DefaultValue(false)]
        public bool NotifyOnSourceUpdated 
        {
            get 
            { 
                return TestFlag(BindingFlags.NotifyOnSourceUpdated);
            } 
            set
            {
                bool currentValue = TestFlag(BindingFlags.NotifyOnSourceUpdated);
                if (currentValue != value) 
                {
                    CheckSealed(); 
                    ChangeFlag(BindingFlags.NotifyOnSourceUpdated, value); 
                }
            } 
        }


        /// <summary> Raise TargetUpdated event whenever a value flows from source to target </summary> 
        [DefaultValue(false)]
        public bool NotifyOnTargetUpdated 
        { 
            get
            { 
                return TestFlag(BindingFlags.NotifyOnTargetUpdated);
            }
            set
            { 
                bool currentValue = TestFlag(BindingFlags.NotifyOnTargetUpdated);
                if (currentValue != value) 
                { 
                    CheckSealed();
                    ChangeFlag(BindingFlags.NotifyOnTargetUpdated, value); 
                }
            }
        }
 
        /// <summary> Raise ValidationError event whenever there is a ValidationError on Update</summary>
        [DefaultValue(false)] 
        public bool NotifyOnValidationError 
        {
            get 
            {
                return TestFlag(BindingFlags.NotifyOnValidationError);
            }
            set 
            {
                bool currentValue = TestFlag(BindingFlags.NotifyOnValidationError); 
                if (currentValue != value) 
                {
                    CheckSealed(); 
                    ChangeFlag(BindingFlags.NotifyOnValidationError, value);
                }
            }
        } 

        /// <summary> The Converter to apply </summary> 
        [DefaultValue(null)] 
        public IValueConverter Converter
        { 
            get { return _converter; }
            set { CheckSealed();  _converter = value; }
        }
 
        /// <summary>
        /// The parameter to pass to converter. 
        /// </summary> 
        /// <value></value>
        [DefaultValue(null)] 
        public object ConverterParameter
        {
            get { return _converterParameter; }
            set { CheckSealed();  _converterParameter = value; } 
        }
 
        /// <summary> Culture in which to evaluate the converter </summary> 
        [DefaultValue(null)]
        [TypeConverter(typeof(System.Windows.CultureInfoIetfLanguageTagConverter))] 
        public CultureInfo ConverterCulture
        {
            get { return _culture; }
            set { CheckSealed();  _culture = value; } 
        }
 
        /// <summary> object to use as the source </summary> 
        /// <remarks> To clear this property, set it to DependencyProperty.UnsetValue. </remarks>
        public object Source 
        {
            get
            {
                if (_objectSource == null) 
                    return null;
                return _objectSource.Target; 
            } 
            set
            { 
                CheckSealed();

                if (_sourceInUse == SourceProperties.None || _sourceInUse == SourceProperties.Source)
                { 
                    if (value != DependencyProperty.UnsetValue)
                    { 
                        _objectSource = new WeakReference(value); 
                        SourceReference = new ExplicitObjectRef(value);
                    } 
                    else
                    {
                        _objectSource = null;
                        SourceReference = null; 
                    }
                } 
                else 
                    throw new InvalidOperationException(SR.Get(SRID.BindingConflict, SourceProperties.Source, _sourceInUse));
            } 
        }

        /// <summary>
        /// This method is used by TypeDescriptor to determine if this property should 
        /// be serialized.
        /// </summary> 
        [EditorBrowsable(EditorBrowsableState.Never)] 
        public bool ShouldSerializeSource()
        { 
            //return _objectSource.IsAlive && _objectSource.Target != DependencyProperty.UnsetValue;

            // M8.2: always false
            return false; 
        }
 
        /// <summary> 
        /// Description of the object to use as the source, relative to the target element.
        /// </summary> 
        [DefaultValue(null)]
        public RelativeSource RelativeSource
        {
            get { return _relativeSource; } 
            set
            { 
                CheckSealed(); 

                if (_sourceInUse == SourceProperties.None || _sourceInUse == SourceProperties.RelativeSource) 
                {
                    _relativeSource = value;
                    SourceReference = (value != null) ? new RelativeObjectRef(value) : null;
                } 
                else
                    throw new InvalidOperationException(SR.Get(SRID.BindingConflict, SourceProperties.RelativeSource, _sourceInUse)); 
            } 
        }
 
        /// <summary> Name of the element to use as the source </summary>
        [DefaultValue(null)]
        public string ElementName
        { 
            get { return _elementSource; }
            set 
            { 
                CheckSealed();
 
                if (_sourceInUse == SourceProperties.None || _sourceInUse == SourceProperties.ElementName)
                {
                    _elementSource = value;
                    SourceReference = (value != null) ? new ElementObjectRef(value) : null; 
                }
                else 
                    throw new InvalidOperationException(SR.Get(SRID.BindingConflict, SourceProperties.ElementName, _sourceInUse)); 
            }
        } 

        /// <summary> True if Binding should get/set values asynchronously </summary>
        [DefaultValue(false)]
        public bool IsAsync 
        {
            get { return _isAsync; } 
            set { CheckSealed();  _isAsync = value; } 
        }
 
        /// <summary> Opaque data passed to the asynchronous data dispatcher </summary>
        [DefaultValue(null)]
        public object AsyncState
        { 
            get { return _asyncState; }
            set { CheckSealed();  _asyncState = value; } 
        } 

        /// <summary> True if Binding should interpret its path relative to 
        /// the data item itself.
        /// </summary>
        /// <remarks>
        /// The normal behavior (when this property is false) 
        /// includes special treatment for a data item that implements IDataSource.
        /// In this case, the path is treated relative to the object obtained 
        /// from the IDataSource.Data property.  In addition, the binding listens 
        /// for the IDataSource.DataChanged event and reacts accordingly.
        /// Setting this property to true overrides this behavior and gives 
        /// the binding access to properties on the data source object itself.
        /// </remarks>
        [DefaultValue(false)]
        public bool BindsDirectlyToSource 
        {
            get { return _bindsDirectlyToSource; } 
            set { CheckSealed();  _bindsDirectlyToSource = value; } 
        }
 
        /// <summary>
        /// called whenever any exception is encountered when trying to update
        /// the value to the source. The application author can provide its own
        /// handler for handling exceptions here. If the delegate returns 
        ///     null - don't throw an error or provide a ValidationError.
        ///     Exception - returns the exception itself, we will fire the exception using Async exception model. 
        ///     ValidationError - it will set itself as the BindingInError and add it to the element's Validation errors. 
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] 
        public UpdateSourceExceptionFilterCallback UpdateSourceExceptionFilter
        {
            get
            { 
                return _exceptionFilterCallback;
            } 
 
            set
            { 
                _exceptionFilterCallback = value;
            }
        }
 
        //-----------------------------------------------------
        // 
        //  Public Fields 
        //
        //----------------------------------------------------- 

        /// <summary>
        ///     A source property or a converter can return Binding.DoNothing
        ///     to instruct the binding engine to do nothing (i.e. do not transfer 
        ///     a value to the target, do not move to the next Binding in a
        ///     PriorityBinding, do not use the fallback or default value). 
        /// </summary> 
        public static readonly object DoNothing = new NamedObject("Binding.DoNothing");
 
        /// <summary>
        ///     This string is used as the PropertyName of the
        ///     PropertyChangedEventArgs to indicate that an indexer property
        ///     has been changed. 
        /// </summary>
        public const string IndexerName = "Item[]"; 
 
        //-----------------------------------------------------
        // 
        //  Protected Methods
        //
        //------------------------------------------------------
 
        /// <summary>
        /// Create an appropriate expression for this Binding, to be attached 
        /// to the given DependencyProperty on the given DependencyObject. 
        /// </summary>
        internal override BindingExpressionBase CreateBindingExpressionOverride(DependencyObject target, DependencyProperty dp, BindingExpressionBase owner) 
        {
            return BindingExpression.CreateBindingExpression(target, dp, this, owner);
        }
 
        internal override ValidationRule LookupValidationRule(Type type)
        { 
            return LookupValidationRule(type, ValidationRulesInternal); 
        }
 
        //-----------------------------------------------------
        //
        //  Internal Methods
        // 
        //------------------------------------------------------
 
        internal object DoFilterException(object bindExpr, Exception exception) 
        {
            if (_exceptionFilterCallback != null) 
                return _exceptionFilterCallback(bindExpr, exception);

            return exception;
        } 

        // called by BindingExpression when the Binding doesn't specify a path. 
        // (Can't use Path setter, since that replaces the BindingExpression.) 
        internal void UsePath(PropertyPath path)
        { 
            _ppath = path;
            SetFlag(BindingFlags.PathGeneratedInternally);
        }
 
        internal override BindingBase CreateClone()
        { 
            return new Binding(); 
        }
 
        internal override void InitializeClone(BindingBase baseClone, BindingMode mode)
        {
            Binding clone = (Binding)baseClone;
 
            clone._ppath = _ppath;
            clone._xpath = _xpath; 
            clone._source = _source; 
            clone._culture = _culture;
            clone._isAsync = _isAsync; 
            clone._asyncState = _asyncState;
            clone._bindsDirectlyToSource = _bindsDirectlyToSource;
            clone._doesNotTransferDefaultValue = _doesNotTransferDefaultValue;
            clone._objectSource = _objectSource; 
            clone._relativeSource = _relativeSource;
            clone._converter = _converter; 
            clone._converterParameter = _converterParameter; 
            clone._attachedPropertiesInPath = _attachedPropertiesInPath;
            clone._validationRules = _validationRules; 

            base.InitializeClone(baseClone, mode);
        }
 
        //------------------------------------------------------
        // 
        //  Internal Properties 
        //
        //----------------------------------------------------- 

        internal override CultureInfo ConverterCultureInternal
        {
            get { return ConverterCulture; } 
        }
 
        internal ObjectRef SourceReference 
        {
            get { return (_source == UnsetSource) ? null : _source; } 
            set { CheckSealed();  _source = value;  DetermineSource(); }
        }

        internal object WorkerData 
        {
            get { return _workerData; } 
            set { _workerData = value; } 
        }
 
        internal bool TreeContextIsRequired
        {
            get
            { 
                bool treeContextIsRequired;
 
                // attached properties in the property path (like "(DockPanel.Dock)") 
                // need inherited value of XmlAttributeProperties properties for namespaces,
                // unless the properties are pre-resolved by the parser 
                if (_attachedPropertiesInPath < 0)
                {
                    if (_ppath != null)
                    { 
                        _attachedPropertiesInPath = _ppath.ComputeUnresolvedAttachedPropertiesInPath();
                    } 
                    else 
                    {
                        _attachedPropertiesInPath = 0; 
                    }
                }
                treeContextIsRequired = (_attachedPropertiesInPath > 0);
 
                // namespace prefixes in the XPath need an XmlNamespaceManager
                if (!treeContextIsRequired && !String.IsNullOrEmpty(XPath) && XPath.IndexOf(':') >= 0) 
                { 
                    treeContextIsRequired = true;
                } 

                return treeContextIsRequired;
            }
        } 

        // same as the public ValidationRules property, but 
        // doesn't try to create an instance if there isn't one there 
        internal override Collection<ValidationRule> ValidationRulesInternal
        { 
            get
            {
                return _validationRules;
            } 
        }
 
        // when the source property has its default value, this flag controls 
        // whether the binding transfers the value anyway, or simply "hides"
        // so that the property engine obtains the target value some other way. 
        internal bool TransfersDefaultValue
        {
            get { return !_doesNotTransferDefaultValue; }
            set { CheckSealed();  _doesNotTransferDefaultValue = !value; } 
        }
 
 
        //------------------------------------------------------
        // 
        //  Private Methods
        //
        //-----------------------------------------------------
 
        // determine the source property currently in use
        void DetermineSource() 
        { 
            _sourceInUse =
                (_source == UnsetSource)                ? SourceProperties.None : 
                (_relativeSource != null)               ? SourceProperties.RelativeSource :
                (_elementSource != null)                ? SourceProperties.ElementName :
                (_objectSource != null)                 ? SourceProperties.Source :
                                                          SourceProperties.InternalSource; 
        }
 
        //----------------------------------------------------- 
        //
        //  Private Fields 
        //
        //-----------------------------------------------------

        object              _workerData; 
        SourceProperties    _sourceInUse;
 
        PropertyPath        _ppath; 
        string              _xpath;
        ObjectRef           _source = UnsetSource; 
        CultureInfo         _culture;

        bool                _isAsync;
        object              _asyncState; 
        bool                _bindsDirectlyToSource;
        bool                _doesNotTransferDefaultValue;   // initially = false 
 
        WeakReference       _objectSource;
        RelativeSource      _relativeSource; 
        string              _elementSource;
        IValueConverter     _converter;
        object              _converterParameter;
 
        int                 _attachedPropertiesInPath;
        ValidationRuleCollection _validationRules; 
        UpdateSourceExceptionFilterCallback _exceptionFilterCallback; 

        static readonly ObjectRef UnsetSource = new ExplicitObjectRef(null); 
    }
}


