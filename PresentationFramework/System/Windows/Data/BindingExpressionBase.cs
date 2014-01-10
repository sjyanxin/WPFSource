//---------------------------------------------------------------------------- 
//
// <copyright file="BindingExpressionBase.cs" company="Microsoft">
//    Copyright (C) Microsoft Corporation.  All rights reserved.
// </copyright> 
//
// Description: Defines BindingExpressionBase object, 
//              base class for BindingExpression, PriorityBindingExpression, 
//              and MultiBindingExpression.
// 
// See spec at http://avalon/connecteddata/Specs/Data%20Binding.mht
//
//---------------------------------------------------------------------------
 
// build with this symbol defined to catch errors about not using
// BindingExpression.GetReference correctly 
//#define USE_ITEM_REFERENCE 

using System; 
using System.Collections.ObjectModel;   // Collection<T>
using System.ComponentModel;        // TypeConverter
using System.Diagnostics;           // StackTrace
using System.Globalization;         // CultureInfo 
using System.Threading;             // Thread
 
using System.Windows;               // FrameworkElement 
using System.Windows.Controls;      // Validation
using System.Windows.Markup;        // XmlLanguage 
using System.Windows.Threading;     // Dispatcher
using MS.Internal;                  // Invariant.Assert
using MS.Internal.Controls;         // ValidationErrorCollection
using MS.Internal.Data;             // DataBindEngine 
using MS.Internal.KnownBoxes;       // BooleanBoxes
using MS.Internal.Utility;          // TraceLog 
 
namespace System.Windows.Data
{ 

    /// <summary>
    /// Base class for Binding Expressions.
    /// </summary> 
    public abstract class BindingExpressionBase : Expression, IWeakEventListener
    { 
 
        // Flags indicating run-time properties of a BindingExpression
        [Flags] 
        internal enum BindingFlags
        {
            // names used by Binding
 
            OneWay                  = PrivateFlags.iSourceToTarget,
            TwoWay                  = PrivateFlags.iSourceToTarget | PrivateFlags.iTargetToSource, 
            OneWayToSource          = PrivateFlags.iTargetToSource, 
            OneTime                 = 0,
            PropDefault             = PrivateFlags.iPropDefault, 
            NotifyOnTargetUpdated   = PrivateFlags.iNotifyOnTargetUpdated,
            NotifyOnSourceUpdated   = PrivateFlags.iNotifyOnSourceUpdated,
            NotifyOnValidationError = PrivateFlags.iNotifyOnValidationError,
            UpdateOnPropertyChanged = 0, 
            UpdateOnLostFocus       = PrivateFlags.iUpdateOnLostFocus,
            UpdateExplicitly        = PrivateFlags.iUpdateExplicitly, 
            UpdateDefault           = PrivateFlags.iUpdateDefault, 
            PathGeneratedInternally = PrivateFlags.iPathGeneratedInternally,
            ValidatesOnExceptions   = PrivateFlags.iValidatesOnExceptions, 
            ValidatesOnDataErrors   = PrivateFlags.iValidatesOnDataErrors,

            Default                 = PropDefault | UpdateDefault,
 
            /// <summary> Error value, returned by FlagsFrom to indicate faulty input</summary>
            IllegalInput                = PrivateFlags.iIllegalInput, 
 
            PropagationMask = OneWay | TwoWay | OneWayToSource | OneTime | PropDefault,
            UpdateMask      = UpdateOnPropertyChanged | UpdateOnLostFocus | UpdateExplicitly | UpdateDefault, 
        }

        [Flags]
        private enum PrivateFlags 
        {
            // internal use 
 
            iSourceToTarget             = 0x00000001,
            iTargetToSource             = 0x00000002, 
            iPropDefault                = 0x00000004,
            iNotifyOnTargetUpdated      = 0x00000008,
            iDefaultValueConverter      = 0x00000010,
            iInTransfer                 = 0x00000020, 
            iInUpdate                   = 0x00000040,
            iTransferPending            = 0x00000080, 
            iNeedDataTransfer           = 0x00000100, 
            iTransferDeferred           = 0x00000200,   // used by MultiBindingExpression
            iUpdateOnLostFocus          = 0x00000400, 
            iUpdateExplicitly           = 0x00000800,
            iUpdateDefault              = iUpdateExplicitly | iUpdateOnLostFocus,
            iNeedUpdate                 = 0x00001000,
            iPathGeneratedInternally    = 0x00002000, 
            iUsingMentor                = 0x00004000,
            iResolveNamesInTemplate     = 0x00008000, 
            iDetaching                  = 0x00010000, 
            iNeedsCollectionView        = 0x00020000,
            iInPriorityBindingExpression= 0x00040000, 
            iInMultiBindingExpression   = 0x00080000,
            iUsingFallbackValue         = 0x00100000,
            iNotifyOnValidationError    = 0x00200000,
            iAttaching                  = 0x00400000, 
            iNotifyOnSourceUpdated      = 0x00800000,
            iValidatesOnExceptions      = 0x01000000, 
            iValidatesOnDataErrors      = 0x02000000, 
            iIllegalInput               = 0x04000000,
            iNeedsValidation            = 0x08000000, 

            iPropagationMask = iSourceToTarget | iTargetToSource | iPropDefault,
            iUpdateMask      = iUpdateOnLostFocus | iUpdateExplicitly,
        } 

        //----------------------------------------------------- 
        // 
        //  Constructors
        // 
        //-----------------------------------------------------

        /// <summary> Constructor </summary>
        internal BindingExpressionBase(BindingBase binding, BindingExpressionBase parent) : base(ExpressionMode.SupportsUnboundSources) 
        {
            if (binding == null) 
                throw new ArgumentNullException("binding"); 

            _binding = binding; 
            _parentBindingExpression = parent;

            _flags = (PrivateFlags)binding.Flags;
 
            if (parent != null)
            { 
                ResolveNamesInTemplate = parent.ResolveNamesInTemplate; 

                Type type = parent.GetType(); 
                if (type == typeof(MultiBindingExpression))
                    ChangeFlag(PrivateFlags.iInMultiBindingExpression, true);
                else if (type == typeof(PriorityBindingExpression))
                    ChangeFlag(PrivateFlags.iInPriorityBindingExpression, true); 
            }
 
            // initialize tracing information 
            PresentationTraceLevel traceLevel = PresentationTraceSources.GetTraceLevel(binding);
 
            if (traceLevel > 0)
            {
                // copy TraceLevel from parent BindingBase - it can be changed later
                PresentationTraceSources.SetTraceLevel(this, traceLevel); 
            }
 
            if (TraceData.IsExtendedTraceEnabled(this, TraceDataLevel.CreateExpression)) 
            {
                if (parent == null) 
                {
                    TraceData.Trace(TraceEventType.Warning,
                                        TraceData.CreatedExpression(
                                            TraceData.Identify(this), 
                                            TraceData.Identify(binding)));
                } 
                else 
                {
                    TraceData.Trace(TraceEventType.Warning, 
                                        TraceData.CreatedExpressionInParent(
                                            TraceData.Identify(this),
                                            TraceData.Identify(binding),
                                            TraceData.Identify(parent))); 
                }
            } 
 
            if (LookupValidationRule(typeof(ExceptionValidationRule)) != null)
            { 
                ChangeFlag(PrivateFlags.iValidatesOnExceptions, true);
            }

            if (LookupValidationRule(typeof(DataErrorValidationRule)) != null) 
            {
                ChangeFlag(PrivateFlags.iValidatesOnDataErrors, true); 
            } 
        }
 
        //------------------------------------------------------
        //
        //  Public Properties
        // 
        //-----------------------------------------------------
 
        /// <summary> Binding from which this expression was created </summary> 
        public BindingBase  ParentBindingBase  { get { return _binding; } }
 
        /// <summary>Status of the BindingExpression</summary>
        public BindingStatus Status { get { return _status; } }

        /// <summary> 
        ///     The ValidationError that caused this
        ///     BindingExpression to be invalid. 
        /// </summary> 
        public virtual ValidationError ValidationError
        { 
            get
            {
                return _validationError;
            } 
        }
 
        /// <summary> 
        ///     HasError returns true if any of the ValidationRules
        ///     in the ParentBinding failed its validation rule. 
        /// </summary>
        public virtual bool HasError
        {
            get 
            {
                return _validationError != null; 
            } 
        }
 
        //------------------------------------------------------
        //
        //  Public Methods
        // 
        //------------------------------------------------------
 
        /// <summary> Force a data transfer from source to target </summary> 
        public virtual void UpdateTarget()
        { 
        }

        /// <summary> Send the current value back to the source </summary>
        /// <remarks> Does nothing when binding's Mode is not TwoWay or OneWayToSource </remarks> 
        public virtual void UpdateSource()
        { 
        } 

        /// <summary> 
        ///     Run UI-side validation rules on the proposed value held by this
        ///     binding expression, but do not write the proposed value back to
        ///     the source, or run source-side validation rules.
        /// </summary> 
        /// <returns>
        ///     True, if the validation rules all pass, or if there is no proposed 
        ///     value to check.   False, otherwise. 
        /// </returns>
        /// <remarks> 
        ///     "UI-side" validation rules are those whose validation step is
        ///     RawProposedValue or ConvertedProposedValue.  Previous validation
        ///     errors for these rules are cleared, and new errors discovered for
        ///     these rules are added to the Validation.Errors collections.  The 
        ///     Validation.HasError property, validation adorner feedback, and
        ///     validation error events are updated appropriately. 
        /// </remarks> 
        public bool ValidateWithoutUpdate()
        { 
            if (!NeedsValidation)
                return true;

            Collection<ProposedValue> values; 
            return ValidateAndConvertProposedValue(out values);
        } 
 
#region Expression overrides
 
        /// <summary>
        ///     Notification that the Expression has been set as a property's value
        /// </summary>
        /// <remarks> 
        ///     Subclasses should not override OnAttach(), but must override Attach()
        /// </remarks> 
        /// <param name="d">DependencyObject being set</param> 
        /// <param name="dp">Property being set</param>
        internal sealed override void OnAttach(DependencyObject d, DependencyProperty dp) 
        {
            if (d == null)
                throw new ArgumentNullException("d");
            if (dp == null) 
                throw new ArgumentNullException("dp");
 
            Attach(d, dp); 
        }
 
        /// <summary>
        ///     Notification that the Expression has been removed as a property's value
        /// </summary>
        /// <remarks> 
        ///     Subclasses should not override OnDetach(), but must override DetachOverride()
        /// </remarks> 
        /// <param name="d">DependencyObject being cleared</param> 
        /// <param name="dp">Property being cleared</param>
        internal sealed override void OnDetach(DependencyObject d, DependencyProperty dp) 
        {
            Detach();
        }
 
        /// <summary>
        ///     Notification that a Dependent that this Expression established has 
        ///     been invalidated as a result of a Source invalidation 
        /// </summary>
        /// <param name="d">DependencyObject that was invalidated</param> 
        /// <param name="args">Changed event args for the property that was invalidated</param>
        internal override void OnPropertyInvalidation(DependencyObject d, DependencyPropertyChangedEventArgs args)
        {
            // It's possible to receive this notification after we've unsubscribed to it. 
            // This can happen if the sender raises the notification to a list of
            // clients and one of the earlier clients causes a later client to 
            // unsubscribe.  The sender uses a copy of the list, so the later client 
            // will still get the notification.  (See Dev10 bug 715390)
            // If this happens, simply ignore the notification. 
            if (_status == BindingStatus.Detached)
                return;

            IsTransferPending = true; 

            // if the notification arrived on the right Dispatcher, handle it now. 
            if (Dispatcher.Thread == Thread.CurrentThread) 
            {
                HandlePropertyInvalidation(d, args); 
            }
            else    // Otherwise, marshal it to the right Dispatcher.
            {
                Dispatcher.BeginInvoke( 
                    DispatcherPriority.DataBind,
                    new DispatcherOperationCallback(HandlePropertyInvalidationOperation), 
                    new object[]{d, args}); 
            }
        } 

        /// <summary>
        ///     List of sources of the Expression
        /// </summary> 
        /// <returns>Sources list</returns>
        internal override DependencySource[] GetSources() 
        { 
            int j, k;
            int n = (_sources != null) ? _sources.Length : 0; 
            if (n == 0)
                return null;

            DependencySource[] result = new DependencySource[n]; 

            // convert the weak references into strong ones 
            for (j=0, k=0; k<n; ++k) 
            {
                DependencyObject d = _sources[k].DependencyObject; 
                if (d != null)
                {
                    result[j++] = new DependencySource(d, _sources[k].DependencyProperty);
                } 
            }
 
            // if any of the references died, return a shortened list 
            if (j < n)
            { 
                DependencySource[] temp = result;
                result = new DependencySource[j];
                Array.Copy(temp, 0, result, 0, j);
            } 

            return result; 
        } 

        // We need this Clone method to copy a binding during Freezable.Copy.  We shouldn't be taking 
        // the target object/dp parameters here, but Binding.ProvideValue requires it.  (Binding
        // could probably be re-factored so that we don't need this).
        internal override Expression Copy( DependencyObject targetObject, DependencyProperty targetDP )
        { 
            return ParentBindingBase.CreateBindingExpression(targetObject, targetDP);
        } 
 
#endregion  Expression overrides
 
        /// <summary>
        /// Handle events from the centralized event table
        /// </summary>
        bool IWeakEventListener.ReceiveWeakEvent(Type managerType, object sender, EventArgs e) 
        {
            return ReceiveWeakEvent(managerType, sender, e); 
        } 

#region BindingExpressions with no target DP 

        //-----------------------------------------------------
        //
        //  API for BindingExpressions with no target DP (task 20769) 
        //  This is internal for now, until we review whether we wish to
        //  make it public 
        // 
        //------------------------------------------------------
 
        /// <summary> Create an untargeted BindingExpression </summary>
        internal static BindingExpressionBase CreateUntargetedBindingExpression(DependencyObject d, BindingBase binding)
        {
            return binding.CreateBindingExpression(d, NoTargetProperty); 
        }
 
        /// <summary> Attach the BindingExpression to its target element </summary> 
        /// <remarks>
        /// This method must be called once during the initialization. 
        /// </remarks>
        /// <param name="d">The target element </param>
        internal void Attach(DependencyObject d)
        { 
            Attach(d, NoTargetProperty);
        } 
 
        /// <summary> This event is raised when the BindingExpression's value changes </summary>
        internal event EventHandler<BindingValueChangedEventArgs> ValueChanged; 

        /* The following APIs are also needed for untargeted bindings, but they already exist
        for other reasons.
 
        /// <summary> The current value of the BindingExpression </summary>
        internal object Value { get; set; } 
 
        /// <summary> Activate the BindingExpression, using the given item as its root item. </summary>
        internal void Activate(object item) {} 

        /// <summary> Deactivate the BindingExpression. </summary>
        internal void Deactivate() {}
 
        /// <summary> Detach the BindingExpression from its target element </summary>
        /// <remarks> 
        /// This method must be called once when the BindingExpression is no longer needed. 
        /// </remarks>
        internal void Detach() {} 

        */

#endregion BindingExpressions with no target DP 

        //----------------------------------------------------- 
        // 
        //  Protected Properties
        // 
        //-----------------------------------------------------

        /// <summary> True if this binding expression is attaching </summary>
        internal bool IsAttaching 
        {
            get { return TestFlag(PrivateFlags.iAttaching); } 
            set { ChangeFlag(PrivateFlags.iAttaching, value); } 
        }
 
        /// <summary> True if this binding expression is detaching </summary>
        internal bool IsDetaching
        {
            get { return TestFlag(PrivateFlags.iDetaching); } 
            set { ChangeFlag(PrivateFlags.iDetaching, value); }
        } 
 
        /// <summary> True if this binding expression updates the target </summary>
        internal bool IsDynamic 
        {
            get
            {
                return (    TestFlag(PrivateFlags.iSourceToTarget) 
                        &&  (!IsInMultiBindingExpression || ParentMultiBindingExpression.IsDynamic));
            } 
        } 

        /// <summary> True if this binding expression updates the source </summary> 
        internal bool IsReflective
        {
            get
            { 
                return (    TestFlag(PrivateFlags.iTargetToSource)
                        &&  (!IsInMultiBindingExpression || ParentMultiBindingExpression.IsReflective)); 
            } 
            set { ChangeFlag(PrivateFlags.iTargetToSource, value); }
        } 

        /// <summary> True if this binding expression uses a default ValueConverter </summary>
        internal bool UseDefaultValueConverter
        { 
            get { return TestFlag(PrivateFlags.iDefaultValueConverter); }
            set { ChangeFlag(PrivateFlags.iDefaultValueConverter, value); } 
        } 

        /// <summary> True if this binding expression is OneWayToSource </summary> 
        internal bool IsOneWayToSource
        {
            get { return (_flags & PrivateFlags.iPropagationMask) == PrivateFlags.iTargetToSource; }
        } 

        /// <summary> True if this binding expression updates on PropertyChanged </summary> 
        internal bool IsUpdateOnPropertyChanged 
        {
            get { return (_flags & PrivateFlags.iUpdateMask) == 0; } 
        }

        /// <summary> True if this binding expression updates on LostFocus </summary>
        internal bool IsUpdateOnLostFocus 
        {
            get { return TestFlag(PrivateFlags.iUpdateOnLostFocus); } 
        } 

        /// <summary> True if this binding expression has a pending target update </summary> 
        internal bool IsTransferPending
        {
            get { return TestFlag(PrivateFlags.iTransferPending); }
            set { ChangeFlag(PrivateFlags.iTransferPending, value); } 
        }
 
        /// <summary> True if this binding expression is deferring a target update </summary> 
        internal bool TransferIsDeferred
        { 
            get { return TestFlag(PrivateFlags.iTransferDeferred); }
            set { ChangeFlag(PrivateFlags.iTransferDeferred, value); }
        }
 
        /// <summary> True if this binding expression is updating the target </summary>
        internal bool IsInTransfer 
        { 
            get { return TestFlag(PrivateFlags.iInTransfer); }
            set { ChangeFlag(PrivateFlags.iInTransfer, value); } 
        }

        /// <summary> True if this binding expression is updating the source </summary>
        internal bool IsInUpdate 
        {
            get { return TestFlag(PrivateFlags.iInUpdate); } 
            set { ChangeFlag(PrivateFlags.iInUpdate, value); } 
        }
 
        /// <summary> True if this binding expression is using the fallback value </summary>
        internal bool UsingFallbackValue
        {
            get { return TestFlag(PrivateFlags.iUsingFallbackValue); } 
            set { ChangeFlag(PrivateFlags.iUsingFallbackValue, value); }
        } 
 
        /// <summary> True if this binding expression uses the mentor of the target element </summary>
        internal bool UsingMentor 
        {
            get { return TestFlag(PrivateFlags.iUsingMentor); }
            set { ChangeFlag(PrivateFlags.iUsingMentor, value); }
        } 

        /// <summary> True if this binding expression should resolve ElementName within the template of the target element </summary> 
        internal bool ResolveNamesInTemplate 
        {
            get { return TestFlag(PrivateFlags.iResolveNamesInTemplate); } 
            set { ChangeFlag(PrivateFlags.iResolveNamesInTemplate, value); }
        }

        /// <summary> True if this binding expression has a pending target update </summary> 
        internal bool NeedsDataTransfer
        { 
            get { return TestFlag(PrivateFlags.iNeedDataTransfer); } 
            set { ChangeFlag(PrivateFlags.iNeedDataTransfer, value); }
        } 

        /// <summary> True if this binding expression has a pending source update </summary>
        internal bool NeedsUpdate
        { 
            get { return TestFlag(PrivateFlags.iNeedUpdate); }
            set 
            { 
                ChangeFlag(PrivateFlags.iNeedUpdate, value);
                if (value) 
                {
                    NeedsValidation = true;
                }
            } 
        }
 
        /// <summary> True if this binding expression needs validation </summary> 
        internal bool NeedsValidation
        { 
            get { return TestFlag(PrivateFlags.iNeedsValidation) || (_validationError != null); }
            set { ChangeFlag(PrivateFlags.iNeedsValidation, value); }
        }
 
        /// <summary> True if this binding expression should raise the TargetUpdated event </summary>
        internal bool NotifyOnTargetUpdated 
        { 
            get { return TestFlag(PrivateFlags.iNotifyOnTargetUpdated); }
            set { ChangeFlag(PrivateFlags.iNotifyOnTargetUpdated, value); } 
        }

        /// <summary> True if this binding expression should raise the SourceUpdated event </summary>
        internal bool NotifyOnSourceUpdated 
        {
            get { return TestFlag(PrivateFlags.iNotifyOnSourceUpdated); } 
            set { ChangeFlag(PrivateFlags.iNotifyOnSourceUpdated, value); } 
        }
 
        /// <summary> True if this binding expression should raise the ValidationError event </summary>
        internal bool NotifyOnValidationError
        {
            get { return TestFlag(PrivateFlags.iNotifyOnValidationError); } 
            set { ChangeFlag(PrivateFlags.iNotifyOnValidationError, value); }
        } 
 
        /// <summary> True if this binding expression belongs to a PriorityBinding </summary>
        internal bool IsInPriorityBindingExpression 
        {
            get { return TestFlag(PrivateFlags.iInPriorityBindingExpression); }
        }
 
        /// <summary> True if this binding expression belongs to a MultiBinding </summary>
        internal bool IsInMultiBindingExpression 
        { 
            get { return TestFlag(PrivateFlags.iInMultiBindingExpression); }
        } 

        /// <summary> True if this binding expression belongs to a PriorityBinding or MultiBinding </summary>
        internal bool IsInBindingExpressionCollection
        { 
            get { return TestFlag(PrivateFlags.iInPriorityBindingExpression | PrivateFlags.iInMultiBindingExpression); }
        } 
 
        /// <summary> True if this binding expression validates on exceptions </summary>
        internal bool ValidatesOnExceptions 
        {
            get { return TestFlag(PrivateFlags.iValidatesOnExceptions); }
        }
 
        /// <summary> True if this binding expression validates on data errors </summary>
        internal bool ValidatesOnDataErrors 
        { 
            get { return TestFlag(PrivateFlags.iValidatesOnDataErrors); }
        } 

        /// <summary> The parent MultiBindingExpression (if any) </summary>
        internal MultiBindingExpression ParentMultiBindingExpression
        { 
            get { return _parentBindingExpression as MultiBindingExpression; }
        } 
 
        /// <summary> The parent PriorityBindingExpression (if any) </summary>
        internal PriorityBindingExpression ParentPriorityBindingExpression 
        {
            get { return _parentBindingExpression as PriorityBindingExpression; }
        }
 
        /// <summary> The parent PriorityBindingExpression or MultiBindingExpression (if any) </summary>
        internal BindingExpressionBase ParentBindingExpressionBase 
        { 
            get { return _parentBindingExpression; }
        } 

        /// <summary> The FallbackValue (from the parent Binding), possibly converted
        /// to a type suitable for the target property.</summary>
        internal object FallbackValue 
        {
            // perf note: we recompute the value every time it's needed.  This is 
            // a good decision if we seldom need the value.  Alternatively we could 
            // cache it.  Wait until we know what the perf impact really is.
            get { return ConvertFallbackValue(ParentBindingBase.FallbackValue, TargetProperty, this); } 
        }

        /// <summary> The default value of the target property </summary>
        internal object DefaultValue 
        {
            // perf note: we recompute the value every time it's needed.  This is 
            // a good decision if we seldom need the value.  Alternatively we could 
            // cache it.  Wait until we know what the perf impact really is.
            get 
            {
                DependencyObject target = TargetElement;
                if (target != null)
                { 
                    return TargetProperty.GetDefaultValue(target.DependencyObjectType);
                } 
                return DependencyProperty.UnsetValue; 
            }
        } 

        /// <summary> The effective string format, taking into account the target
        /// property, parent bindings, convenience syntax, etc. </summary>
        internal string EffectiveStringFormat 
        {
            get { return _effectiveStringFormat; } 
        } 

        /// <summary> The effective TargetNullValue, taking into account the target 
        /// property, parent bindings, etc. </summary>
        internal object EffectiveTargetNullValue
        {
            get { return _effectiveTargetNullValue; } 
        }
 
        /// <summary> return the root of the tree of {Multi/Priority}BindingExpressions</summary> 
        internal BindingExpressionBase RootBindingExpression
        { 
            get
            {
                BindingExpressionBase child = this;
                BindingExpressionBase parent = this.ParentBindingExpressionBase; 
                while (parent != null)
                { 
                    child = parent; 
                    parent = child.ParentBindingExpressionBase;
                } 
                return child;
            }
        }
 
        /// <summary> return the binding group to which this expression belongs (or null) </summary>
        internal BindingGroup BindingGroup 
        { 
            get
            { 
                BindingExpressionBase root = RootBindingExpression;
                WeakReference wr = root._bindingGroup;
                return (wr == null) ? null : (BindingGroup)wr.Target;
            } 
        }
 
        internal virtual bool IsParentBindingUpdateTriggerDefault 
        {
            get { return false; } 
        }

        internal bool UsesLanguage
        { 
            get { return (ParentBindingBase.ConverterCultureInternal == null); }
        } 
 
        //-----------------------------------------------------
        // 
        //  Protected Methods
        //
        //------------------------------------------------------
 
        /// <summary>
        /// Attach the binding expression to the given target object and property. 
        /// Derived classes should call base.AttachOverride before doing their work, 
        /// and should continue only if it returns true.
        /// </summary> 
        internal virtual bool AttachOverride(DependencyObject target, DependencyProperty dp)
        {
            // get the engine
            DataBindEngine engine = DataBindEngine.CurrentDataBindEngine; 
            if (engine == null || engine.IsShutDown)
            { 
                return false;   // don't even think about doing any work if the DBE is shut down 
            }
            _engine = engine; 

            _targetElement = new WeakReference(target);
            _targetProperty = dp;
 
            DetermineEffectiveStringFormat();
            DetermineEffectiveTargetNullValue(); 
 
            if (TraceData.IsExtendedTraceEnabled(this, TraceDataLevel.Attach))
            { 
                TraceData.Trace(TraceEventType.Warning,
                                    TraceData.AttachExpression(
                                        TraceData.Identify(this),
                                        target.GetType().FullName, dp.Name, AvTrace.GetHashCodeHelper(target))); 
            }
 
            return true; 
        }
 
        /// <summary>
        /// Detach the binding expression from its target object and property.
        /// Derived classes should call base.DetachOverride after doing their work.
        /// </summary> 
        internal virtual void DetachOverride()
        { 
            UpdateValidationError(null); 

            _engine = null; 
            _targetElement = null;
            _targetProperty = null;
            SetStatus(BindingStatus.Detached);
 
            if (TraceData.IsExtendedTraceEnabled(this, TraceDataLevel.Attach))
            { 
                TraceData.Trace(TraceEventType.Warning, 
                                    TraceData.DetachExpression(
                                        TraceData.Identify(this))); 
            }
        }

        /// <summary> 
        /// Invalidate the given child expression.
        /// </summary> 
        internal abstract void InvalidateChild(BindingExpressionBase bindingExpression); 

        /// <summary> 
        /// Change the dependency sources for the given child expression.
        /// </summary>
        internal abstract void ChangeSourcesForChild(BindingExpressionBase bindingExpression, WeakDependencySource[] newSources);
 
        /// <summary>
        /// Replace the given child expression with a new one. 
        /// </summary> 
        internal abstract void ReplaceChild(BindingExpressionBase bindingExpression);
 
        internal abstract void HandlePropertyInvalidation(DependencyObject d, DependencyPropertyChangedEventArgs args);

        private object HandlePropertyInvalidationOperation(object o)
        { 
            // This is the case where the source of the Binding belonged to a different Dispatcher
            // than the target. For this scenario the source marshals off the invalidation information 
            // onto the target's Dispatcher queue. This is where we unpack the marshalled information 
            // to fire the invalidation on the target object.
 
            object[] args = (object[])o;
            HandlePropertyInvalidation((DependencyObject)args[0], (DependencyPropertyChangedEventArgs)args[1]);
            return null;
        } 

        // handle joining or leaving a binding group 
        internal void OnBindingGroupChanged(bool joining) 
        {
            if (joining) 
            {
                // joining a binding group:
                // update Explicitly, unless declared otherwise
                if (IsParentBindingUpdateTriggerDefault) 
                {
                    if (IsUpdateOnLostFocus) 
                    { 
                        LostFocusEventManager.RemoveListener(TargetElement, this);
                    } 

                    SetUpdateSourceTrigger(UpdateSourceTrigger.Explicit);
                }
            } 
            else
            { 
                // leaving a binding group: 
                // restore update trigger
                if (IsParentBindingUpdateTriggerDefault) 
                {
                    // do this asynchronously, to avoid event-leapfrogging.
                    // In the template-swapping scenario (common in DataGrid) it's
                    // common to leave a binding group because you've lost focus. 
                    // We don't want that focus-loss to cause an update.
                    Dispatcher.BeginInvoke( 
                        DispatcherPriority.Loaded, 
                        new DispatcherOperationCallback(RestoreUpdateTriggerOperation),
                        null); 
                }
            }
        }
 
        object RestoreUpdateTriggerOperation(object arg)
        { 
            DependencyObject target = TargetElement; 
            if (Status != BindingStatus.Detached && target != null)
            { 
                FrameworkPropertyMetadata fwMetaData = TargetProperty.GetMetadata(target.DependencyObjectType) as FrameworkPropertyMetadata;

                UpdateSourceTrigger ust = GetDefaultUpdateSourceTrigger(fwMetaData);
                SetUpdateSourceTrigger(ust); 

                if (IsUpdateOnLostFocus) 
                { 
                    LostFocusEventManager.AddListener(target, this);
                } 
            }

            return null;
        } 

        // register the leaf bindings with the binding group 
        internal abstract void UpdateBindingGroup(BindingGroup bg); 

        // transfer a value from target to source 
        internal void UpdateValue()
        {
            ValidationError oldValidationError = _validationError;
 
            if (Status == BindingStatus.UpdateSourceError)
                SetStatus(BindingStatus.Active); 
 
            object value = GetRawProposedValue();
            if (!Validate(value, ValidationStep.RawProposedValue)) 
                return;

            value = ConvertProposedValue(value);
            if (!Validate(value, ValidationStep.ConvertedProposedValue)) 
                return;
 
            value = UpdateSource(value); 
            if (!Validate(value, ValidationStep.UpdatedValue))
                return; 

            value = CommitSource(value);
            if (!Validate(value, ValidationStep.CommittedValue))
                return; 

            if (_validationError == oldValidationError) 
            { 
                // the binding is now valid - remove the old error
                UpdateValidationError(null); 
            }

            EndSourceUpdate();
        } 

        /// <summary> 
        /// Get the raw proposed value 
        /// <summary>
        internal virtual object GetRawProposedValue() 
        {
            object value = Value;

            // TargetNullValue is the UI representation of a "null" value.  Use null internally. 
            if (Object.Equals(value, EffectiveTargetNullValue))
            { 
                value = null; 
            }
 
            return value;
        }

        /// <summary> 
        /// Get the converted proposed value
        /// <summary> 
        internal abstract object ConvertProposedValue(object rawValue); 

        /// <summary> 
        /// Get the converted proposed value and inform the binding group
        /// <summary>
        internal abstract bool ObtainConvertedProposedValue(BindingGroup bindingGroup);
 
        /// <summary>
        /// Update the source value 
        /// <summary> 
        internal abstract object UpdateSource(object convertedValue);
 
        /// <summary>
        /// Update the source value and inform the binding group
        /// <summary>
        internal abstract bool UpdateSource(BindingGroup bindingGroup); 

        /// <summary> 
        /// Commit the source value 
        /// <summary>
        internal virtual object CommitSource(object value) 
        {
            return value;
        }
 
        /// <summary>
        /// Store the value in the binding group 
        /// </summary> 
        internal abstract void StoreValueInBindingGroup(object value, BindingGroup bindingGroup);
 
        /// <summary>
        /// Run validation rules for the given step
        /// <summary>
        internal virtual bool Validate(object value, ValidationStep validationStep) 
        {
            if (value == Binding.DoNothing) 
                return true; 

            if (value == DependencyProperty.UnsetValue) 
            {
                SetStatus(BindingStatus.UpdateSourceError);
                return false;
            } 

            // get old errors from this step - we're about to reevaluate those rules. 
            ValidationError oldValidationError = GetValidationErrors(validationStep); 

            // ignore an error from the implicit DataError rule - this is checked 
            // separately (in BindingExpression.Validate).  [Dev10 575988]
            if (oldValidationError != null &&
                oldValidationError.RuleInError == DataErrorValidationRule.Instance)
            { 
                oldValidationError = null;
            } 
 
            Collection<ValidationRule> validationRules = ParentBindingBase.ValidationRulesInternal;
 
            if (validationRules != null)
            {
                CultureInfo culture = GetCulture();
                switch (validationStep) 
                {
                    case ValidationStep.UpdatedValue: 
                    case ValidationStep.CommittedValue: 
                        // rules at these steps get passed the rule owner
                        value = this; 
                        break;
                }

                foreach (ValidationRule validationRule in validationRules) 
                {
                    if (validationRule.ValidationStep == validationStep) 
                    { 
                        ValidationResult validationResult = validationRule.Validate(value, culture);
 
                        if (!validationResult.IsValid)
                        {
                            if (TraceData.IsExtendedTraceEnabled(this, TraceDataLevel.Update))
                            { 
                                TraceData.Trace(TraceEventType.Warning,
                                                    TraceData.ValidationRuleFailed( 
                                                        TraceData.Identify(this), 
                                                        TraceData.Identify(validationRule)));
                            } 

                            UpdateValidationError( new ValidationError(validationRule, this, validationResult.ErrorContent, null));
                            return false; // kenlai:
                        } 
                    }
                } 
            } 

            // this step is now valid - clear the old error (if any) 
            if (oldValidationError != null && oldValidationError == GetValidationErrors(validationStep))
            {
                UpdateValidationError(null);
            } 

            return true; 
        } 

        /// <summary> 
        /// Run validation rules for the given step, and inform the binding group
        /// <summary>
        internal abstract bool CheckValidationRules(BindingGroup bindingGroup, ValidationStep validationStep);
 
        /// <summary>
        /// Get the proposed value(s) that would be written to the source(s), applying 
        /// conversion and checking UI-side validation rules. 
        /// </summary>
        /// <returns> 
        /// if binding is not dirty:  return true, (values = null)
        /// if conversion and validation succeed:  return true
        /// if conversion or validation fails:  return false
        /// In the last two cases, values is set to a list of ProposedValue records 
        /// describing the proposed values.  Each entry hold a BindingExpression
        /// and a value - the value is UnsetValue if the conversion or validation 
        /// failed. 
        /// </returns>
        internal abstract bool ValidateAndConvertProposedValue(out Collection<ProposedValue> values); 

        internal class ProposedValue
        {
            internal ProposedValue(BindingExpression bindingExpression, object rawValue, object convertedValue) 
            {
                _bindingExpression = bindingExpression; 
                _rawValue = rawValue; 
                _convertedValue = convertedValue;
            } 

            internal BindingExpression BindingExpression { get { return _bindingExpression; } }
            internal object RawValue { get { return _rawValue; } }
            internal object ConvertedValue { get { return _convertedValue; } } 

            BindingExpression   _bindingExpression; 
            object              _rawValue; 
            object              _convertedValue;
        } 

        /// <summary>
        /// Compute the culture, either from the parent Binding, or from the target element.
        /// </summary> 
        internal CultureInfo GetCulture()
        { 
            // lazy initialization, to let the target element acquire all its properties 
            if (_culture == DefaultValueObject)
            { 
                // explicit culture set in Binding
                _culture = ParentBindingBase.ConverterCultureInternal;

                // if that doesn't work, use target element's xml:lang property 
                if (_culture == null)
                { 
                    DependencyObject target = TargetElement; 
                    if (target != null)
                    { 
                        if (IsInTransfer && (TargetProperty == FrameworkElement.LanguageProperty))
                        {
                            // A binding for the Language property needs the value
                            // of the Language property.  This circularity is not 
                            // supported (bug 1274874).
                            if (TraceData.IsEnabled) 
                            { 
                                TraceData.Trace(TraceEventType.Critical, TraceData.RequiresExplicitCulture, TargetProperty.Name, this);
                            } 

                            throw new InvalidOperationException(SR.Get(SRID.RequiresExplicitCulture, TargetProperty.Name));
                        }
 
                        // cache CultureInfo since requerying an inheritable property on every Transfer/Update can be quite expensive
                        // CultureInfo DP rarely changes once a XAML document is loaded. 
                        // To be 100% correct, changes to the CultureInfo attached DP should be tracked 
                        // and cause a re-evaluation of this binding.
                        _culture = ((XmlLanguage) target.GetValue(FrameworkElement.LanguageProperty)).GetSpecificCulture(); 
                    }
                }
            }
            return (CultureInfo)_culture; 
        }
 
        /// <summary> Culture has changed.  Re-fetch the value with the new culture. </summary> 
        internal void InvalidateCulture()
        { 
            _culture = DefaultValueObject;
            RootBindingExpression.UpdateTarget();
        }
 
        /// <summary> Begin a source update </summary>
        internal void BeginSourceUpdate() 
        { 
            ChangeFlag(PrivateFlags.iInUpdate, true);
        } 

        /// <summary> End a source update </summary>
        internal void EndSourceUpdate()
        { 
            if (IsInUpdate && Status == BindingStatus.Active)
            { 
                // After a successful source update, always re-transfer the source value. 
                // This picks up changes that the source item may make in the setter,
                // and applies the converter (if any) to the value.  This fixes 
                // the so-called "$10 bug".
                UpdateTarget();
            }
 
            ChangeFlag(PrivateFlags.iInUpdate | PrivateFlags.iNeedUpdate, false);
        } 
 
        // Consider the markup <Element A="x" B="{Binding...}"/>, and suppose
        // that setting A (to x) causes the element to assign a new value y for B. 
        // (Lots of examples of this:  e.g. setting Selector.SelectedIndex causes
        // Selector to assign a new value to Selector.SelectedItem.)  The end
        // result depends on what order the assignments are done.  If A=x happens
        // first, it assigns y to B but that is later overwritten by the 
        // data-bound value z;  if B happens first, the binding is installed and
        // produces the value z, then the assignment A=x sends the value y through 
        // the binding (if it's two-way) to the data source.  In other words, you 
        // end up with z in the first case, but y in the second, and only the
        // second case changes the data source. 
        //
        // The order of assignment (during initialization) is out of the user's
        // control, especially when the element is part of a template instance.
        // It can depend on the order in which static constructors are called, 
        // which can vary depending on which elements appear first in the markup.
        // To mitigate this mysterious behavior, the following code attempts to 
        // detect the situation and make it appear as if the binding always 
        // happened first.
        internal bool ShouldUpdateWithCurrentValue(DependencyObject target, out object currentValue) 
        {
            if (IsReflective)
            {
                // the bad situation only arises during initialization. After that, 
                // the order of property assignments is determined by the app's
                // normal control flow.  Unfortunately, we can only detect 
                // initialization for framework elements;  fortunately, this covers 
                // all the known cases of the bad situation
                FrameworkObject fo = new FrameworkObject(target); 
                if (!fo.IsInitialized)
                {
                    // if the target property (B) already has a changed value (y),
                    // we're in the bad situation and should propagate y back to 
                    // the data source
                    DependencyProperty dp = TargetProperty; 
                    EntryIndex entryIndex = target.LookupEntry(dp.GlobalIndex); 
                    if (entryIndex.Found)
                    { 
                        EffectiveValueEntry entry = target.GetValueEntry(entryIndex, dp, null, RequestFlags.RawEntry);
                        if (entry.IsCoercedWithCurrentValue)
                        {
                            currentValue = entry.GetFlattenedEntry(RequestFlags.FullyResolved).Value; 
                            if (entry.IsDeferredReference)
                            { 
                                DeferredReference deferredReference = (DeferredReference)currentValue; 
                                currentValue = deferredReference.GetValue(entry.BaseValueSourceInternal);
                            } 
                            return true;
                        }
                    }
                } 
            }
 
            currentValue = null; 
            return false;
        } 


        /// <summary> change the value to the new value, and notify listeners </summary>
        internal void ChangeValue(object newValue, bool notify) 
        {
            object oldValue = (_value != DefaultValueObject) ? _value : DependencyProperty.UnsetValue; 
 
            _value = newValue;
 
            if (notify && ValueChanged != null)
            {
                ValueChanged(this, new BindingValueChangedEventArgs(oldValue, newValue));
            } 
        }
 
        /// <summary> the target value has changed - the source needs to be updated </summary> 
        internal void Dirty()
        { 
            if (!IsInTransfer)
            {
                NeedsUpdate = true;
                if (IsUpdateOnPropertyChanged) 
                    Update(true);
            } 
        } 

        // invalidate the target property 
        internal void Invalidate(bool isASubPropertyChange)
        {
            // don't invalidate during Attach.  The property engine does it
            // already, and it would interfere with the on-demand activation 
            // of style-defined BindingExpressions.
            if (IsAttaching) 
                return; 

            DependencyObject target = TargetElement; 
            if (target != null)
            {
                if (IsInBindingExpressionCollection)
                    ParentBindingExpressionBase.InvalidateChild(this); 
                else
                { 
                    if (TargetProperty != NoTargetProperty) 
                    {
                        // recompute expression 
                        if (!isASubPropertyChange)
                        {
                            target.InvalidateProperty(TargetProperty);
                        } 
                        else
                        { 
                            target.NotifySubPropertyChange(TargetProperty); 
                        }
                    } 
                }
            }
        }
 
        /// <summary> use the Fallback or Default value, called when a real value is not available </summary>
        internal object UseFallbackValue() 
        { 
            object value = FallbackValue;
 
            // if there's a problem with the fallback, use Default instead
            if (value == DefaultValueObject)
            {
                value = DependencyProperty.UnsetValue; 
            }
 
            // OneWayToSource bindings should initialize to the Fallback/Default 
            // value without error
            if (value == DependencyProperty.UnsetValue && IsOneWayToSource) 
            {
                value = DefaultValue;
            }
 
            if (value != DependencyProperty.UnsetValue)
            { 
                UsingFallbackValue = true; 
            }
            else 
            {
                // if fallback isn't available, use Default (except when in a binding collection)
                if (Status == BindingStatus.Active)
                    SetStatus(BindingStatus.UpdateTargetError); 

                if (!IsInBindingExpressionCollection) 
                { 
                    value = DefaultValue;
 
                    if (TraceData.IsEnabled)
                    {
                        TraceData.Trace(TraceEventType.Information, TraceData.NoValueToTransfer, this);
                    } 
                }
            } 
 
            return value;
        } 

        // determine if the given value is "null" (in a general sense)
        internal bool IsNullValue(object value)
        { 
            if (value == null)
                return true; 
 
            if (Convert.IsDBNull(value))
                return true; 

            if (AssemblyHelper.IsSqlNull(value))
                return true;
 
            return false;
        } 
 
        // determine a "null" value appropriate for the given type
        internal object NullValueForType(Type type) 
        {
            if (type == null)
                return null;
 
            if (AssemblyHelper.IsSqlNullableType(type))
                return AssemblyHelper.NullValueForSqlNullableType(type); 
 
            if (!type.IsValueType)
                return null; 

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                return null;
 
            return DependencyProperty.UnsetValue;
        } 
 

        internal ValidationRule LookupValidationRule(Type type) 
        {
            ValidationRule result = ParentBindingBase.GetValidationRule(type);

            if (result == null && _parentBindingExpression != null) 
            {
                result = _parentBindingExpression.LookupValidationRule(type); 
            } 

            return result; 
        }

        // discover the binding group (if any) that this binding should join,
        // and join it.  More precisely, cause the root binding to join. 
        internal void JoinBindingGroup(bool isReflective, DependencyObject contextElement)
        { 
            BindingGroup bindingGroup = RootBindingExpression.FindBindingGroup(isReflective, contextElement); 

            if (bindingGroup != null) 
            {
                JoinBindingGroup(bindingGroup, /*explicit*/false);
            }
        } 

        // remove the binding from its group 
        internal void LeaveBindingGroup() 
        {
            BindingExpressionBase root = RootBindingExpression; 
            BindingGroup bg = root.BindingGroup;
            if (bg != null)
            {
                bg.BindingExpressions.Remove(root); 
                root._bindingGroup = null;
            } 
        } 

        // reevaluate which binding group this binding should join, and perform 
        // the change if required
        internal void RejoinBindingGroup(bool isReflective, DependencyObject contextElement)
        {
            BindingExpressionBase root = RootBindingExpression; 
            BindingGroup oldBindingGroup = root.BindingGroup;
 
            // discover the binding group, as if this were the first time 
            BindingGroup newBindingGroup;
            WeakReference oldBindingGroupWR = root._bindingGroup; 
            root._bindingGroup = null;
            try
            {
                newBindingGroup = root.FindBindingGroup(isReflective, contextElement); 
            }
            finally 
            { 
                root._bindingGroup = oldBindingGroupWR;
            } 

            // if it's different from the current binding group, move
            if (oldBindingGroup != newBindingGroup)
            { 
                root.LeaveBindingGroup();
                if (newBindingGroup != null) 
                { 
                    JoinBindingGroup(newBindingGroup, /*explicit*/false);
                } 
            }
        }

        // discover the binding group (if any) that this root binding should join. 
        internal BindingGroup FindBindingGroup(bool isReflective, DependencyObject contextElement)
        { 
            Debug.Assert(RootBindingExpression == this, "Only call this with a root binding"); 

            // if we've already joined (or failed to join) a group, just return the result 
            if (_bindingGroup != null)
            {
                return (BindingGroup)_bindingGroup.Target;
            } 

            BindingGroup bg; 
            string groupName = ParentBindingBase.BindingGroupName; 

            // a null group name means "don't join any group". 
            if (groupName == null)
            {
                MarkAsNonGrouped();
                return null; 
            }
 
            // an empty group name means join by DataContext 
            if (String.IsNullOrEmpty(groupName))
            { 
                // check further preconditions:
                if (!isReflective ||                // must have target-to-source data flow
                    contextElement == null)         // must use data context
                { 
                    // later child bindings might pass this test, so don't mark
                    // this root binding as non-grouped yet 
                    return null; 
                }
 
                // only the innermost binding group is eligible
                bg = (BindingGroup)contextElement.GetValue(FrameworkElement.BindingGroupProperty);
                if (bg == null)
                { 
                    MarkAsNonGrouped();
                    return null; 
                } 

                // the context element must share data context with the group 
                DependencyProperty dataContextDP = FrameworkElement.DataContextProperty;
                DependencyObject groupContextElement = bg.InheritanceContext;
                if (groupContextElement == null ||
                    !Object.Equals( contextElement.GetValue(dataContextDP), 
                                    groupContextElement.GetValue(dataContextDP)))
                { 
                    MarkAsNonGrouped(); 
                    return null;
                } 

                // if the binding survives the gauntlet, return the group
                return bg;
            } 

            // a non-empty group name means join by name 
            else 
            {
                // walk up the tree, looking for a matching binding group 
                DependencyProperty bindingGroupDP = FrameworkElement.BindingGroupProperty;
                FrameworkObject fo = new FrameworkObject(Helper.FindMentor(TargetElement));
                while (fo.DO != null)
                { 
                    BindingGroup bgCandidate = (BindingGroup)fo.DO.GetValue(bindingGroupDP);
                    if (bgCandidate == null) 
                    { 
                        MarkAsNonGrouped();
                        return null; 
                    }

                    if (bgCandidate.Name == groupName)
                    { 
                        if (bgCandidate.SharesProposedValues && TraceData.IsEnabled)
                        { 
                            TraceData.Trace(TraceEventType.Warning, 
                                    TraceData.SharesProposedValuesRequriesImplicitBindingGroup(
                                            TraceData.Identify(this), 
                                            groupName,
                                            TraceData.Identify(bgCandidate)));
                        }
 
                        // return the matching group
                        return bgCandidate; 
                    } 

                    fo = fo.FrameworkParent; 
                }

                // no match - report an error
                if (TraceData.IsEnabled) 
                {
                    TraceData.Trace(TraceEventType.Error, 
                            TraceData.BindingGroupNameMatchFailed(groupName), 
                            this);
                } 

                MarkAsNonGrouped();
                return null;
            } 
        }
 
        // add a binding to the given binding group 
        internal void JoinBindingGroup(BindingGroup bg, bool explicitJoin)
        { 
            BindingExpressionBase root = null;  // set to non-null by the next loop

            for (   BindingExpressionBase bindingExpr = this;
                    bindingExpr != null; 
                    bindingExpr = bindingExpr.ParentBindingExpressionBase)
            { 
                root = bindingExpr; 

                // bindings in a group update Explicitly, unless declared otherwise 
                bindingExpr.OnBindingGroupChanged(/*joining*/true);

                bg.AddToValueTable(bindingExpr);
            } 

            // add the root binding to the group 
            if (root._bindingGroup == null) 
            {
                // use WeakReference because the BindingGroup contains a strong reference 
                // to the visual tree (via InheritanceContext)
                root._bindingGroup = new WeakReference(bg);

                // when the group is implicitly discovered, always add the root binding to the group's collection. 
                // When the binding is added explicitly - via BindingGroup.BindingExpressions.Add() -
                // check first to see if it has already been added 
                bool addToGroup = explicitJoin ? !bg.BindingExpressions.Contains(root) : true; 
                if (addToGroup)
                { 
                    bg.BindingExpressions.Add(root);
                }

                // in the explicit case, register its items and values with the binding group 
                // (the implicit case does this when the bindings activate)
                if (explicitJoin) 
                { 
                    root.UpdateBindingGroup(bg);
 
                    if (bg.SharesProposedValues && TraceData.IsEnabled)
                    {
                        TraceData.Trace(TraceEventType.Warning,
                                TraceData.SharesProposedValuesRequriesImplicitBindingGroup( 
                                        TraceData.Identify(root),
                                        root.ParentBindingBase.BindingGroupName, 
                                        TraceData.Identify(bg))); 
                    }
                } 
            }
            else
            {
                if (root.BindingGroup != bg) 
                    throw new InvalidOperationException(SR.Get(SRID.BindingGroup_CannotChangeGroups));
            } 
        } 

        // mark a binding as non-grouped, so that we avoid doing the discovery again 
        void MarkAsNonGrouped()
        {
            // Leaf bindings only get asked once, so there's no need to add a mark
            if (!(this is BindingExpression)) 
            {
                _bindingGroup = new WeakReference(null); 
            } 
        }
 
        /// <summary>
        /// Handle events from the centralized event table
        /// </summary>
        internal virtual bool ReceiveWeakEvent(Type managerType, object sender, EventArgs e) 
        {
            return false;       // unrecognized event 
        } 

        private bool TestFlag(PrivateFlags flag) 
        {
            return (_flags & flag) != 0;
        }
 
        private void ChangeFlag(PrivateFlags flag, bool value)
        { 
            if (value)  _flags |=  flag; 
            else        _flags &= ~flag;
        } 

        //-----------------------------------------------------
        //
        //  Internal Properties 
        //
        //------------------------------------------------------ 
 
        internal DependencyProperty TargetProperty { get { return _targetProperty; } }
 
        // A BindingExpression cannot hold a strong reference to the target element - this
        // leads to memory leaks (bug 871139).  The problem is that BindingExpression and its workers
        // register for events from the data item, creating a reference from
        // the data item to the BindingExpression.  The data item typically has a long lifetime, 
        // so if the BindingExpression held a SR to the target, the target element would
        // also stay alive forever. 
        //      Instead, BindingExpression holds a WeakReference to the target.  This means we 
        // have to check it before dereferencing (here), and cope when the
        // reference fails (in callers to this property).  Requests for the TargetElement 
        // are not trivial, so callers should request it once and cache the result
        // in a local variable.  They should not save it in a global or instance
        // variable of course;  that would defeat the purpose of the WR.
        //      This allows the target element to be GC'd when it's no longer in 
        // use by the tree or application.  The next time the BindingExpression asks for
        // its TargetElement, the WR will fail.  At this point, the BindingExpression is no 
        // longer useful, so it can sever all connections to the outside world (i.e. 
        // stop listening for events).  This allows the BindingExpression itself to be GC'd.
        internal DependencyObject TargetElement 
        {
            get
            {
                if (_targetElement != null) 
                {
                    DependencyObject result = _targetElement.Target as DependencyObject; 
                    if (result != null) 
                        return result;
 
                    // target has been GC'd, sever all connections
                    _targetElement = null;      // prevents re-entry from Detach()
                    Detach();
                } 

                return null; 
            } 
        }
 
        internal WeakReference TargetElementReference
        {
            get { return _targetElement; }
        } 

        internal DataBindEngine Engine 
        { 
            get { return _engine; }
        } 

        internal Dispatcher Dispatcher
        {
            get { return (_engine != null) ? _engine.Dispatcher : null; } 
        }
 
        internal object Value 
        {
            get 
            {
                if (_value == DefaultValueObject)
                {
                    // don't notify listeners.  This isn't a real value change. 
                    ChangeValue(UseFallbackValue(), false /*notify*/);
                } 
                return _value; 
            }
            set 
            {
                ChangeValue(value, true);
                Dirty();
            } 
        }
 
        internal WeakDependencySource[] WeakSources 
        {
            get { return _sources; } 
        }

        // queried by MultiBindings.  Only implemented today by BindingExpression.
        // If we ever support nested MultiBindings, implement this in MultiBinding 
        // as well.
        internal virtual bool IsDisconnected 
        { 
            get { return false; }
        } 

        /// <summary>
        ///     NoTarget DependencyProperty, a placeholder used by BindingExpressions with no target property
        /// </summary> 
        internal static readonly DependencyProperty NoTargetProperty =
                DependencyProperty.RegisterAttached("NoTarget", typeof(object), typeof(BindingExpressionBase), 
                                            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.None)); 

        internal TraceLog TraceLog { get { return _traceLog; } } 

        //------------------------------------------------------
        //
        //  Internal Methods 
        //
        //----------------------------------------------------- 
 
        /// <summary>
        /// Attach the binding expression to the given target object and property. 
        /// </summary>
        internal void Attach(DependencyObject target, DependencyProperty dp)
        {
            // make sure we're on the right thread to access the target 
            if (target != null)
            { 
                target.VerifyAccess(); 
            }
 
            IsAttaching = true;
            AttachOverride(target, dp);
            IsAttaching = false;
        } 

        /// <summary> 
        /// Detach the binding expression from its target object and property. 
        /// </summary>
        internal void Detach() 
        {
            if (_status == BindingStatus.Detached || IsDetaching)
                return;
 
            IsDetaching = true;
 
            // leave the binding group before doing anything else, so as to preserve 
            // a proposed value (if any).
            LeaveBindingGroup(); 

            DetachOverride();
            IsDetaching = false;
        } 

        // disconnect from sources, and update the target property as cheaply as possible 
        internal virtual void Disconnect() 
        {
            // determine the new value (if any) to expose 
            object newValue = DependencyProperty.UnsetValue;    // usually none
            DependencyProperty targetDP = TargetProperty;

            if (targetDP == ContentControl.ContentProperty || 
                targetDP == ContentPresenter.ContentProperty ||
                targetDP == HeaderedItemsControl.HeaderProperty || 
                targetDP == HeaderedContentControl.HeaderProperty) 
            {
                // pass DisconnectedItem through content-like properties, so 
                // that their descendent bindings can disconnect
                newValue = DisconnectedItem;
            }
 
            if (targetDP.PropertyType == typeof(System.Collections.IEnumerable))
            { 
                // set IEnumerable properties (e.g. ItemsSource) to null, so that 
                // the control can disconnect from the individual items
                newValue = null; 
            }

            // notify the target property about the new value (cheaply)
            if (newValue != DependencyProperty.UnsetValue) 
            {
                ChangeValue(newValue, false); 
                Invalidate(false); 
            }
        } 

        internal void SetStatus(BindingStatus status)
        {
            if (_status == BindingStatus.Detached && status != _status) 
            {
                throw new InvalidOperationException(SR.Get(SRID.BindingExpressionStatusChanged, _status, status)); 
            } 

            _status = status; 

            if (_traceLog != null)
            {
                _traceLog.Add("Set status = {0} {1}", _status, new StackTrace()); 
            }
        } 
 
        // convert a user-supplied fallback value to a usable equivalent
        //  returns:    UnsetValue          if user did not supply a fallback value 
        //              value               if fallback value is legal
        //              DefaultValueObject  otherwise
        internal static object ConvertFallbackValue(object value, DependencyProperty dp, object sender)
        { 
            Exception e;
            object result = ConvertValue(value, dp, out e); 
 
            if (result == DefaultValueObject)
            { 
                if (TraceData.IsEnabled)
                {
                    TraceData.Trace(TraceEventType.Error,
                            TraceData.FallbackConversionFailed( 
                                AvTrace.ToStringHelper(value),
                                AvTrace.TypeName(value), 
                                dp.Name, 
                                dp.PropertyType.Name),
                            sender, e); 
                }
            }

            return result; 
        }
 
        // convert a user-supplied TargetNullValue to a usable equivalent 
        //  returns:    UnsetValue          if user did not supply a fallback value
        //              value               if fallback value is legal 
        //              DefaultValueObject  otherwise
        internal static object ConvertTargetNullValue(object value, DependencyProperty dp, object sender)
        {
            Exception e; 
            object result = ConvertValue(value, dp, out e);
 
            if (result == DefaultValueObject) 
            {
                if (TraceData.IsEnabled) 
                {
                    TraceData.Trace(TraceEventType.Error,
                            TraceData.TargetNullValueConversionFailed(
                                AvTrace.ToStringHelper(value), 
                                AvTrace.TypeName(value),
                                dp.Name, 
                                dp.PropertyType.Name), 
                            sender, e);
                } 
            }

            return result;
        } 

        static object ConvertValue(object value, DependencyProperty dp, out Exception e) 
        { 
            object result;
            e = null; 

            if (value == DependencyProperty.UnsetValue || dp.IsValidValue(value))
            {
                result = value; 
            }
            else 
            { 
                result = null;  // placeholder to keep compiler happy
                // if value isn't the right type, use a type converter to get a better value 
                bool success = false;
                TypeConverter converter = DefaultValueConverter.GetConverter(dp.PropertyType);
                if (converter != null && converter.CanConvertFrom(value.GetType()))
                { 
                    // PreSharp uses message numbers that the C# compiler doesn't know about.
                    // Disable the C# complaints, per the PreSharp documentation. 
                    #pragma warning disable 1634, 1691 

                    // PreSharp complains about catching NullReference (and other) exceptions. 
                    // It doesn't recognize that IsCriticalException() handles these correctly.
                    #pragma warning disable 56500

                    try 
                    {
                        result = converter.ConvertFrom(null, CultureInfo.InvariantCulture, value); 
                        success = dp.IsValidValue(result); 
                    }
 
                    // Catch all exceptions.  If we can't convert the fallback value, it doesn't
                    // matter why not;  we should always use the default value instead.
                    // (See bug 1853628 for an example of a converter that throws
                    // an exception not mentioned in the documentation for ConvertFrom.) 
                    catch (Exception ex)
                    { 
                        e = ex; 
                    }
                    catch // non CLS compliant exception 
                    {
                    }

                    #pragma warning restore 56500 
                    #pragma warning restore 1634, 1691
                } 
 
                if (!success)
                { 
                    // if can't convert it, don't use it
                    result = DefaultValueObject;
                }
            } 

            return result; 
        } 

        // Certain trace reports should be marked as 'error' unless the binding 
        // is prepared to handle it in some way (e.g. FallbackValue), in which
        // case 'warning'.
        internal TraceEventType TraceLevel
        { 
            get
            { 
                // FallbackValue is present 
                if (ParentBindingBase.FallbackValue != DependencyProperty.UnsetValue)
                    return TraceEventType.Warning; 

                // Binding is a member of MultiBinding or PriorityBinding
                if (IsInBindingExpressionCollection)
                    return TraceEventType.Warning; 

                // all other cases - error 
                return TraceEventType.Error; 
            }
        } 

        internal virtual void Activate()
        {
        } 

        internal virtual void Deactivate() 
        { 
        }
 
        internal virtual void Update(bool synchronous)
        {
        }
 
        internal void UpdateValidationError(ValidationError validationError)
        { 
            // the steps are carefully ordered to avoid going through a "no error" 
            // state while replacing one error with another
            ValidationError oldValidationError = _validationError; 

            _validationError = validationError;

            if (validationError != null) 
            {
                AddValidationError(_validationError); 
            } 

            if (oldValidationError != null) 
            {
                RemoveValidationError(oldValidationError);
            }
        } 

        internal void AddValidationError(ValidationError validationError) 
        { 
            // add the error to the target element
            Validation.AddValidationError(validationError, TargetElement, NotifyOnValidationError); 

            // add the error to the binding group's target element
            BindingGroup bindingGroup = BindingGroup;
            if (bindingGroup != null) 
            {
                bindingGroup.AddValidationError(validationError); 
            } 
        }
 
        internal void RemoveValidationError(ValidationError validationError)
        {
            // remove the error from the target element
            Validation.RemoveValidationError(validationError, TargetElement, NotifyOnValidationError); 

            // remove the error from the binding group's target element 
            BindingGroup bindingGroup = BindingGroup; 
            if (bindingGroup != null)
            { 
                bindingGroup.RemoveValidationError(validationError);
            }
        }
 
        // get all errors raised at the given step, in preparation for running
        // the rules at that step 
        internal ValidationError GetValidationErrors(ValidationStep validationStep) 
        {
            if (_validationError == null || _validationError.RuleInError.ValidationStep != validationStep) 
                return null;

            return _validationError;
        } 

        internal void ChangeSources(WeakDependencySource[] newSources) 
        { 
            if (IsInBindingExpressionCollection)
                ParentBindingExpressionBase.ChangeSourcesForChild(this, newSources); 
            else
                ChangeSources(TargetElement, TargetProperty, newSources);

            // store the sources with weak refs, so they don't cause memory leaks (bug 980041) 
            _sources = newSources;
        } 
 
        /// <summary>
        /// combine the sources of BindingExpressions, using new sources for 
        /// the BindingExpression at the given index
        /// </summary>
        /// <param name="index">-1 to indicate no new sources</param>
        /// <param name="bindingExpressions">collection of child binding expressions </param> 
        /// <param name="count">how many child expressions to include</param>
        /// <param name="newSources">use null when no new sources</param> 
        /// <returns></returns> 
        internal static WeakDependencySource[] CombineSources(int index, Collection<BindingExpressionBase> bindingExpressions, int count, WeakDependencySource[] newSources)
        { 
            if (index == count)
            {
                // Be sure to include newSources if they are being appended
                count++; 
            }
 
            Collection<WeakDependencySource> tempList = new Collection<WeakDependencySource>(); 

            for (int i = 0; i < count; ++i) 
            {
                BindingExpressionBase bindExpr = bindingExpressions[i];
                WeakDependencySource[] sources = (i==index) ? newSources :
                                            (bindExpr != null) ? bindExpr.WeakSources : 
                                            null;
                int m = (sources == null) ? 0 : sources.Length; 
                for (int j = 0; j < m; ++j) 
                {
                    WeakDependencySource candidate = sources[j]; 

                    // don't add duplicate source
                    for (int k = 0; k < tempList.Count; ++k)
                    { 
                        WeakDependencySource prior = tempList[k];
                        if (candidate.DependencyObject == prior.DependencyObject && 
                            candidate.DependencyProperty == prior.DependencyProperty) 
                        {
                            candidate = null; 
                            break;
                        }
                    }
 
                    if (candidate != null)
                        tempList.Add(candidate); 
                } 
            }
 
            WeakDependencySource[] result;
            if (tempList.Count > 0)
            {
                result = new WeakDependencySource[tempList.Count]; 
                tempList.CopyTo(result, 0);
                tempList.Clear(); 
            } 
            else
            { 
                result = null;
            }

            return result; 
        }
 
        internal void ResolvePropertyDefaultSettings(BindingMode mode, UpdateSourceTrigger updateTrigger, FrameworkPropertyMetadata fwMetaData) 
        {
            // resolve "property-default" dataflow 
            if (mode == BindingMode.Default)
            {
                BindingFlags f = BindingFlags.OneWay;
                if (fwMetaData != null && fwMetaData.BindsTwoWayByDefault) 
                {
                    f = BindingFlags.TwoWay; 
                } 

                ChangeFlag(PrivateFlags.iPropagationMask, false); 
                ChangeFlag((PrivateFlags)f, true);

                if (TraceData.IsExtendedTraceEnabled(this, TraceDataLevel.ResolveDefaults))
                { 
                    TraceData.Trace(TraceEventType.Warning,
                                        TraceData.ResolveDefaultMode( 
                                            TraceData.Identify(this), 
                                            (f == BindingFlags.OneWay) ? BindingMode.OneWay : BindingMode.TwoWay));
                } 
            }

            Debug.Assert((_flags & PrivateFlags.iPropagationMask) != PrivateFlags.iPropDefault,
                "BindingExpression should not have Default propagation"); 

            // resolve "property-default" update trigger 
            if (updateTrigger == UpdateSourceTrigger.Default) 
            {
                UpdateSourceTrigger ust = GetDefaultUpdateSourceTrigger(fwMetaData); 

                SetUpdateSourceTrigger(ust);

                if (TraceData.IsExtendedTraceEnabled(this, TraceDataLevel.ResolveDefaults)) 
                {
                    TraceData.Trace(TraceEventType.Warning, 
                                        TraceData.ResolveDefaultUpdate( 
                                            TraceData.Identify(this),
                                            ust)); 
                }
            }

            Invariant.Assert((_flags & PrivateFlags.iUpdateMask) != PrivateFlags.iUpdateDefault, 
                "BindingExpression should not have Default update trigger");
        } 
 
        // return the effective update trigger, used when binding doesn't set one explicitly
        internal UpdateSourceTrigger GetDefaultUpdateSourceTrigger(FrameworkPropertyMetadata fwMetaData) 
        {
            UpdateSourceTrigger ust =
                IsInMultiBindingExpression ? UpdateSourceTrigger.Explicit :
                (fwMetaData != null) ? fwMetaData.DefaultUpdateSourceTrigger : 
                                    UpdateSourceTrigger.PropertyChanged;
            return ust; 
        } 

        internal void SetUpdateSourceTrigger(UpdateSourceTrigger ust) 
        {
            ChangeFlag(PrivateFlags.iUpdateMask, false);
            ChangeFlag((PrivateFlags)BindingBase.FlagsFrom(ust), true);
        } 

        internal Type GetEffectiveTargetType() 
        { 
            Type targetType = TargetProperty.PropertyType;
            BindingExpressionBase be = this.ParentBindingExpressionBase; 

            while (be != null)
            {
                if (be is MultiBindingExpression) 
                {
                    // for descendants of a MultiBinding, the effective target 
                    // type is Object. 
                    targetType = typeof(Object);
                    break; 
                }

                be = be.ParentBindingExpressionBase;
            } 

            return targetType; 
        } 

        internal void DetermineEffectiveStringFormat() 
        {
            Type targetType = TargetProperty.PropertyType;
            if (targetType != typeof(String))
            { 
                // if the target type isn't String, we don't need a string format
                return; // _effectiveStringFormat is already null 
            } 

            // determine the effective target type and the declared string format 
            // by looking up the tree of binding expressions
            string stringFormat = ParentBindingBase.StringFormat;
            BindingExpressionBase be = this.ParentBindingExpressionBase;
 
            while (be != null)
            { 
                if (be is MultiBindingExpression) 
                {
                    // MultiBindings should receive object values, not string 
                    targetType = typeof(Object);
                    break;
                }
                else if (stringFormat == null && be is PriorityBindingExpression) 
                {
                    // use a PriorityBinding's string format, unless we already 
                    // have a more specific one 
                    stringFormat = be.ParentBindingBase.StringFormat;
                } 

                be = be.ParentBindingExpressionBase;
            }
 
            // if we need a string format, cache it
            if (targetType == typeof(String)) 
            { 
                #if NotToday
                // special case: when these conditions all apply 
                //      a) target element belongs to a DataTemplate
                //      b) template was found by implicit (type-based) lookup
                //      c) container (ContentPresenter) has a string format
                //      d) binding has no effective string format 
                // then use the CP's string format.  This enables scenarios like
                //      <DataTemplate DataType="{x:Type Customer}"> 
                //          <TextBlock Text="{Binding Path=AmountPayable}" 
                //      </DataTemplate>
                //      <ContentControl Content="{StaticResource MyCustomer}" ContentStringFormat="C2"/> 
                // where you'd like to control the format at the point of use,
                // rather than at the point of template declaration.  This is
                // especially useful when the template is declared in a global place
                // like app resources or a theme file.  In particular it makes the 
                // GroupStyle.HeaderStringFormat property work;  the template for
                // GroupItem is defined in the theme, but needs to pick up formatting 
                // from the app's markup. 

                if (stringFormat == null)   // (d) 
                {
                    FrameworkObject fo = new FrameworkObject(Helper.FindMentor(TargetElement));
                    ContentPresenter cp = fo.TemplatedParent as ContentPresenter;
                    if (cp != null &&                       // (a) 
                        cp.ContentStringFormat != null)     // (c)
                    { 
                        DataTemplate dt = cp.TemplateInternal as DataTemplate; 
                        if (dt != null &&                   // (a)
                            dt.DataType != null)            // (b) 
                        {
                            stringFormat = cp.ContentStringFormat;
                        }
                    } 
                }
                #endif 
 
                if (!String.IsNullOrEmpty(stringFormat))
                { 
                    _effectiveStringFormat = Helper.GetEffectiveStringFormat(stringFormat);
                }
            }
        } 

        internal void DetermineEffectiveTargetNullValue() 
        { 
            Type targetType = TargetProperty.PropertyType;
 
            // determine the effective target type and the declared TargetNullValue
            // by looking up the tree of binding expressions
            object targetNullValue = ParentBindingBase.TargetNullValue;
            BindingExpressionBase be = this.ParentBindingExpressionBase; 

            while (be != null) 
            { 
                if (be is MultiBindingExpression)
                { 
                    // MultiBindings should receive object values
                    targetType = typeof(Object);
                    break;
                } 
                else if (targetNullValue == DependencyProperty.UnsetValue && be is PriorityBindingExpression)
                { 
                    // use a PriorityBinding's TargetNullValue, unless we already 
                    // have a more specific one
                    targetNullValue = be.ParentBindingBase.TargetNullValue; 
                }

                be = be.ParentBindingExpressionBase;
            } 

            // if user declared a TargetNullValue, make sure it has the right type. 
            if (targetNullValue != DependencyProperty.UnsetValue) 
            {
                targetNullValue = ConvertTargetNullValue(targetNullValue, TargetProperty, this); 
                if (targetNullValue == DefaultValueObject)
                {
                    // if not, ignore it (having logged a trace message)
                    targetNullValue = DependencyProperty.UnsetValue; 
                }
            } 
 
            // for back-compat, don't turn on TargetNullValue unless user explicitly
            // asks for it.  This means users have to add TargetNullValue to get 
            // pretty basic scenarios to work (e.g. binding a TextBox to a database
            // string field that supports DBNull).  It's painful, but can't be
            // helped.
            #if TargetNullValueBC //BreakingChange 
            // if no declared (or poorly declared) value, make one up
            if (targetNullValue == DependencyProperty.UnsetValue) 
            { 
                targetNullValue = NullValueForType(targetType);
            } 
            #endif

            _effectiveTargetNullValue = targetNullValue;
        } 

        // To prevent memory leaks, we store WeakReferences to certain objects 
        // in various places:  _dataItem, _sources, worker fields.  The logic 
        // for this is centralized in these two static methods.  (See bug 940041)
 
        internal static object CreateReference(object item)
        {
            // One source of leaks is the reference cycle formed when a BindingExpression's
            // source item contains a reference chain to the target element: 
            //      target -> BindingExpression -> source item -> target
            // 
            // Making the second link into a WeakReference incurs some cost, 
            // so it should be avoided if we know the third link never exists.
            // We definitely can't avoid this when the item is a DependencyObject, 
            // since it's reasonably common for the third link to occur (e.g.
            // a ContextMenu contains a link to its Popup, which has a property
            // bound back to the ContextMenu).
            // 
            // For safety, we choose to use WeakRef all the time, unless the item is null.
            // Exception (bug 1124954):  Keep a strong reference to 
            // BindingListCollectionView - this keeps the underlying DataView 
            // alive, when nobody else will.
            // Exception (bug 1970505):  Don't allocate a WeakRef for the common 
            // case of the NullDataItem

            if (item != null &&
                !(item is BindingListCollectionView) && 
                !(item == BindingExpression.NullDataItem) &&
                !(item == DisconnectedItem)) 
            { 
                item = new WeakReference(item);
            } 

#if USE_ITEM_REFERENCE
            item = new ItemReference(item);
#endif 

            return item; 
        } 

        // like CreateReference, but use an existing WeakReference 
        internal static object CreateReference(WeakReference item)
        {
            object result = item;
#if USE_ITEM_REFERENCE 
            result = new ItemReference(item);
#endif 
            return result; 
        }
 
        // like CreateReference, except re-target the old WeakReference (if any)
        internal static object ReplaceReference(object oldReference, object item)
        {
            if (item != null && 
                !(item is BindingListCollectionView) &&
                !(item == BindingExpression.NullDataItem) && 
                !(item == DisconnectedItem)) 
            {
#if USE_ITEM_REFERENCE 
                // if this cast fails, it's because you have done a direct assignment of an
                // item to some field instead of assigning the result of CreateReference.
                oldReference = ((ItemReference)oldReference).Item;
#endif 
                WeakReference wr = oldReference as WeakReference;
                if (wr != null) 
                { 
                    wr.Target = item;
                    item = wr; 
                }
                else
                {
                    item = new WeakReference(item); 
                }
            } 
 
#if USE_ITEM_REFERENCE
            item = new ItemReference(item); 
#endif

            return item;
        } 

        internal static object GetReference(object reference) 
        { 
            if (reference == null)
                return null; 

#if USE_ITEM_REFERENCE
            // if this cast fails, it's because you have done a direct assignment of an
            // item to some field instead of assigning the result of CreateReference. 
            reference = ((ItemReference)reference).Item;
#endif 
 
            WeakReference wr = reference as WeakReference;
            if (wr != null) 
                return wr.Target;
            else
                return reference;
        } 

        internal static void InitializeTracing(BindingExpressionBase expr, DependencyObject d, DependencyProperty dp) 
        { 
            BindingBase parent = expr.ParentBindingBase;
        } 

        //------------------------------------------------------
        //
        //  Private Methods 
        //
        //----------------------------------------------------- 
 
#if USE_ITEM_REFERENCE
 
        private class ItemReference
        {
            internal ItemReference(object item)
            { 
                _item = item;
            } 
 
            internal object Item { get { return _item; } }
 
            object _item;
        }

#endif 

        // change WeakDependencySources to (strong) DependencySources, and notify 
        // the property engine about the new sources 
        void ChangeSources(DependencyObject target, DependencyProperty dp, WeakDependencySource[] newSources)
        { 
            DependencySource[] sources;

            if (newSources != null)
            { 
                // convert weak reference to strong
                sources = new DependencySource[newSources.Length]; 
                int n = 0; 
                for (int i = 0; i < newSources.Length; ++i)
                { 
                    DependencyObject sourceDO = newSources[i].DependencyObject;
                    if (sourceDO != null)
                    {
                        // only include sources that are still alive 
                        sources[n++] = new DependencySource(sourceDO, newSources[i].DependencyProperty);
                    } 
                } 

                // if any of the sources were no longer alive, trim the array 
                if (n < newSources.Length)
                {
                    DependencySource[] temp;
                    if (n > 0) 
                    {
                        temp = new DependencySource[n]; 
                        Array.Copy(sources, 0, temp, 0, n); 
                    }
                    else 
                    {
                        temp = null;
                    }
 
                    sources = temp;
                } 
            } 
            else
            { 
                sources = null;
            }

            // notify property engine 
            ChangeSources(target, dp, sources);
        } 
 
        // this method is here just to avoid the compiler error
        // error CS0649: Warning as Error: Field 'System.Windows.Data.BindingExpression._traceLog' is never assigned to, and will always have its default value null 
        void InitializeTraceLog()
        {
            _traceLog = new TraceLog(20);
        } 

        //----------------------------------------------------- 
        // 
        //  Private Fields
        // 
        //-----------------------------------------------------

        BindingBase         _binding;
        WeakReference       _targetElement; 
        DependencyProperty  _targetProperty;
        DataBindEngine      _engine; 
        PrivateFlags        _flags; 
        BindingExpressionBase _parentBindingExpression;
        object              _value = DefaultValueObject; 
        BindingStatus       _status;
        TraceLog            _traceLog;
        ValidationError     _validationError;
        WeakDependencySource[]  _sources; 
        string              _effectiveStringFormat;
        object              _effectiveTargetNullValue; 
        WeakReference       _bindingGroup; 

        object                  _culture = DefaultValueObject; 

        /// <summary> Sentinel meaning "field has its default value" </summary>
        internal static readonly object DefaultValueObject = new NamedObject("DefaultValue");
 
        // sentinel value meaning "unhook from previous item's events, but do not
        // change values (except for special cases)" 
        internal static readonly object DisconnectedItem = new NamedObject("DisconnectedItem"); 
    }
 
}


